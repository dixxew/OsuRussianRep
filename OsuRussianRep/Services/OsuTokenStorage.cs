using System.Text.Json;
using Microsoft.Extensions.Options;
using OsuRussianRep.Dtos.OsuAuth;
using OsuRussianRep.Options;

namespace OsuRussianRep.Services;

public class OsuTokenStorage
{
    private readonly string _path;

    public OsuTokenStorage(IOptions<OsuApiOptions> cfg)
    {
        _path = cfg.Value.TokenFilePath;
    }

    public async Task<OsuTokenState?> LoadAsync()
    {
        if (!File.Exists(_path))
        {
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(new OsuTokenState()));
            return null;
        }

        var json = await File.ReadAllTextAsync(_path);
        return JsonSerializer.Deserialize<OsuTokenState>(json);
    }

    public async Task SaveAsync(OsuTokenState state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_path, json);
    }
}


