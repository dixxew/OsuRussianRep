using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;

namespace OsuRussianRep.Services;

public interface IUserWordStatsService
{
    Task<IReadOnlyList<TopWordDto>> GetTopWordsForUser(Guid userId, int limit, CancellationToken ct);
    Task<IReadOnlyList<(string Lemma, long Count)>> GetUsersForWord(string lemma, int limit, CancellationToken ct);
}

public sealed class UserWordStatsService(AppDbContext db) : IUserWordStatsService
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
    
    public async Task<IReadOnlyList<TopWordDto>> GetTopWordsForUser(Guid userId, int limit, CancellationToken ct)
    {
        var capped = Math.Clamp(limit, 1, 500);

        var q = from wu in db.WordUsers.AsNoTracking()
            where wu.UserId == userId
            join w in db.Words.AsNoTracking() on wu.WordId equals w.Id
            where !StopWords.Contains(w.Lemma) // 🧹 фильтруем мусор
            orderby wu.Cnt descending
            select new TopWordDto(w.Lemma, wu.Cnt);

        return await q.Take(capped).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<(string Lemma, long Count)>> GetUsersForWord(string lemma, int limit, CancellationToken ct)
    {
        var capped = Math.Clamp(limit, 1, 500);

        var wid = await db.Words.AsNoTracking()
            .Where(x => x.Lemma == lemma.ToLowerInvariant())
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (wid is null) return [];

        var q = from wu in db.WordUsers.AsNoTracking()
            where wu.WordId == wid
            join u in db.ChatUsers.AsNoTracking() on wu.UserId equals u.Id
            orderby wu.Cnt descending
            select new ValueTuple<string, long>(u.Nickname, wu.Cnt);

        return await q.Take(capped).ToListAsync(ct);
    }
}