using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OsuRussianRep.Dtos.OsuAuth;
using OsuRussianRep.Options;
using OsuRussianRep.Services;

namespace OsuRussianRep.Controllers;

[ApiController]
[Route("api/[Controller]/[action]")]
public class AuthController(IOptions<OsuApiOptions> config, OsuTokenService tokens) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Callback(string code)
    {
        await tokens.SaveNewTokens(await ExchangeCode(code));
        return Ok();
    }

    [HttpGet]
    public IActionResult Relogin()
    {
        var url =
            $"https://osu.ppy.sh/oauth/authorize" +
            $"?client_id={config.Value.ClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(config.Value.RedirectUri)}" +
            $"&response_type=code" +
            $"&scope=public+chat.read+chat.write+chat.write_manage";

        return Redirect(url);
    }
    
    async Task<TokenResponse> ExchangeCode(string code)
    {
        var http = new HttpClient();
        var dict = new Dictionary<string, string>
        {
            ["client_id"] = config.Value.ClientId,
            ["client_secret"] = config.Value.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = config.Value.RedirectUri,
        };

        var res = await http.PostAsync(
            "https://osu.ppy.sh/oauth/token",
            new FormUrlEncodedContent(dict)
        );
        
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(json)!;
    }

}