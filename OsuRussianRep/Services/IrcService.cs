using System.Text;
using Meebey.SmartIrc4net;
using OsuRussianRep.Interfaces;

namespace OsuRussianRep.Services;

public sealed class IrcService : IIrcService
{
    private readonly IrcClient _client = new();
    private readonly string _server;
    private readonly int _port;
    private readonly bool _useSsl;
    private readonly string _channel;
    private readonly string _nickname;
    private readonly string? _password;
    private readonly ILogger<IrcService> _logger;

    private readonly TimeSpan _reconnectMin = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _reconnectMax = TimeSpan.FromMinutes(1);
    private int _reconnectAttempt;
    private readonly HashSet<string> _autoJoin = new(StringComparer.OrdinalIgnoreCase);
    private TaskCompletionSource _registeredTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _listenCts;

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<IrcChannelMessageEventArgs>? ChannelMessageReceived;
    public event EventHandler<IrcPrivateMessageEventArgs>? PrivateMessageReceived;
    public event EventHandler<IrcWhoisMessageEventArgs>? WhoisMessageReceived;

    public bool IsConnected => _client.IsConnected;

    public IrcService(IConfiguration config, ILogger<IrcService> logger)
    {
        _logger = logger;

        _server = config.GetValue("IrcConnection:Server", "");
        _port = config.GetValue("IrcConnection:Port", 6667);
        _useSsl = config.GetValue("IrcConnection:UseSsl", false);
        _channel = config.GetValue("IrcConnection:Channel", "");
        _nickname = config.GetValue("IrcConnection:Nickname", "");
        _password = config.GetValue<string?>("IrcConnection:Password", null);

        _client.Encoding = Encoding.UTF8;
        _client.SendDelay = 200;
        _client.ActiveChannelSyncing = true;
        _client.AutoReconnect = false;
        _client.AutoRelogin = false;
        _client.AutoRejoin = false;

        _client.OnConnected += (_, __) =>
        {
            _logger.LogInformation("IRC connected {Server}:{Port}", _server, _port);
            Connected?.Invoke(this, EventArgs.Empty);
        };
        _client.OnDisconnected += OnDisconnectedInternal;
        _client.OnError += (_, e) => _logger.LogWarning("IRC client error: {Error}", e.ErrorMessage);
        _client.OnRegistered += OnRegistered;

        _client.OnRawMessage += (_, e) =>
        {
            if (e.Data.ReplyCode == ReplyCode.WhoIsUser)
            {
                var args = e.Data.RawMessageArray;

                var nick = args[3];
                var profileUrl = args[4];

                WhoisMessageReceived?.Invoke(this, new(nick, profileUrl));
            }
        };

        _client.OnChannelMessage += (_, e) =>
        {
            var ch = e.Data.Channel;
            var from = e.Data.Nick;
            var msg = e.Data.Message;

            ChannelMessageReceived?.Invoke(this, new(ch, from, msg));
        };
        _client.OnQueryMessage += (_, e) =>
        {
            var from = e.Data.Nick;
            var msg = e.Data.Message;
            PrivateMessageReceived?.Invoke(this, new(from, msg));
        };
        _client.OnJoin += (_, e) => { _logger.LogInformation("IRC joined {Channel}", e.Channel); };
    }

