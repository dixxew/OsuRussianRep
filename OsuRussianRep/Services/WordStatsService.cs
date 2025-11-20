using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;
using OsuRussianRep.Models;

namespace OsuRussianRep.Services;

public interface IWordStatsService
{
    Task<IReadOnlyList<TopWordDto>> GetTopWords(DateOnly from, DateOnly to, int limit, CancellationToken ct);
    Task<IReadOnlyList<WordSeriesPointDto>> GetWordTimeseries(string word, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<string>> SuggestWords(string query, int limit, CancellationToken ct);
}

public sealed class WordStatsService(AppDbContext db) : IWordStatsService
{
    public async Task<IReadOnlyList<TopWordDto>> GetTopWords(DateOnly from, DateOnly to, int limit, CancellationToken ct)
    {
        var capped = Math.Clamp(limit, 1, 500);

        var data = await db.WordsInDay.AsNoTracking()
            .Where(wd => wd.Day >= from && wd.Day < to)
            .GroupBy(wd => wd.WordId)
            .Select(g => new { WordId = g.Key, Cnt = g.Sum(x => x.Cnt) })
            .OrderByDescending(x => x.Cnt)
            .Take(capped)
            .Join(db.Words.AsNoTracking(),
                agg => agg.WordId,
                w => w.Id,
                (agg, w) => new TopWordDto(w.Lemma, agg.Cnt))
            .ToListAsync(ct);

        return data;
    }


    public async Task<IReadOnlyList<WordSeriesPointDto>> GetWordTimeseries(string word, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(word)) return Array.Empty<WordSeriesPointDto>();
        var lemma = word.ToLowerInvariant();

        var wid = await db.Words.AsNoTracking()
            .Where(x => x.Lemma == lemma)
            .Select(x => (long?)x.Id)
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
            .Select(w => new {
                w.Lemma,
                Sum = db.WordsInDay.Where(d => d.WordId == w.Id).Sum(d => (long?)d.Cnt) ?? 0
            })
            .OrderByDescending(x => x.Sum)
            .ThenBy(x => x.Lemma)
            .Select(x => x.Lemma);

        return await q.Take(capped).ToListAsync(ct);
    }

	internal async Task IncrementWordStat(string targetWord, string senderNickname, CancellationToken ct)
	{
        /*
        var context = db;

		var senderUser = await context.ChatUsers
			.FirstAsync(u => u.Nickname == senderNickname, cancellationToken: ct);

		var targetWord = await context.ChatUsers
			.FirstOrDefaultAsync(u => u.Nickname == targetNickname, cancellationToken: ct);

		if (senderNickname != "dixxew")
			if (senderUser.LastUsedAddRep != null)
				if (DateTime.Now - senderUser.LastUsedAddRep > new TimeSpan(1, 0, 0))
					senderUser.LastUsedAddRep = DateTime.UtcNow;
				else return;
			else
				senderUser.LastUsedAddRep = DateTime.UtcNow;

		if (targetUser == null)
		{
			targetUser = new ChatUser
			{
				Nickname = targetNickname,
				Reputation = 1,
				LastRepNickname = senderNickname,
				LastRepTime = DateTime.UtcNow
			};

			context.ChatUsers.Add(targetUser);
		}
		else
		{
			targetUser.Reputation += 1;
			targetUser.LastRepNickname = senderNickname;
			targetUser.LastRepTime = DateTime.UtcNow;
		}

		await context.SaveChangesAsync(ct);
        */
        await db.SaveChangesAsync(ct);
	}
}
