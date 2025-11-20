using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;
using OsuRussianRep.Models;
using OsuRussianRep.Models.ChatStatics;

namespace OsuRussianRep.Services;

public interface IWordStatsService
{
    Task<IReadOnlyList<TopWordDto>> GetTopWords(DateOnly from, DateOnly to, int limit, CancellationToken ct);
    Task<IReadOnlyList<WordSeriesPointDto>> GetWordTimeseries(string word, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<string>> SuggestWords(string query, int limit, CancellationToken ct);
}

public sealed class WordStatsService(IServiceScopeFactory scopeFactory) : IWordStatsService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "я", "ты", "он", "она", "мы", "вы", "они", "как", "это", "что", "бы", "вот",  "чё",  "че", 
        "в", "во", "на", "по", "из", "и", "или", "а", "но", "же", "то", "щас", "всё", "когда", "уже", 
        "к", "с", "у", "о", "об", "от", "до", "за", "для", "под", "там", "чтобы", "чтоб", "только", "тока",    
        "osu", "pp", "https", "http", "twitch", "discord", "sh",  "ещё", "какойто", "какоето",    
        "com", "ru", "org", "net", "youtu", "youtube", "ss", "так", "да", "какието", "тоже", "его", "их",       
        "www", "ppy", "osu.ppy", "osu.ppy.sh", "мне", "ну", "меня", "live", "65535", "io",  "ul", "eu",
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "staticflickr", "jpg", "png",  "beatmapsets",
        "если", "даже", "нибудь", "ща", "кто","зачем","где","нет","еще","есть","без","s","не" 
    };
    
    public async Task<IReadOnlyList<TopWordDto>> GetTopWords(DateOnly from, DateOnly to, int limit, CancellationToken ct)
    {
		using var scope = scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

		var capped = Math.Clamp(limit, 1, 500);

        var data = await db.WordsInDay.AsNoTracking()
            .Where(wd => wd.Day >= from && wd.Day < to && !StopWords.Contains(wd.Word.Lemma))
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
		using var scope = scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
		using var scope = scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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

	internal async Task IncrementWordScore(string targetWord, string senderNickname, CancellationToken ct)
	{
		using var scope = scopeFactory.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

		Word word = await context.Words
	        .FirstAsync(w => w.Lemma == targetWord, cancellationToken: ct);

        if (word == null)
        {
            word = new Word() { Lemma = targetWord };
            context.Words.Add(word);
        }

        word.WordScore += 1;
        await context.SaveChangesAsync(ct);
	}
}
