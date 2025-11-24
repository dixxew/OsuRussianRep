using System.Text.Json;
using Microsoft.Extensions.Options;
using OsuRussianRep.Dtos.OsuWebChat;
using OsuRussianRep.Options;

namespace OsuRussianRep.Services;

public class OsuChannelStateStorage
{
    private readonly string _path;

    public OsuChannelStateStorage(IOptions<OsuApiOptions> cfg)
    {
        _path = $"osu.channel.{cfg.Value.ChannelId}.json";
    }

    public async Task<OsuChannelState> LoadAsync()
    {
        if (!File.Exists(_path))
            return new OsuChannelState { LastMessageId = 0 };

        return JsonSerializer.Deserialize<OsuChannelState>(
                   await File.ReadAllTextAsync(_path))
               ?? new OsuChannelState();
    }

    public Task SaveAsync(OsuChannelState state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        return File.WriteAllTextAsync(_path, json);
    }
    
}