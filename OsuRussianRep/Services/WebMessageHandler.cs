using System.Text.Json;
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
    private const int MaxMessageLength = 500;

    /// <summary>
    /// Главный обработчик сообщений из osu! web chat.
    /// </summary>
    public async Task HandleAsync(WebChatMessage msg, CancellationToken ct = default)
    {
        var usernameRaw = msg.sender?.username ?? "unknown";
        var username = NormalizeUsername(usernameRaw);
        var osuId = msg.sender_id;
        var text = msg.content?.Trim() ?? "";
        var channel = msg.channel_id.ToString();
        var timestamp = msg.timestamp;
        try
        {

            if (string.IsNullOrWhiteSpace(text))
            {
                await WriteFailedToJsonlAsync(msg, new Exception("EMPTY MESSAGE"), ct);
            }

            logger.LogDebug("WEBCHAT [{Chan}] {User}: {Text}",
                channel, username, text);

            if (text.Length <= MaxMessageLength)
                await SaveMessageToDb(osuId, username, text, timestamp, channel, ct);

        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Ошибка при обработке web-сообщения от {User}: {Msg}",
                msg.sender?.username, msg.content);
            await WriteFailedToJsonlAsync(msg, ex, ct);
        }

        try
        {
            await commands.ProcessAsync(
                username,
                text,
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Ошибка при обработке команды от {User}: {Msg}",
                msg.sender?.username, msg.content);
        }
    }
    
    private async Task WriteFailedToJsonlAsync(WebChatMessage msg, Exception ex, CancellationToken ct)
    {
        var line = JsonSerializer.Serialize(new
        {
            error = ex.Message,
            sender = msg.sender?.username,
            sender_id = msg.sender_id,
            content = msg.content,
            channel = msg.channel_id,
            timestamp = msg.timestamp,
            created = DateTime.UtcNow
        });

        var path = Path.Combine(AppContext.BaseDirectory, "data/webchat_failed.jsonl");

        await File.AppendAllTextAsync(path, line + Environment.NewLine, ct);
    }

    
    private static string NormalizeUsername(string raw)
        => raw.Replace(' ', '_');

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

        await using var tx = await db.Database.BeginTransactionAsync(ct);

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
                MessagesCount = 1,
                OldNicknames = new List<ChatUserNickHistory>()
            };

            db.ChatUsers.Add(user);
        }
        else
        {
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
        await tx.CommitAsync(ct);
    }

}