using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;
using OsuRussianRep.Models;
using OsuRussianRep.Services;

public sealed class UserOsuSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<UserOsuSyncBackgroundService> logger)
    : BackgroundService
{
    private const int BatchSize = 50;     // сколько юзеров за цикл
    private const int DelaySeconds = 20;  // задержка между сканами
    private const int MaxDegreeOfParallelism = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("UserOsuSyncBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatch(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "UserOsuSync job crashed");
            }

            await Task.Delay(TimeSpan.FromSeconds(DelaySeconds), stoppingToken);
        }

        logger.LogInformation("UserOsuSyncBackgroundService stopped");
    }

    private async Task ProcessBatch(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<OsuUserCache>();

        // забираем юзеров без osu данных
        var users = await db.ChatUsers
            .Where(u => u.OsuUserId == null || u.OsuProfileUrl == null)
            .OrderByDescending(u => u.Messages.Count)
            .Take(BatchSize)
            .Include(u => u.OldNicknames)
            .ToListAsync(ct);

        if (users.Count == 0)
            return;

        var sem = new SemaphoreSlim(MaxDegreeOfParallelism);

        var tasks = users.Select(async user =>
        {
            await sem.WaitAsync(ct);
            try
            {
                CachedOsuUserDto? osu = null;

                try
                {
                    // кэш сам шарит, есть данные или надо в API
                    osu = await cache.GetUserAsync(user.Nickname, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to fetch osu for {Nick}", user.Nickname);
                }

                if (osu == null)
                    return;

                bool changed = false;

                if (user.OsuUserId == null)
                {
                    user.OsuUserId = osu.Id;
                    changed = true;
                }

                if (string.IsNullOrEmpty(user.OsuProfileUrl))
                {
                    user.OsuProfileUrl = $"https://osu.ppy.sh/u/{osu.Id}";
                    changed = true;
                }

                if (osu.PreviousUsernames?.Count > 0)
                {
                    var existing = user.OldNicknames
                        .Select(x => x.Nickname)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var fresh = osu.PreviousUsernames
                        .Where(x => !existing.Contains(x));

                    foreach (var nick in fresh)
                    {
                        user.OldNicknames.Add(new ChatUserNickHistory
                        {
                            ChatUserId = user.Id,
                            Nickname = nick
                        });
                        changed = true;
                    }
                }

                if (changed)
                {
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Synced osu for {Nick}", user.Nickname);
                }
            }
            finally
            {
                sem.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