    public void RequestWhois(string msgNick)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("IRC: attempted WHOIS for {Nick}, but client is NOT connected", msgNick);
            return;
        }

        try
        {
            _client.RfcWhois(msgNick, msgNick);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IRC: failed to send WHOIS for {Nick}", msgNick);
        }
    }


    private static string NormalizeChannel(string ch)
        => string.IsNullOrWhiteSpace(ch) ? ch : (ch.StartsWith('#') ? ch : "#" + ch);

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_server) || string.IsNullOrWhiteSpace(_nickname))
        {
            _logger.LogError("IRC: проверь конфиг (Server/Nickname)");
            return;
        }

        try
        {
            _logger.LogInformation("IRC connecting to {Server}:{Port} as {Nick}", _server, _port, _nickname);

            _client.Connect(_server, _port);

            _client.Login(_nickname, _nickname, 0, _nickname, _password);

            _listenCts?.Cancel();
            _listenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var listenToken = _listenCts.Token;

            _ = Task.Run(() =>
            {
                try
                {
                    _client.Listen();
                }
                catch (Exception ex)
                {
                    if (!listenToken.IsCancellationRequested)
                        _logger.LogWarning(ex, "IRC listen stopped");
                }
            }, listenToken);

            _reconnectAttempt = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IRC connect failed");
            await ScheduleReconnectAsync(ct);
        }
    }

    public Task DisconnectAsync(string? reason = null)
    {
        try
        {
            if (_client.IsConnected)
            {
                // корректно уходим
                if (!string.IsNullOrEmpty(reason))
                    _client.RfcQuit(reason);
                else
                    _client.RfcQuit("bye");

                _client.Disconnect();
            }
        }
        catch
        {
            /* ignore */
        }
        finally
        {
            try
            {
                _listenCts?.Cancel();
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }

    public Task JoinAsync(string channel, CancellationToken ct = default)
    {
        channel = NormalizeChannel(channel);
        if (string.IsNullOrWhiteSpace(channel))
            return Task.CompletedTask;

        _autoJoin.Add(channel);

        if (_client.IsConnected)
        {
            _logger.LogInformation("IRC: joining {Channel}", channel);
            _client.RfcJoin(channel);
            return Task.CompletedTask;
        }

        _logger.LogInformation("IRC: deferred join {Channel} until Registered", channel);
        return Task.CompletedTask;
    }

    public Task PartAsync(string channel, string? reason = null, CancellationToken ct = default)
    {
        channel = NormalizeChannel(channel);
        if (string.IsNullOrWhiteSpace(channel))
            return Task.CompletedTask;

        _client.RfcPart(channel, reason ?? "leaving");
        return Task.CompletedTask;
    }

    public Task SendChannelMessageAsync(string channel, string message, CancellationToken ct = default)
    {
        channel = NormalizeChannel(channel);
        if (!string.IsNullOrWhiteSpace(channel) && !string.IsNullOrEmpty(message))
            _client.RfcPrivmsg(channel, message); // <-- правильно для сообщений в канал
        return Task.CompletedTask;
    }

    public Task SendPrivateMessageAsync(string nick, string message, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(nick) && !string.IsNullOrEmpty(message))
            _client.RfcPrivmsg(nick, message);
        return Task.CompletedTask;
    }

    private void OnRegistered(object? sender, EventArgs e)
    {
        _logger.LogInformation("IRC registered as {Nick}", _nickname);

        if (!_registeredTcs.Task.IsCompleted)
            _registeredTcs.TrySetResult();

        foreach (var ch in _autoJoin.ToArray())
        {
            _logger.LogInformation("IRC: auto-join {Channel}", ch);
            _client.RfcJoin(ch);
        }

        // если в конфиге указан канал — тоже вступим (как и раньше)
        var cfgCh = NormalizeChannel(_channel);
        if (!string.IsNullOrWhiteSpace(cfgCh))
        {
            _autoJoin.Add(cfgCh);
            _client.RfcJoin(cfgCh);
        }
    }

    private async void OnDisconnectedInternal(object? sender, EventArgs e)
    {
        _logger.LogWarning("IRC disconnected");
        Disconnected?.Invoke(this, EventArgs.Empty);

        _registeredTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await ScheduleReconnectAsync(CancellationToken.None);
    }

    private async Task ScheduleReconnectAsync(CancellationToken ct)
    {
        _reconnectAttempt++;
        var delay = TimeSpan.FromMilliseconds(
            Math.Min(_reconnectMax.TotalMilliseconds,
                _reconnectMin.TotalMilliseconds * Math.Pow(2, _reconnectAttempt)));
        _logger.LogInformation("IRC: reconnect in {Delay}", delay);
        try
        {
            await Task.Delay(delay, ct);
            await ConnectAsync(ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void EnsureRegistered()
    {
        if (!_client.IsConnected || !_registeredTcs.Task.IsCompleted)
            throw new InvalidOperationException("IRC not connected/registered yet.");
    }

    public void Dispose() => _ = DisconnectAsync();
}