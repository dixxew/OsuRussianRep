using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using OsuRussianRep.Context;
using OsuRussianRep.Models.ChatStatics;

namespace OsuRussianRep.Services;

public sealed class WordFrequencyIngestService : BackgroundService
{
    private readonly IServiceProvider _sp;

    private static readonly Regex TokenRx =
        new(@"\p{L}[\p{L}\p{M}\p{N}_]*|\p{N}+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public WordFrequencyIngestService(IServiceProvider sp) => _sp = sp;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Tick(ct);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    private async Task Tick(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));

        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var days = new[] {todayUtc.AddDays(-2), todayUtc};

        foreach (var day in days)
            await ProcessDay(db, day, ct);
    }

    private static DateOnly UtcDay(DateTime utc) => DateOnly.FromDateTime(utc.Date);

    private static (DateTime fromUtc, DateTime toUtc) DayBoundsUtc(DateOnly day)
    {
        // границы суток в UTC [from; to)
        var from = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return (from, from.AddDays(1));
    }

    private async Task ProcessDay(AppDbContext db, DateOnly day, CancellationToken ct)
    {
        var offset = await db.IngestOffsets.FindAsync([day], ct);
        var isNewOffset = offset is null;
        offset ??= new IngestOffset {Day = day, LastSeq = 0};

        const int batchSize = 20_000;
        var (fromUtc, toUtc) = DayBoundsUtc(day);

        while (true)
        {
            var msgs = await db.Messages.AsNoTracking()
                .Where(m => m.Date >= fromUtc && m.Date < toUtc && m.Seq > offset.LastSeq)
                .OrderBy(m => m.Seq)
                .Select(m => new {m.Seq, m.Text, m.UserId})
                .Take(batchSize)
                .ToListAsync(ct);

            if (msgs.Count == 0)
                break;

            var batchCounts = new Dictionary<string, long>(StringComparer.Ordinal);
            var userWordCounts = new Dictionary<(Guid userId, string lemma), long>();

            foreach (var m in msgs)
            {
                if (!string.IsNullOrWhiteSpace(m.Text))
                {
                    foreach (Match t in TokenRx.Matches(m.Text.ToLowerInvariant()))
                    {
                        var w = t.Value;
                        if (w.Length == 0) continue;

                        // глобальный счётчик по дню
                        batchCounts.TryGetValue(w, out var c);
                        batchCounts[w] = c + 1;

                        // счётчик по юзеру
                        userWordCounts.TryGetValue((m.UserId, w), out var cu);
                        userWordCounts[(m.UserId, w)] = cu + 1;
                    }
                }

                offset.LastSeq = m.Seq;
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            if (batchCounts.Count > 0)
            {
                var lemmas = batchCounts.Keys
                    .Concat(userWordCounts.Keys.Select(k => k.lemma))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                var pLemmas = new NpgsqlParameter("lemmas", NpgsqlDbType.Array | NpgsqlDbType.Text) {Value = lemmas};

                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "Words" ("Lemma")
                    SELECT x FROM unnest(@lemmas) AS x
                    ON CONFLICT ("Lemma") DO NOTHING;
                    """,
                    new object[] {pLemmas}, ct);

                var map = await db.Words.AsNoTracking()
                    .Where(w => lemmas.Contains(w.Lemma))
                    .ToDictionaryAsync(w => w.Lemma, w => w.Id, StringComparer.Ordinal, ct);

                // === обновляем WordDays (как было)
                var dArr = new DateOnly[batchCounts.Count];
                var widArr = new long[batchCounts.Count];
                var cArr = new long[batchCounts.Count];
                int i = 0;
                foreach (var (lemma, cnt) in batchCounts)
                {
                    dArr[i] = day;
                    widArr[i] = map[lemma];
                    cArr[i] = cnt;
                    i++;
                }

                var pDays = new NpgsqlParameter("d", NpgsqlDbType.Array | NpgsqlDbType.Date) {Value = dArr};
                var pWids = new NpgsqlParameter("w", NpgsqlDbType.Array | NpgsqlDbType.Bigint) {Value = widArr};
                var pCnts = new NpgsqlParameter("c", NpgsqlDbType.Array | NpgsqlDbType.Bigint) {Value = cArr};

                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "WordDays" ("Day","WordId","Cnt")
                    SELECT d, wid, c
                    FROM unnest(@d::date[], @w::bigint[], @c::bigint[]) AS t(d, wid, c)
                    ON CONFLICT ("Day","WordId")
                    DO UPDATE SET "Cnt" = "WordDays"."Cnt" + EXCLUDED."Cnt";
                    """,
                    new object[] {pDays, pWids, pCnts}, ct);

                // === новая часть: WordUsers
                if (userWordCounts.Count > 0)
                {
                    var uArr = new Guid[userWordCounts.Count];
                    var wuWids = new long[userWordCounts.Count];
                    var wuCnts = new long[userWordCounts.Count];
                    int j = 0;
                    foreach (var ((userId, lemma), cnt) in userWordCounts)
                    {
                        uArr[j] = userId;
                        wuWids[j] = map[lemma];
                        wuCnts[j] = cnt;
                        j++;
                    }

                    var pUids = new NpgsqlParameter("u", NpgsqlDbType.Array | NpgsqlDbType.Uuid) {Value = uArr};
                    var pWuWids = new NpgsqlParameter("w", NpgsqlDbType.Array | NpgsqlDbType.Bigint) {Value = wuWids};
                    var pWuCnts = new NpgsqlParameter("c", NpgsqlDbType.Array | NpgsqlDbType.Bigint) {Value = wuCnts};

                    await db.Database.ExecuteSqlRawAsync(
                        """
                        INSERT INTO "WordUsers" ("UserId","WordId","Cnt")
                        SELECT u, wid, c
                        FROM unnest(@u::uuid[], @w::bigint[], @c::bigint[]) AS t(u, wid, c)
                        ON CONFLICT ("UserId","WordId")
                        DO UPDATE SET "Cnt" = "WordUsers"."Cnt" + EXCLUDED."Cnt";
                        """,
                        new object[] {pUids, pWuWids, pWuCnts}, ct);
                }
            }

            if (isNewOffset) db.IngestOffsets.Add(offset);
            else db.IngestOffsets.Update(offset);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            if (msgs.Count < batchSize)
                break;
        }
    }
}