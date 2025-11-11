namespace OsuRussianRep.Interfaces;

public interface IIrcService : IDisposable
{
    // состояние
    bool IsConnected { get; }

    // коннект/дисконнект
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(string? reason = null);

    // каналы
    Task JoinAsync(string channel, CancellationToken ct = default);
    Task PartAsync(string channel, string? reason = null, CancellationToken ct = default);

    // отправка
    Task SendChannelMessageAsync(string channel, string message, CancellationToken ct = default);
    Task SendPrivateMessageAsync(string nick, string message, CancellationToken ct = default);

    // события
    event EventHandler? Connected;
    event EventHandler? Disconnected;
    event EventHandler<IrcChannelMessageEventArgs>? ChannelMessageReceived;
    event EventHandler<IrcPrivateMessageEventArgs>? PrivateMessageReceived;
}

// простые евент-арги
public sealed class IrcChannelMessageEventArgs(string channel, string nick, string text) : EventArgs
{
    public string Channel { get; } = channel;
    public string Nick { get; } = nick;
    public string Text { get; } = text;
}

public sealed class IrcPrivateMessageEventArgs(string nick, string text) : EventArgs
{
    public string Nick { get; } = nick;
    public string Text { get; } = text;
}
