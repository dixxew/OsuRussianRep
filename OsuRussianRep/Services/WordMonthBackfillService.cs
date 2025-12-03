using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using OsuRussianRep.Context;

namespace OsuRussianRep.Services;

public sealed class WordMonthBackfillService(IServiceProvider sp, ILogger<WordMonthBackfillService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var minDay = await db.WordsInDay
            .MinAsync(w => (DateOnly?)w.Day, ct);

        if (minDay is null)
        {
            logger.LogWarning("Нет данных — нечего делать.");
            return;
        }

        var start = minDay.Value;
        var end = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        logger.LogInformation($"Стартуем с {start} до {end}");

        var current = new DateOnly(start.Year, start.Month, 1);

        while (current <= end && !ct.IsCancellationRequested)
        {
            await ProcessMonth(db, current, ct);
            current = current.AddMonths(1);
        }

        logger.LogInformation("Заверешно");
    }

    private async Task ProcessMonth(AppDbContext db, DateOnly month, CancellationToken ct)
    {
        var monthStart = month;
        var monthEnd = month.AddMonths(1);

        logger.LogInformation($"Месяц {monthStart}");

        // собираем все WordDays за месяц
        var daily = await db.WordsInDay
            .AsNoTracking()
            .Where(wd => wd.Day >= monthStart && wd.Day < monthEnd)
            .GroupBy(wd => wd.WordId)
            .Select(g => new
            {
                WordId = g.Key,
                Cnt = g.Sum(x => x.Cnt)
            })
            .ToListAsync(ct);

        if (daily.Count == 0)
        {
            logger.LogInformation($"Месяц {monthStart} пустой, пропускаем");
            return;
        }

        var mArr = new DateOnly[daily.Count];
        var wArr = new long[daily.Count];
        var cArr = new long[daily.Count];

        for (int i = 0; i < daily.Count; i++)
        {
            mArr[i] = month;
            wArr[i] = daily[i].WordId;
            cArr[i] = daily[i].Cnt;
        }

        var pMonth = new NpgsqlParameter("m", NpgsqlDbType.Array | NpgsqlDbType.Date) {Value = mArr};
        var pWordId = new NpgsqlParameter("w", NpgsqlDbType.Array | NpgsqlDbType.Bigint) {Value = wArr};
        var pCnt = new NpgsqlParameter("c", NpgsqlDbType.Array | NpgsqlDbType.Bigint) {Value = cArr};

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "WordMonths" ("Month","WordId","Cnt")
            SELECT m, wid, c
            FROM unnest(@m::date[], @w::bigint[], @c::bigint[]) AS t(m, wid, c)
            ON CONFLICT ("Month","WordId")
            DO UPDATE SET "Cnt" = EXCLUDED."Cnt";
            """,
            new object[] { pMonth, pWordId, pCnt },
            ct);
    }
}
