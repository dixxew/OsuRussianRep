using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Models;

namespace OsuRussianRep.Services;

/// <summary>
/// Обработчик IRC-сообщений: rep-команды, логирование, служебные реакции.
/// </summary>
public sealed class IrcMessageHandler(
    IIrcLogEnqueuer ircLogEnqueuer,
    IServiceScopeFactory scopeFactory,
    ILogger<IrcMessageHandler> logger)
{
    private static readonly Regex PlusCmd = new(
        @"^\s*(\+?rep|\+?реп|репорт|voteban)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);


    private static readonly Regex MinusCmd = new(
        @"^\s*(-?rep|-?реп)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RateWordCmd = new(
        @"^\s*(рейт|rate)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const int MaxMessageLength = 200;

    /// <summary>
    /// BOSS OF WHOLE SYSTEM
    /// </summary>
    private const string Boss = "dixxew";


    /// <summary>
    /// Обработка сообщений из каналов IRC.
    /// </summary>
    public async Task HandleChannelMessageAsync(string channel, string nickname, string message,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            logger.LogDebug("IRC [{Channel}] {Nick}: {Message}", channel, nickname, message);

            if (message.Length <= MaxMessageLength)
                ircLogEnqueuer.EnqueueMessage(channel, nickname, message, DateTime.UtcNow);

            if (string.Equals(nickname, Boss, StringComparison.OrdinalIgnoreCase))
                BossCommand(message);

            if (TryParsePlus(message, out var targetPlus))
                await ProcessPlusAsync(nickname, targetPlus, ct);

            if (TryParseMinus(message, out var targetMinus))
                await ProcessMinusAsync(nickname, targetMinus, ct);

            if (TryParseRateWord(message, out var targetWord))
                await ProcessRateWord(nickname, targetWord, ct);
        }
        catch (OperationCanceledException)
        {
            // ingored
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
    /// Парсинг +rep.
    /// </summary>
    private static bool TryParsePlus(string msg, out string target)
    {
        var m = PlusCmd.Match(msg);
        target = m.Success ? m.Groups[2].Value.Trim() : string.Empty;
        return m.Success && target.Length > 0;
    }

    /// <summary>
    /// Парсинг -rep.
    /// </summary>
    private static bool TryParseMinus(string msg, out string target)
    {
        var m = MinusCmd.Match(msg);
        target = m.Success ? m.Groups[2].Value.Trim() : string.Empty;
        return m.Success && target.Length > 0;
    }

    /// <summary>
    /// Парсинг rate {word}
    /// </summary>
    private static bool TryParseRateWord(string msg, out string target)
    {
        var m = RateWordCmd.Match(msg);
        target = m.Success ? m.Groups[2].Value.Trim() : string.Empty;
        return m.Success && target.Length > 0;
    }

    /// <summary>
    /// Обработка +rep.
    /// </summary>
    private async Task ProcessPlusAsync(string from, string target, CancellationToken ct)
    {
        var osuService = scopeFactory.CreateScope().ServiceProvider.GetRequiredService<OsuService>();
        var reputationService = scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ReputationService>();

        if (SameUser(from, target) && !IsBoss(from)) return;

        if (!await osuService.CheckUserExists(target, ct))
        {
            logger.LogWarning("Цель +rep не найдена: {Target}", target);
            return;
        }

        logger.LogInformation("{Nick} выдал +rep {Target}", from, target);
        await reputationService.AddReputationAsync(target, from, ct);
    }

    /// <summary>
    /// Обработка -rep.
    /// </summary>
    private async Task ProcessMinusAsync(string from, string target, CancellationToken ct)
    {
        var osuService = scopeFactory.CreateScope().ServiceProvider.GetRequiredService<OsuService>();
        var reputationService = scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ReputationService>();

        if (SameUser(from, target)) return;

        if (!await osuService.CheckUserExists(target, ct))
        {
            logger.LogWarning("Цель -rep не найдена: {Target}", target);
            return;
        }

        logger.LogInformation("{Nick} выдал -rep {Target}", from, target);
        await reputationService.RemoveReputationAsync(target, from, ct);
    }

    /// <summary>
    /// Обработка rate-команды.
    /// </summary>
    private async Task ProcessRateWord(string from, string targetWord, CancellationToken ct)
    {
        var wordsStatsService = scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IWordStatsService>();

        logger.LogInformation("{Nick} оценил слово {Target}", from, targetWord);
        await wordsStatsService.IncrementWordScore(targetWord, from, ct);
    }

    /// <summary>
    /// Проверяет, совпадают ли ники.
    /// </summary>
    private static bool SameUser(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Проверяет, является ли отправитель боссом.
    /// </summary>
    private static bool IsBoss(string n)
        => string.Equals(n, Boss, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Обработка команд босса (заглушка).
    /// </summary>
    private void BossCommand(string message)
    {
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return;

        var cmd = parts[0];
        var arg = parts[1];

        logger.LogDebug("Неизвестная команда: {Command}", cmd);
    }

    /// <summary>
    /// Обработка WHOIS.
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
                OsuUserId = osuId
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