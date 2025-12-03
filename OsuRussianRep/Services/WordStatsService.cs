using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;
using OsuRussianRep.Helpers;
using OsuRussianRep.Models.ChatStatics;

namespace OsuRussianRep.Services;

public interface IWordStatsService
{
    Task<IReadOnlyList<TopWordDto>> GetTopWords(DateOnly from, DateOnly to, int limit, CancellationToken ct);

    Task<IReadOnlyList<WordSeriesPointDto>> GetWordTimeseries(string word, DateOnly from, DateOnly to,
        CancellationToken ct);

    Task<IReadOnlyList<string>> SuggestWords(string query, int limit, CancellationToken ct);
    Task IncrementWordScore(string targetWord, string senderNickname, CancellationToken ct);
}

public sealed class WordStatsService(AppDbContext db, IStopWordsProvider stopWordsProvider) : IWordStatsService
{
    private static bool UseMonths(DateOnly from, DateOnly to)
    {
        return (to.DayNumber - from.DayNumber) > 90;
    }

    public async Task<IReadOnlyList<TopWordDto>> GetTopWords(
    DateOnly from, DateOnly to, int limit, CancellationToken ct)
{
    var capped = Math.Clamp(limit, 1, 500);
    var stops = stopWordsProvider.All.ToArray();
    var useMonths = UseMonths(from, to);

    if (!useMonths)
    {
        var q =
            db.WordsInDay
                .AsNoTracking()
                .Where(wd => wd.Day >= from && wd.Day < to
                             && !stops.Contains(wd.Word.Lemma))
                .GroupBy(wd => wd.WordId)
                .Select(g => new
                {
                    WordId = g.Key,
                    Cnt = g.Sum(x => x.Cnt)
                })
                .Join(db.Words.AsNoTracking(),
                    agg => agg.WordId,
                    w => w.Id,
                    (agg, w) => new
                    {
                        w.Lemma,
                        agg.Cnt,
                        w.WordScore
                    })
                .OrderByDescending(x => x.WordScore)
                .ThenByDescending(x => x.Cnt)
                .Take(capped)
                .Select(x => new TopWordDto(x.Lemma, x.Cnt, x.WordScore));

        return await q.ToListAsync(ct);
    }
    else
    {
        var monthFrom = FirstMonth(from);
        var monthTo = FirstMonth(to);

        var q =
            db.WordsInMonth
                .AsNoTracking()
                .Where(wm => wm.Month >= monthFrom && wm.Month < monthTo
                             && !stops.Contains(wm.Word.Lemma))
                .GroupBy(wm => wm.WordId)
                .Select(g => new
                {
                    WordId = g.Key,
                    Cnt = g.Sum(x => x.Cnt)
                })
                .Join(db.Words.AsNoTracking(),
                    agg => agg.WordId,
                    w => w.Id,
                    (agg, w) => new
                    {
                        w.Lemma,
                        agg.Cnt,
                        w.WordScore
                    })
                .OrderByDescending(x => x.WordScore)
                .ThenByDescending(x => x.Cnt)
                .Take(capped)
                .Select(x => new TopWordDto(x.Lemma, x.Cnt, x.WordScore));

        return await q.ToListAsync(ct);
    }
}


    public async Task<IReadOnlyList<WordSeriesPointDto>> GetWordTimeseries(
        string word, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(word)) return Array.Empty<WordSeriesPointDto>();
        var lemma = word.ToLowerInvariant();

        var wid = await db.Words.AsNoTracking()
            .Where(x => x.Lemma == lemma)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (wid is null) return Array.Empty<WordSeriesPointDto>();

        var useMonths = UseMonths(from, to);

        if (!useMonths)
        {
            return await db.WordsInDay.AsNoTracking()
                .Where(x => x.WordId == wid && x.Day >= from && x.Day < to)
                .OrderBy(x => x.Day)
                .Select(x => new WordSeriesPointDto(x.Day, x.Cnt))
                .ToListAsync(ct);
        }
        else
        {
            return await db.WordsInMonth.AsNoTracking()
                .Where(x => x.WordId == wid
                            && x.Month >= FirstMonth(from)
                            && x.Month < FirstMonth(to))
                .OrderBy(x => x.Month)
                .Select(x => new WordSeriesPointDto(x.Month, x.Cnt))
                .ToListAsync(ct);
        }
    }


    private static DateOnly FirstMonth(DateOnly day)
        => new DateOnly(day.Year, day.Month, 1);


    public async Task<IReadOnlyList<string>> SuggestWords(string query, int limit, CancellationToken ct)
    {
        // сюда месячная логика не лезет — оставляем как было
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<string>();
        var prefix = query.ToLowerInvariant();
        var capped = Math.Clamp(limit, 1, 50);

        var q = db.Words.AsNoTracking()
            .Where(w => EF.Functions.ILike(w.Lemma, prefix + "%"))
            .Select(w => new
            {
                w.Lemma,
                Sum = db.WordsInDay.Where(d => d.WordId == w.Id).Sum(d => (long?)d.Cnt) ?? 0
            })
            .OrderByDescending(x => x.Sum)
            .ThenBy(x => x.Lemma)
            .Select(x => x.Lemma);

        return await q.Take(capped).ToListAsync(ct);
    }


    public async Task IncrementWordScore(string targetWord, string senderNickname, CancellationToken ct)
    {
        // untouched
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var user = await db.ChatUsers
            .FirstOrDefaultAsync(u => u.Nickname == senderNickname, ct);

        if (user is null)
            return;

        if (DateTime.UtcNow - user.LastUsedAddRep < TimeSpan.FromDays(1))
            return;

        var word = await db.Words
            .FirstOrDefaultAsync(w => w.Lemma == targetWord, ct);

        if (word == null)
        {
            word = new Word { Lemma = targetWord };
            db.Words.Add(word);
        }

        user.LastRepTime = DateTime.UtcNow;
        word.WordScore += 1;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}