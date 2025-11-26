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
    public async Task<IReadOnlyList<TopWordDto>> GetTopWords(
        DateOnly from, DateOnly to, int limit, CancellationToken ct)
    {
        var capped = Math.Clamp(limit, 1, 500);
        var stops = stopWordsProvider.All.ToArray();
        var candidateWords =
            db.Words
                .Where(w => !stops.Contains(w.Lemma))
                .OrderByDescending(w => w.WordScore)
                .Take(capped * 5)
                .Select(w => new { w.Id, w.Lemma, w.WordScore });

        var query =
            db.WordsInDay
                .Where(wd => wd.Day >= from && wd.Day < to)
                .Join(
                    candidateWords,
                    wd => wd.WordId,
                    cw => cw.Id,
                    (wd, cw) => new { wd.Cnt, wd.Day, cw.Id, cw.Lemma, cw.WordScore }
                )
                .GroupBy(x => new { x.Id, x.Lemma, x.WordScore })
                .Select(g => new TopWordDto(
                    g.Key.Lemma,
                    g.Sum(x => x.Cnt),
                    g.Key.WordScore
                ));

        return await query
            .OrderByDescending(x => x.Rate)
            .ThenByDescending(x => x.Cnt)
            .Take(capped)
            .ToListAsync(ct);
    }


    public async Task<IReadOnlyList<WordSeriesPointDto>> GetWordTimeseries(string word, DateOnly from, DateOnly to,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(word)) return Array.Empty<WordSeriesPointDto>();
        var lemma = word.ToLowerInvariant();

        var wid = await db.Words.AsNoTracking()
            .Where(x => x.Lemma == lemma)
            .Select(x => (long?) x.Id)
            .FirstOrDefaultAsync(ct);

        if (wid is null) return Array.Empty<WordSeriesPointDto>();

        var q = db.WordsInDay.AsNoTracking()
            .Where(x => x.WordId == wid && x.Day >= from && x.Day < to)
            .OrderBy(x => x.Day)
            .Select(x => new WordSeriesPointDto(x.Day, x.Cnt));

        return await q.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> SuggestWords(string query, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<string>();
        var prefix = query.ToLowerInvariant();
        var capped = Math.Clamp(limit, 1, 50);

        // сортируем по суммарной популярности (за всё время), потом лексикографически
        var q = db.Words.AsNoTracking()
            .Where(w => EF.Functions.ILike(w.Lemma, prefix + "%"))
            .Select(w => new
            {
                w.Lemma,
                Sum = db.WordsInDay.Where(d => d.WordId == w.Id).Sum(d => (long?) d.Cnt) ?? 0
            })
            .OrderByDescending(x => x.Sum)
            .ThenBy(x => x.Lemma)
            .Select(x => x.Lemma);

        return await q.Take(capped).ToListAsync(ct);
    }

    public async Task IncrementWordScore(string targetWord, string senderNickname, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var user = await db.ChatUsers
            .FirstOrDefaultAsync(u => u.Nickname == senderNickname, ct);

        if (user is null)
            return;

        if (DateTime.UtcNow - user.LastUsedAddRep < new TimeSpan(1, 0, 0, 0))
            return;

        var word = await db.Words
            .FirstOrDefaultAsync(w => w.Lemma == targetWord, ct);

        if (word == null)
        {
            word = new Word {Lemma = targetWord};
            db.Words.Add(word);
        }

        user.LastRepTime = DateTime.UtcNow;
        word.WordScore += 1;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}