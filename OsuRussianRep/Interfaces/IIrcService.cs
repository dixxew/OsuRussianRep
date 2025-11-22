namespace OsuRussianRep.Interfaces;

public interface IIrcService : IDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(string? reason = null);

    Task JoinAsync(string channel, CancellationToken ct = default);
    Task PartAsync(string channel, string? reason = null, CancellationToken ct = default);

    Task SendChannelMessageAsync(string channel, string message, CancellationToken ct = default);
    Task SendPrivateMessageAsync(string nick, string message, CancellationToken ct = default);

    event EventHandler? Connected;
    event EventHandler? Disconnected;
    event EventHandler<IrcChannelMessageEventArgs>? ChannelMessageReceived;
    event EventHandler<IrcPrivateMessageEventArgs>? PrivateMessageReceived;
    event EventHandler<IrcWhoisMessageEventArgs>? WhoisMessageReceived;
    void RequestWhois(string msgNick);
}

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

public sealed class IrcWhoisMessageEventArgs(string nick, string url) : EventArgs
{
    public string Nick { get; } = nick;
    public string ProfileUrl { get; } = url;
}