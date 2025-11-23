using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;
using OsuRussianRep.Models;

namespace OsuRussianRep.Services;

public class MessageService(AppDbContext db, ILogger<MessageService> logger)
{
    public async Task AddUserMessageAsync(string nickname, string message, string channel)
    {
        var user = await db.ChatUsers
            .FirstOrDefaultAsync(u => u.Nickname == nickname);

        if (user == null)
        {
            user = new ChatUser
            {
                Nickname = nickname,
                Reputation = 0
            };

            await db.ChatUsers.AddAsync(user);
            logger.LogInformation("Добавлен новый пользователь: {Nickname}", nickname);
        }

        await db.Messages.AddAsync(new Message
        {
            ChatChannel = channel,
            Text = message,
            UserId = user.Id
        });

        await db.SaveChangesAsync();

        logger.LogDebug("Сохранено сообщение от {Nickname} в канал {Channel}", nickname, channel);
    }

    public List<ChannelDailyMessageCount> GetMessageCountsForLast30Days()
    {
        var startDate = DateTime.UtcNow.AddDays(-30).Date;
        var endDate = DateTime.UtcNow.Date.AddDays(1);

        var messageCounts = db.Messages
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
        var startDate = DateTime.UtcNow.AddDays(-30).Date;
        var endDate = DateTime.UtcNow.Date.AddDays(1);

        var messageCounts = db.Messages
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
    
    public async Task<LastMessagesDto> GetLastMessagesAsync(int offset, int limit)
    {
        if (limit <= 0) limit = 50;
        if (offset < 0) offset = 0;

        // Считаем всего записей
        var total = await db.Messages.CountAsync();

        // Чтобы offset работал, сортируем по возрастанию,
        // но выбираем самые последние total - offset - limit… ну ты понял.
        var msgs = await db.Messages
            .Include(m => m.User)
            .OrderByDescending(m => m.Date)
            .Skip(offset)
            .Take(limit)
            .Select(m => new MessageDto
            {
                Nickname = m.User.Nickname,
                Text = m.Text,
                Date = m.Date,
                ChatChannel = m.ChatChannel,
                UserId = m.UserId,
                UserOsuId = m.User.OsuUserId
            })
            .ToListAsync();

        // Клиенту удобнее отдавать с конца
        msgs.Reverse();

        return new LastMessagesDto
        {
            Offset = offset + msgs.Count,
            Limit = limit,
            Messages = msgs
        };
    }


}
