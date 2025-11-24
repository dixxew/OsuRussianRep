using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Models;
using OsuRussianRep.Dtos.OsuWebChat;

namespace OsuRussianRep.Services;

/// <summary>
/// Обработчик сообщений osu! Web Chat.
/// Логирует сообщение + обрабатывает rep-команды.
/// </summary>
public sealed class WebMessageHandler(
    IServiceScopeFactory scopeFactory,
    ChatCommandProcessor commands,
    ILogger<WebMessageHandler> logger)
{
    private const int MaxMessageLength = 500; // в вебчате лимит больше

    /// <summary>
    /// Главный обработчик сообщений из osu! web chat.
    /// </summary>
    public async Task HandleAsync(WebChatMessage msg, CancellationToken ct = default)
    {
        try
        {
            var osuId = msg.sender?.id ?? 0;
            var username = msg.sender?.username ?? "unknown";
            var text = msg.content?.Trim() ?? "";
            var channel = msg.channel_id.ToString();
            var timestamp = msg.timestamp;

            if (string.IsNullOrWhiteSpace(text))
                return;

            logger.LogDebug("WEBCHAT [{Chan}] {User}: {Text}",
                channel, username, text);

            // 1) ЛОГИРОВАНИЕ
            if (text.Length <= MaxMessageLength)
                await SaveMessageToDb(osuId, username, text, timestamp, channel, ct);

            // 2) ОБРАБОТКА КОМАНД
            await commands.ProcessAsync(
                username,
                text,
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Ошибка при обработке web-сообщения от {User}: {Msg}",
                msg.sender?.username, msg.content);
        }
    }

    /// <summary>
    /// Запись сообщения в БД.
    /// </summary>
    private async Task SaveMessageToDb(
        long osuId,
        string username,
        string message,
        DateTime timestamp,
        string channel,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.ChatUsers
            .Include(u => u.OldNicknames)
            .FirstOrDefaultAsync(u => u.OsuUserId == osuId, ct);

        if (user is null)
        {
            user = new ChatUser
            {
                Id = Guid.NewGuid(),
                Nickname = username,
                OsuUserId = osuId,
                LastMessageDate = DateTime.UtcNow,
                MessagesCount = 1
            };

            user.OldNicknames = new List<ChatUserNickHistory>();

            db.ChatUsers.Add(user);
        }
        else
        {
            // обновляем ник если изменился
            if (!string.Equals(user.Nickname, username, StringComparison.OrdinalIgnoreCase))
            {
                user.OldNicknames ??= new List<ChatUserNickHistory>();
                user.OldNicknames.Add(new ChatUserNickHistory
                {
                    ChatUserId = user.Id,
                    Nickname = user.Nickname
                });
                user.Nickname = username;
            }

            user.MessagesCount++;
            user.LastMessageDate = DateTime.UtcNow;
        }

        // Сохраняем сообщение
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            Text = message,
            Date = timestamp,
            ChatChannel = channel,
            UserId = user.Id
        };

        db.Messages.Add(msg);
        await db.SaveChangesAsync(ct);
    }
}