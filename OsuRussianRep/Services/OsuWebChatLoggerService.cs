using Microsoft.Extensions.Options;
using OsuRussianRep.Dtos.OsuWebChat;
using OsuRussianRep.Options;
using OsuRussianRep.Services;

public class OsuWebChatLoggerService(
    OsuWebChatService webChat,
    IOptions<OsuApiOptions> config,
    WebMessageHandler handler,
    OsuChannelStateStorage storage)
    : BackgroundService
{
    private readonly OsuApiOptions _config = config.Value;

    private long _lastMessageId = 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        OsuChannelState state = new();
        try
        {
            state = await storage.LoadAsync();
            _lastMessageId = state.LastMessageId;
            if (_lastMessageId == 0)
            {
                await webChat.SendKeepalive();
                //_lastMessageId = await webChat.GetLatestKnownMessageId(_config.ChannelId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка: " + ex.Message);
            await Task.Delay(30000, stoppingToken);
            await ExecuteAsync(stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await webChat.SendKeepalive();

                var msgs = await webChat.GetMessages(_config.ChannelId, _lastMessageId);

                foreach (var m in msgs)
                {
                    Console.WriteLine($"[{m.timestamp}] <{m.sender?.username}> {m.content}");

                    await handler.HandleAsync(m, stoppingToken);

                    // обновляем и сохраняем
                    _lastMessageId = Math.Max(_lastMessageId, m.message_id);
                    state.LastMessageId = _lastMessageId;
                    await storage.SaveAsync(state);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }

            await Task.Delay(2000, stoppingToken);
        }
    }
}