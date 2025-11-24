using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OsuRussianRep.Dtos.OsuWebChat;
using OsuRussianRep.Options;

namespace OsuRussianRep.Services;

public class OsuWebChatService
{
    private readonly OsuTokenService _osuTokenService;
    private readonly OsuApiOptions _config;
    private readonly HttpClient _http = new();

    public OsuWebChatService(OsuTokenService osuTokenService, IOptions<OsuApiOptions> config)
    {
        _osuTokenService = osuTokenService;
        _config = config.Value;

        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private async Task EnsureAuth()
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await _osuTokenService.GetAccessTokenAsync());
    }

    public async Task SendKeepalive()
    {
        await EnsureAuth();
        
        var res = await _http.PostAsync("https://osu.ppy.sh/api/v2/chat/ack", null);
        res.EnsureSuccessStatusCode();
    }

    public async Task<List<WebChatMessage>> GetMessages(int channelId, long? since = null)
    {
        await EnsureAuth();

        var url = $"https://osu.ppy.sh/api/v2/chat/channels/{channelId}/messages?{(since is null ? "" : $"since={since}")}&limit=50";
        var res = await _http.GetAsync(url);
        
        var json = await res.Content.ReadAsStringAsync();
        res.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<List<WebChatMessage>>(json) ?? new();
    }
    
    public async Task<long> GetLatestKnownMessageId(int channelId)
    {
        await EnsureAuth();

        var url = "https://osu.ppy.sh/api/v2/chat/updates";
        var res = await _http.GetAsync(url);
        var json = await res.Content.ReadAsStringAsync();
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("presence", out var presence))
            return 0;

        foreach (var ch in presence.EnumerateArray())
        {
            if (ch.GetProperty("channel_id").GetInt32() == channelId)
            {
                if (ch.TryGetProperty("last_message_id", out var lm))
                    return lm.GetInt64();
            }
        }

        return 0;
    }
    
    public async Task SendMe()
    {
        await EnsureAuth();

        var url = "https://osu.ppy.sh/api/v2/notifications";
        var res = await _http.GetAsync(url);
        var json = await res.Content.ReadAsStringAsync();
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);
    }

}