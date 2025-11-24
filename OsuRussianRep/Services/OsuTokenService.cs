using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OsuRussianRep.Dtos.OsuAuth;
using OsuRussianRep.Options;

namespace OsuRussianRep.Services;

public class OsuTokenService
{
    private readonly OsuApiOptions _cfg;
    private readonly OsuTokenStorage _storage;
    private readonly HttpClient _http = new();

    public OsuTokenService(IOptions<OsuApiOptions> cfg, OsuTokenStorage storage)
    {
        _cfg = cfg.Value;
        _storage = storage;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var state = await _storage.LoadAsync();
        if (state == null) throw new Exception("Не авторизовано.");

        if (state.ExpiresAt < DateTimeOffset.UtcNow.AddMinutes(1))
        {
            state = await RefreshAsync(state);
            await _storage.SaveAsync(state);
        }

        return state.AccessToken;
    }

    public async Task SaveNewTokens(TokenResponse tokens)
    {
        // ПАРСИМ JWT
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokens.access_token);

        var sub = jwt.Claims.First(x => x.Type == "sub").Value;
        var userId = int.Parse(sub);

        if (userId != _cfg.AllowedUserId)
            throw new Exception("Авторизовался не тот пользователь.");

        var state = new OsuTokenState
        {
            AccessToken = tokens.access_token,
            RefreshToken = tokens.refresh_token,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.expires_in)
        };

        await _storage.SaveAsync(state);
    }

    private async Task<OsuTokenState> RefreshAsync(OsuTokenState state)
    {
        var body = new Dictionary<string, string>
        {
            ["client_id"] = _cfg.ClientId,
            ["client_secret"] = _cfg.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = state.RefreshToken,
            ["scope"] = "public chat.read chat.write chat.write_manage"
        };

        var res = await _http.PostAsync("https://osu.ppy.sh/oauth/token",
            new FormUrlEncodedContent(body));

        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();
        var tokens = JsonSerializer.Deserialize<TokenResponse>(json)!;

        return new OsuTokenState
        {
            AccessToken = tokens.access_token,
            RefreshToken = tokens.refresh_token,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.expires_in)
        };
    }
}

