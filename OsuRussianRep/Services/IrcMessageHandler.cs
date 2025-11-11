using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Models;

namespace OsuRussianRep.Services;

public sealed class IrcMessageHandler(
    ReputationService reputationService,
    OsuService osuService,
    IServiceScopeFactory scopeFactory,
    ILogger<IrcMessageHandler> logger)
{
    // +rep-триггеры: "+rep", "rep", "реп", "+реп", "репорт", "voteban"
    private static readonly Regex PlusCmd = new(
        @"^\s*(\+?rep|\+?реп|репорт|voteban)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // -rep-триггеры: "-rep", "-реп"
    private static readonly Regex MinusCmd = new(
        @"^\s*(-rep|-реп)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const int MaxMessageLength = 200;
    private const string Boss = "dixxew";

    // дергай это из IIrcService.ChannelMessageReceived
    public async Task HandleChannelMessageAsync(string channel, string nickname, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            logger.LogDebug("IRC [{Channel}] {Nick}: {Message}", channel, nickname, message);

            if (message.Length <= MaxMessageLength)
                await AddUserMessageSafe(nickname, message, channel, ct);

            if (string.Equals(nickname, Boss, StringComparison.OrdinalIgnoreCase))
                BossCommand(message);

            if (TryParsePlus(message, out var targetPlus))
                await ProcessPlusAsync(nickname, targetPlus, ct);

            if (TryParseMinus(message, out var targetMinus))
                await ProcessMinusAsync(nickname, targetMinus, ct);
        }
        catch (OperationCanceledException) { /* игнор */ }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке IRC-сообщения от {Nick}: {Message}", nickname, message);
        }
    }

    // дергай это из IIrcService.PrivateMessageReceived (если надо)
    public Task HandlePrivateMessageAsync(string nickname, string message, CancellationToken ct = default)
    {
        logger.LogInformation("PM от {Nick}: {Message}", nickname, message);
        return Task.CompletedTask;
    }

    private static bool TryParsePlus(string msg, out string target)
    {
        var m = PlusCmd.Match(msg);
        target = m.Success ? m.Groups[2].Value.Trim() : string.Empty;
        return m.Success && target.Length > 0;
    }

    private static bool TryParseMinus(string msg, out string target)
    {
        var m = MinusCmd.Match(msg);
        target = m.Success ? m.Groups[2].Value.Trim() : string.Empty;
        return m.Success && target.Length > 0;
    }

    private async Task ProcessPlusAsync(string from, string target, CancellationToken ct)
    {
        if (SameUser(from, target) && !IsBoss(from)) return;

        if (!await osuService.CheckUserExists(target, ct))
        {
            logger.LogWarning("Цель +rep не найдена: {Target}", target);
            return;
        }

        logger.LogInformation("{Nick} выдал +rep {Target}", from, target);
        await reputationService.AddReputationAsync(target, from, ct);
    }

    private async Task ProcessMinusAsync(string from, string target, CancellationToken ct)
    {
        if (SameUser(from, target)) return;

        if (!await osuService.CheckUserExists(target, ct))
        {
            logger.LogWarning("Цель -rep не найдена: {Target}", target);
            return;
        }

        logger.LogInformation("{Nick} выдал -rep {Target}", from, target);
        await reputationService.RemoveReputationAsync(target, from, ct);
    }

    private static bool SameUser(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool IsBoss(string n)
        => string.Equals(n, Boss, StringComparison.OrdinalIgnoreCase);

    private void BossCommand(string message)
    {
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return;

        var cmd = parts[0];
        var arg = parts[1];

        logger.LogDebug("Неизвестная команда: {Command}", cmd);
    }

    private async Task AddUserMessageSafe(string nickname, string message, string channel, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // один запрос на юзера
        var user = await db.ChatUsers.FirstOrDefaultAsync(u => u.Nickname == nickname, ct);
        if (user is null)
        {
            user = new ChatUser { Nickname = nickname, Reputation = 0 };
            db.ChatUsers.Add(user);
        }

        // одна SaveChanges на всё
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),  
            ChatChannel = channel,
            Text = message,
            User = user,
            Date = DateTime.UtcNow  
        });

        await db.SaveChangesAsync(ct);
    }
}
