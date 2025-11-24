using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Models;

namespace OsuRussianRep.Services;

/// <summary>
/// Упрощённый обработчик IRC-сообщений: теперь только логирование.
/// Никаких команд репутации / rate.
/// </summary>
public sealed class IrcMessageHandler(
    IrcLogService ircLogService,
    IServiceScopeFactory scopeFactory,
    ILogger<IrcMessageHandler> logger)
{
    private const int MaxMessageLength = 200;

    /// <summary>
    /// Обработка сообщений из IRC-каналов.
    /// </summary>
    public async Task HandleChannelMessageAsync(
        string channel,
        string nickname,
        string message,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            logger.LogDebug("IRC [{Channel}] {Nick}: {Msg}", channel, nickname, message);
            
            using var scope = scopeFactory.CreateScope();
            var commandProcessor = scope.ServiceProvider.GetRequiredService<ChatCommandProcessor>();
            
            await commandProcessor.ProcessAsync(nickname, message, ct);

            if (message.Length <= MaxMessageLength)
                ircLogService.EnqueueMessage(channel, nickname, message, DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке IRC-сообщения от {Nick}: {Message}", nickname, message);
        }
    }

    /// <summary>
    /// Обработка приватного сообщения IRC.
    /// </summary>
    public Task HandlePrivateMessageAsync(string nickname, string message, CancellationToken ct = default)
    {
        logger.LogInformation("PM от {Nick}: {Message}", nickname, message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Обработка WHOIS для определения osuId.
    /// </summary>
    public async Task HandleWhoisAsync(string nick, string profileUrl, CancellationToken ct = default)
    {
        long? osuId = null;
        var lastSlash = profileUrl.LastIndexOf('/');
        if (lastSlash >= 0 && long.TryParse(profileUrl[(lastSlash + 1)..], out var parsed))
            osuId = parsed;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.ChatUsers
            .FirstOrDefaultAsync(u => u.Nickname == nick, ct);

        if (user is null)
        {
            user = new ChatUser
            {
                Id = Guid.NewGuid(),
                Nickname = nick,
                Reputation = 0,
                OsuProfileUrl = profileUrl,
                OsuUserId = osuId,
                LastMessageDate = DateTime.UtcNow,
                MessagesCount = 0
            };
            db.ChatUsers.Add(user);
        }
        else
        {
            user.OsuProfileUrl = profileUrl;
            user.OsuUserId = osuId;
            user.LastMessageDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
