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
public sealed class IrcLogService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IIrcService _irc;
    private readonly ILogger<IrcLogService> _logger;

    /// <summary>
    /// Очередь сообщений для обработки.
    /// </summary>
    private readonly Channel<PendingIrcMessage> _queue = Channel.CreateBounded<PendingIrcMessage>(
        new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });


    /// <summary>
    /// Кэш WHOIS по никам.
    /// </summary>
    private readonly ConcurrentDictionary<string, WhoisInfo> _whois = new(StringComparer.OrdinalIgnoreCase);

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
    private void WalStartProcessing()
    {
        lock (_fileLock)
        {
            if (File.Exists(PendingProcessingFile))
            {
                // старый processing лежит → сервис упал → всё обработать как есть
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
                File.Delete(PendingProcessingFile);
        }
    }

    /// <summary>
    /// Откат WAL из <b>PendingProcessingFile</b> -> <b>PendingFile</b>.
    /// </summary>
    private void WalRollback()
    {
        lock (_fileLock)
        {
            if (!File.Exists(PendingProcessingFile))
                return;

            var lines = File.ReadAllText(PendingProcessingFile);
            File.AppendAllText(PendingFile, lines);

            File.Delete(PendingProcessingFile);
        }
    }

    /// <summary>
    /// Восстанавливает очередь из WALs.
    /// </summary>
    private void WalRestore()
    {
        lock (_fileLock)
        {
            if (File.Exists(PendingProcessingFile))
                ReadFileToQueue(PendingProcessingFile);

            if (File.Exists(PendingFile))
                ReadFileToQueue(PendingFile);
        }
    }

    /// <summary>
    /// Загружает файл WAL в очередь.
    /// </summary>
    private void ReadFileToQueue(string file)
    {
        foreach (var line in File.ReadLines(file))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var msg = JsonSerializer.Deserialize<PendingIrcMessage>(line);
            if (msg != null)
                _queue.Writer.TryWrite(msg);
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
            Nick = e.Nick // ← если нужно сохранять
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
        _queue.Writer.TryWrite(msg);
    }


    /// <summary>
    /// Основная фоновая логика сервиса.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        WhoisRestore();
        WalRestore();
        WalStartProcessing();

        bool ok = false;

        try
        {
            await foreach (var msg in _queue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    ok = await ProcessAsync(msg, ct);
                }
                catch (Exception ex)
                {
                    ok = false;
                    _logger.LogWarning(ex, "Ошибка обработки лога IRC");
                }
            }
        }
        catch (Exception ex)
        {
            ok = false;
            _logger.LogError(ex, "Фатальная ошибка в IrcLogService");
        }

        if (ok)
            WalCommit();
        else
            WalRollback();
    }


    /// <summary>
    /// Обрабатывает сообщение IRC и сохраняет в БД.
    /// </summary>
    private async Task<bool> ProcessAsync(PendingIrcMessage msg, CancellationToken ct)
    {
        if (!_whois.TryGetValue(msg.Nick, out var info) || info.Expired(_whoisTtl))
        {
            _irc.RequestWhois(msg.Nick);
            await Task.Delay(10, ct);
            _queue.Writer.TryWrite(msg);
            return false;
        }

        if (info.OsuUserId is null)
        {
            _logger.LogWarning("WHOIS не дал osuId для {Nick}", msg.Nick);
            return false;
        }

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