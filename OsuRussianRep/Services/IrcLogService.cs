using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Text.Json;
using OsuRussianRep.Models;
using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Interfaces;

namespace OsuRussianRep.Services;

/// <summary>
/// Сервис фоновой обработки IRC-логов с WAL и кэшем WHOIS.
/// </summary>
public sealed class IrcLogService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IIrcService _irc;
    private readonly ILogger<IrcLogService> _logger;

    private Timer _timer;
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(200);
    private bool _tickRunning = false;

    /// <summary>
    /// Кэш WHOIS по никам.
    /// </summary>
    private readonly ConcurrentDictionary<string, WhoisInfo> _whois = new(StringComparer.OrdinalIgnoreCase);

// когда в последний раз запрашивали WHOIS по нику
    private readonly ConcurrentDictionary<string, DateTime> _whoisRequested = new(StringComparer.OrdinalIgnoreCase);

    private readonly TimeSpan _whoisRequestCooldown = TimeSpan.FromSeconds(20);
    private readonly TimeSpan _whoisTtl = TimeSpan.FromMinutes(30);

    private const string PendingFile = "data/pending.jsonl";
    private const string PendingProcessingFile = "data/pending.jsonl.processing";
    private const string WhoisFile = "data/whois.jsonl";

    private readonly object _fileLock = new();
    private readonly object _whoisFileLock = new();

    public IrcLogService(IServiceScopeFactory scopeFactory, IIrcService irc, ILogger<IrcLogService> logger)
    {
        _scopeFactory = scopeFactory;
        _irc = irc;
        _logger = logger;

        _irc.WhoisMessageReceived += OnWhois;

        WhoisRestore();

        // запускаем таймер
        _timer = new Timer(Tick, null, _interval, _interval);
    }

    private async void Tick(object? _)
    {
        if (_tickRunning) return; // защита от реентрантности
        _tickRunning = true;

        try
        {
            WalStartProcessing();

            var batch = LoadFromWalBatch();
            if (batch.Count > 0)
            {
                var failed = await ProcessBatchAsync(batch, CancellationToken.None);
                RewriteWal(failed);
            }

            WalCommit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IrcLogService tick failure");
        }
        finally
        {
            _tickRunning = false;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _irc.WhoisMessageReceived -= OnWhois;
    }


    #region WAL

    /// <summary>
    /// Добавляет запись в WAL файл.
    /// </summary>
    private void WalAppend(PendingIrcMessage msg)
    {
        var line = JsonSerializer.Serialize(msg) + "\n";

        lock (_fileLock)
        {
            File.AppendAllText(PendingFile, line);
        }
    }


    /// <summary>
    /// Начинает обработку WAL.
    /// </summary>
    /// <remarks>
    /// Переименовыват PendingFile в PendingProcessingFile
    /// </remarks>
    private void WalStartProcessing()
    {
        lock (_fileLock)
        {
            if (File.Exists(PendingProcessingFile))
            {
                return;
            }

            if (File.Exists(PendingFile))
            {
                File.Move(PendingFile, PendingProcessingFile, overwrite: true);
            }
        }
    }

    /// <summary>
    /// Коммит обработки WAL.
    /// </summary>
    private void WalCommit()
    {
        lock (_fileLock)
        {
            if (File.Exists(PendingProcessingFile))
            {
                File.Delete(PendingProcessingFile);
            }
        }
    }

    private List<PendingIrcMessage> LoadFromWalBatch()
    {
        var list = new List<PendingIrcMessage>();

        if (!File.Exists(PendingProcessingFile))
            return list;

        foreach (var line in File.ReadLines(PendingProcessingFile))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var msg = JsonSerializer.Deserialize<PendingIrcMessage>(line);
            if (msg != null)
                list.Add(msg);
        }

        return list;
    }


    private void RewriteWal(List<PendingIrcMessage> failed)
    {
        lock (_fileLock)
        {
            foreach (var f in failed)
                WalAppend(f);
        }
    }

    #endregion

    #region Whois

    /// <summary>
    /// Сохранить whois.
    /// </summary>
    private void PersistWhois()
    {
        lock (_whoisFileLock)
        {
            var json = JsonSerializer.Serialize(_whois);
            File.WriteAllText(WhoisFile, json);
        }
    }


    /// <summary>
    /// Обработчик WHOIS.
    /// </summary>
    private void OnWhois(object? s, IrcWhoisMessageEventArgs e)
    {
        _logger.LogDebug("WHOIS received for {Nick}: {Url}", e.Nick, e.ProfileUrl);
        long? osuId = null;
        var url = e.ProfileUrl;
        var last = url.LastIndexOf('/') + 1;
        if (last > 0 && long.TryParse(url[last..], out var parsed))
            osuId = parsed;

        var info = new WhoisInfo
        {
            OsuUserId = osuId,
            ProfileUrl = url,
            Updated = DateTime.UtcNow,
            Nick = e.Nick
        };

        _whois[e.Nick] = info;
        PersistWhois();
    }

    /// <summary>
    /// Восстанавливает whois из файла.
    /// </summary>
    private void WhoisRestore()
    {
        if (!File.Exists(WhoisFile))
            return;

        var json = File.ReadAllText(WhoisFile);
        var dict = JsonSerializer.Deserialize<Dictionary<string, WhoisInfo>>(json);

        if (dict == null)
            return;

        foreach (var kv in dict)
            if (!kv.Value.Expired(_whoisTtl))
                _whois[kv.Key] = kv.Value;
    }

    #endregion

    /// <summary>
    /// Добавляет сообщение IRC в очередь и WAL.
    /// </summary>
    public void EnqueueMessage(string channel, string nick, string text, DateTime dateUtc)
    {
        var msg = new PendingIrcMessage
        {
            Channel = channel,
            Nick = nick,
            Text = text,
            DateUtc = dateUtc
        };

        WalAppend(msg);
    }

    private async Task<List<PendingIrcMessage>> ProcessBatchAsync(List<PendingIrcMessage> batch, CancellationToken ct)
    {
        var failed = new List<PendingIrcMessage>();

        foreach (var msg in batch)
        {
            try
            {
                var ok = await ProcessAsync(msg, ct);

                if (!ok)
                    failed.Add(msg);
            }
            catch (Exception ex)
            {
                failed.Add(msg);
            }
        }

        return failed;
    }


    /// <summary>
    /// Обрабатывает сообщение IRC и сохраняет в БД.
    /// </summary>
    private async Task<bool> ProcessAsync(PendingIrcMessage msg, CancellationToken ct)
    {
        if (!_whois.TryGetValue(msg.Nick, out var info))
        {
            if (!_whoisRequested.TryGetValue(msg.Nick, out var lastReq) ||
                DateTime.UtcNow - lastReq > _whoisRequestCooldown)
            {
                if (_irc.RequestWhois(msg.Nick))
                    _whoisRequested[msg.Nick] = DateTime.UtcNow;
            }

            await Task.Delay(10, ct);
            return false;
        }

        if (info.Expired(_whoisTtl))
        {
            if (!_whoisRequested.TryGetValue(msg.Nick, out var lastReq) ||
                DateTime.UtcNow - lastReq > _whoisRequestCooldown)
            {
                if (_irc.RequestWhois(msg.Nick))
                    _whoisRequested[msg.Nick] = DateTime.UtcNow;
            }

            await Task.Delay(10, ct);
            return false;
        }

        if (info.OsuUserId is null)
        {
            return false;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await db.ChatUsers.FirstOrDefaultAsync(u => u.OsuUserId == info.OsuUserId, ct);
            if (user == null)
            {
                user = new ChatUser
                {
                    Id = Guid.NewGuid(),
                    Nickname = msg.Nick,
                    OsuUserId = info.OsuUserId,
                    OsuProfileUrl = info.ProfileUrl,
                    LastMessageDate = msg.DateUtc
                };
                db.ChatUsers.Add(user);
            }
            else
            {
                if (!string.Equals(user.Nickname, msg.Nick, StringComparison.OrdinalIgnoreCase))
                {
                    db.ChatUserNickHistories.Add(new ChatUserNickHistory
                    {
                        ChatUserId = user.Id,
                        Nickname = user.Nickname
                    });
                    user.Nickname = msg.Nick;
                }

                user.LastMessageDate = DateTime.UtcNow;
            }

            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ChatChannel = msg.Channel,
                Text = msg.Text,
                Date = msg.DateUtc
            });

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }

        return true;
    }
}

/// <summary>
/// Сообщение, ожидающее обработки IRC.
/// </summary>
public sealed class PendingIrcMessage
{
    /// <summary>
    /// Канал IRC.
    /// </summary>
    public string Channel { get; set; }


    /// <summary>
    /// Ник отправителя.
    /// </summary>
    public string Nick { get; set; }


    /// <summary>
    /// Текст сообщения.
    /// </summary>
    public string Text { get; set; }


    /// <summary>
    /// Время сообщения (UTC).
    /// </summary>
    public DateTime DateUtc { get; set; }
}

/// <summary>
/// Информация WHOIS по пользователю.
/// </summary>
public sealed class WhoisInfo
{
    /// <summary>
    /// osu! user id.
    /// </summary>
    public long? OsuUserId { get; set; }


    /// <summary>
    /// URL профиля.
    /// </summary>
    public string? ProfileUrl { get; set; }


    /// <summary>
    /// Дата обновления WHOIS.
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>
    /// Ник пользователя.
    /// </summary>
    public string Nick { get; set; }


    /// <summary>
    /// Проверяет, устарела ли запись.
    /// </summary>
    public bool Expired(TimeSpan ttl) => DateTime.UtcNow - Updated > ttl;
}