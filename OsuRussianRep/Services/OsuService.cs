using Microsoft.Extensions.Caching.Memory;
using OsuSharp.Domain;
using OsuSharp.Interfaces;

namespace OsuRussianRep.Services;

public class OsuService(IOsuClient client, IMemoryCache cache, ILogger<OsuService> logger)
{
    public async IAsyncEnumerable<IBeatmapset> GetLastRankedBeatmapsetsAsync(int count)
    {
        var builder = new BeatmapsetsLookupBuilder()
            .WithGameMode(GameMode.Osu)
            .WithConvertedBeatmaps()
            .WithCategory(BeatmapsetCategory.Ranked);

        await foreach (var beatmap in client.EnumerateBeatmapsetsAsync(builder, BeatmapSorting.Ranked_Desc))
        {
            logger.LogDebug("Получена мапа: {Title} - {Creator}", beatmap.Title, beatmap.Creator);
            yield return beatmap;

            count--;
            if (count == 0)
            {
                logger.LogInformation("Достигнут лимит мап: {Count}", count);
                break;
            }
        }
    }

    public async Task<IUser?> GetUserAsync(string username, CancellationToken ct)
    {
        if (cache.TryGetValue(username, out IUser cachedUser))
        {
            logger.LogDebug("Пользователь {Username} найден в кэше", username);
            return cachedUser;
        }

        IUser? user = null;
        try
        {
            logger.LogDebug("Пробую получить юзера {Username} (osu)", username);
            user = await client.GetUserAsync(username, GameMode.Osu, token: ct);
            if (user.GameMode != GameMode.Osu)
                user = await client.GetUserAsync(username, user.GameMode, token: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось получить {Username} в режиме osu. Пробую mania...", username);
            try
            {
                user = await client.GetUserAsync(username, GameMode.Mania, ct);
            }
            catch (Exception ex2)
            {
                logger.LogWarning(ex2, "Не удалось получить {Username} в режиме mania. Отдаю null", username);
                return null;
            }
        }

        if (user != null)
        {
            logger.LogInformation("Пользователь {Username} найден. Кэшируем.", username);

            cache.Set(username, user, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3)
            });
        }

        return user;
    }

    public async Task<bool> CheckUserExists(string? username, CancellationToken ct = default)
    {
        try
        {
            logger.LogDebug("Проверка существования юзера {Username} (osu)", username);
            var user = await client.GetUserAsync(username, GameMode.Osu, ct);
            return true;
        }
        catch
        {
            logger.LogDebug("osu не дал юзера, пробую mania: {Username}", username);
            try
            {
                var user = await client.GetUserAsync(username, GameMode.Mania, ct);
                return true;
            }
            catch
            {
                logger.LogInformation("Юзер {Username} не найден ни в osu, ни в mania", username);
                return false;
            }
        }
    }
}
