using System.Text.RegularExpressions;

namespace OsuRussianRep.Services;

/// <summary>
/// Общий обработчик команд чата (rep, rate).
/// </summary>
public sealed class ChatCommandProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatCommandProcessor> _logger;

    private const string Boss = "dixxew"; // как в IRC

    // +rep / реп / voteban
    private static readonly Regex PlusCmd = new(
        @"^\s*(\+?rep|\+?реп|репорт|voteban)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // -rep / -реп
    private static readonly Regex MinusCmd = new(
        @"^\s*(-?rep|-?реп)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // rate {word}
    private static readonly Regex RateCmd = new(
        @"^\s*(рейт|rate)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ChatCommandProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<ChatCommandProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Универсальная обработка текста сообщения.
    /// </summary>
    public async Task ProcessAsync(string username, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            if (IsBoss(username))
                BossCommand(message);

            if (TryParsePlus(message, out var targetPlus))
                await ProcessPlusAsync(username, targetPlus, ct);

            if (TryParseMinus(message, out var targetMinus))
                await ProcessMinusAsync(username, targetMinus, ct);

            if (TryParseRate(message, out var word))
                await ProcessRateAsync(username, word, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке команды: {Message}", message);
        }
    }


    private static bool TryParsePlus(string msg, out string target)
    {
        var m = PlusCmd.Match(msg);
        target = m.Success ? m.Groups[2].Value.Trim() : "";
        return m.Success && target.Length > 0;
    }

    private static bool TryParseMinus(string msg, out string target)
    {
        var m = MinusCmd.Match(msg);
        target = m.Success ? m.Groups[2].Value.Trim() : "";
        return m.Success && target.Length > 0;
    }

    private static bool TryParseRate(string msg, out string word)
    {
        var m = RateCmd.Match(msg);
        word = m.Success ? m.Groups[2].Value.Trim() : "";
        return m.Success && word.Length > 0;
    }


    private async Task ProcessPlusAsync(string from, string targetNick, CancellationToken ct)
    {
        if (SameUser(from, targetNick) && !IsBoss(from))
            return;

        using var scope = _scopeFactory.CreateScope();
        var osuService = scope.ServiceProvider.GetRequiredService<OsuService>();
        var reputation = scope.ServiceProvider.GetRequiredService<ReputationService>();

        if (!await osuService.CheckUserExists(targetNick, ct))
        {
            _logger.LogWarning("Цель +rep не найдена: {Target}", targetNick);
            return;
        }

        _logger.LogInformation("{From} выдал +rep {Target}", from, targetNick);
        await reputation.AddReputationAsync(targetNick, from, ct);
    }

    private async Task ProcessMinusAsync(string from, string targetNick, CancellationToken ct)
    {
        if (SameUser(from, targetNick))
            return;

        using var scope = _scopeFactory.CreateScope();
        var osuService = scope.ServiceProvider.GetRequiredService<OsuService>();
        var reputation = scope.ServiceProvider.GetRequiredService<ReputationService>();

        if (!await osuService.CheckUserExists(targetNick, ct))
        {
            _logger.LogWarning("Цель -rep не найдена: {Target}", targetNick);
            return;
        }

        _logger.LogInformation("{From} выдал -rep {Target}", from, targetNick);
        await reputation.RemoveReputationAsync(targetNick, from, ct);
    }

    private async Task ProcessRateAsync(string from, string word, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var wordStats = scope.ServiceProvider.GetRequiredService<IWordStatsService>();

        _logger.LogInformation("{From} оценил слово {Word}", from, word);
        await wordStats.IncrementWordScore(word, from, ct);
    }


    private static bool SameUser(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool IsBoss(string nickname)
        => string.Equals(nickname, Boss, StringComparison.OrdinalIgnoreCase);

    private void BossCommand(string msg)
    {
        // Заглушка, как в IRC
        var parts = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return;

        var cmd = parts[0];
        _logger.LogDebug("Команда босса: {Cmd}", cmd);
    }
}
