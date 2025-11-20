using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;
using OsuRussianRep.Helpers;

namespace OsuRussianRep.Services;

public interface IUserWordStatsService
{
    Task<IReadOnlyList<TopWordDto>> GetTopWordsForUser(Guid userId, int limit, CancellationToken ct);
    Task<IReadOnlyList<(string Lemma, long Count)>> GetUsersForWord(string lemma, int limit, CancellationToken ct);
}

public sealed class UserWordStatsService(AppDbContext db, IStopWordsProvider stopWordsProvider) : IUserWordStatsService
{
    
    public async Task<IReadOnlyList<TopWordDto>> GetTopWordsForUser(Guid userId, int limit, CancellationToken ct)
    {
        var capped = Math.Clamp(limit, 1, 500);

        var q = from wu in db.WordUsers.AsNoTracking()
            where wu.UserId == userId
            join w in db.Words.AsNoTracking() on wu.WordId equals w.Id
            where !stopWordsProvider.Contains(w.Lemma) // 🧹 фильтруем мусор
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