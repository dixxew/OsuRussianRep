using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using OsuRussianRep.Dtos.OsuWebChat;
using OsuRussianRep.Options;
using OsuRussianRep.Services;

public class OsuWebChatLoggerService(
    OsuWebChatService webChat,
    IOptions<OsuApiOptions> config,
    WebMessageHandler handler,
    OsuChannelStateStorage storage,
    ILogger<OsuWebChatLoggerService> logger)
    : BackgroundService
{
    private readonly OsuApiOptions _config = config.Value;
    private long _lastMessageId = 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[WebChat] Старт логгера канала {Channel}", _config.ChannelId);

        OsuChannelState state = new();

        try
        {
            state = await storage.LoadAsync();
            _lastMessageId = state.LastMessageId;

            logger.LogInformation("[WebChat] Загружено состояние: LastMessageId = {LastMessageId}",
                _lastMessageId);

            if (_lastMessageId == 0)
            {
                logger.LogWarning("[WebChat] LastMessageId = 0, запрашиваю keepalive...");

                await webChat.SendKeepalive();

                // логика получения последнего id пока отрублена
                // _lastMessageId = await webChat.GetLatestKnownMessageId(_config.ChannelId);
                logger.LogInformation("[WebChat] Keepalive отправлен, начнём с нуля");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WebChat] Ошибка при загрузке состояния");
            await Task.Delay(30000, stoppingToken);
            await ExecuteAsync(stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await webChat.SendKeepalive();
                logger.LogDebug("[WebChat] Keepalive OK");

                var msgs = await webChat.GetMessages(_config.ChannelId, _lastMessageId);

                if (msgs.Count > 0)
                    logger.LogInformation("[WebChat] Получено {Count} сообщений", msgs.Count);
                else
                    logger.LogDebug("[WebChat] Новых сообщений нет");

                foreach (var m in msgs)
                {
                    logger.LogInformation("[{Ts}] <{User}> {Text}",
                        m.timestamp,
                        m.sender?.username ?? m.sender_id.ToString(),
                        m.content);

                    await handler.HandleAsync(m, stoppingToken);
                    
                    _lastMessageId = Math.Max(_lastMessageId, m.message_id);
                    state.LastMessageId = _lastMessageId;
                    await storage.SaveAsync(state);

                    logger.LogDebug("[WebChat] Состояние обновлено: LastMessageId = {Last}",
                        _lastMessageId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[WebChat] Ошибка в основном цикле");
            }

            await Task.Delay(15000, stoppingToken);
        }

        logger.LogInformation("[WebChat] Логгер остановлен");
    }
}
