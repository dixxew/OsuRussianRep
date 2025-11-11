using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Models;

namespace OsuRussianRep.Services;

public class MessageService(IServiceScopeFactory scopeFactory, ILogger<MessageService> logger)
{
    public async Task AddUserMessageAsync(string nickname, string message, string channel)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await context.ChatUsers
            .FirstOrDefaultAsync(u => u.Nickname == nickname);

        if (user == null)
        {
            user = new ChatUser
            {
                Nickname = nickname,
                Reputation = 0
            };

            await context.ChatUsers.AddAsync(user);
            logger.LogInformation("Добавлен новый пользователь: {Nickname}", nickname);
        }

        await context.Messages.AddAsync(new Message
        {
            ChatChannel = channel,
            Text = message,
            UserId = user.Id
        });

        await context.SaveChangesAsync();

        logger.LogDebug("Сохранено сообщение от {Nickname} в канал {Channel}", nickname, channel);
    }

    public List<ChannelDailyMessageCount> GetMessageCountsForLast30Days()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var startDate = DateTime.UtcNow.AddDays(-30).Date;
        var endDate = DateTime.UtcNow.Date.AddDays(1);

        var messageCounts = context.Messages
            .Where(m => m.Date >= startDate && m.Date < endDate)
            .GroupBy(m => m.Date.Date)
            .Select(g => new ChannelDailyMessageCount
            {
                Date = g.Key,
                MessageCount = g.Count()
            })
            .ToList();

        for (var date = startDate; date < endDate; date = date.AddDays(1))
        {
            if (!messageCounts.Any(mc => mc.Date == date))
            {
                messageCounts.Add(new ChannelDailyMessageCount
                {
                    Date = date,
                    MessageCount = 0
                });
            }
        }

        var result = messageCounts.OrderBy(mc => mc.Date).ToList();
        logger.LogDebug("Получена статистика сообщений за последние 30 дней: {Count} дней", result.Count);
        return result;
    }

    public List<HourlyAverageMessageCount> GetHourlyAverageMessageCountsForLast30Days()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var startDate = DateTime.UtcNow.AddDays(-30).Date;
        var endDate = DateTime.UtcNow.Date.AddDays(1);

        var messageCounts = context.Messages
            .Where(m => m.Date >= startDate && m.Date < endDate)
            .GroupBy(m => new { m.Date.Date, m.Date.Hour })
            .Select(g => new
            {
                g.Key.Date,
                g.Key.Hour,
                MessageCount = g.Count()
            })
            .ToList();

        var hourlyAverageMessageCounts = messageCounts
            .GroupBy(m => m.Hour)
            .Select(g => new HourlyAverageMessageCount
            {
                Hour = g.Key,
                AverageMessageCount = g.Average(x => x.MessageCount)
            })
            .OrderBy(h => h.Hour)
            .ToList();

        logger.LogDebug("Получена почасовая статистика сообщений (среднее) за 30 дней");
        return hourlyAverageMessageCounts;
    }
}
