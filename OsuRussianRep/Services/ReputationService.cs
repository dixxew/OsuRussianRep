using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Models;

namespace OsuRussianRep.Services;

public class ReputationService(IServiceScopeFactory scopeFactory)
{
    public async Task AddReputationAsync(string targetNickname, string senderNickname, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var senderUser = await context.ChatUsers
            .FirstAsync(u => u.Nickname == senderNickname, cancellationToken: ct);

        var targetUser = await context.ChatUsers
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
    }

    public async Task RemoveReputationAsync(string targetNickname, string senderNickname, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var senderUser = await context.ChatUsers
            .FirstAsync(u => u.Nickname == senderNickname, cancellationToken: ct);

        var targetUser = await context.ChatUsers
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
                Reputation = -1,
                LastRepNickname = senderNickname,
                LastRepTime = DateTime.UtcNow
            };

            context.ChatUsers.Add(targetUser);
        }
        else
        {
            targetUser.Reputation -= 1;
            targetUser.LastRepNickname = senderNickname;
            targetUser.LastRepTime = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(ct);
    }
}


