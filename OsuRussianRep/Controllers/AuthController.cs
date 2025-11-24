using Microsoft.AspNetCore.Mvc;

namespace OsuRussianRep.Controllers;

[ApiController]
[Route("api/[Controller]/[action]")]
public class AuthController(IConfiguration config) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Callback(string code)
    {
        var resp = await ExchangeCode(code);
        Console.WriteLine("ACCESS: " + resp.access_token);
        Console.WriteLine("REFRESH: " + resp.refresh_token);
        return Ok();
    }
    
    async Task<TokenResponse> ExchangeCode(string code)
    {
        var http = new HttpClient();
        var dict = new Dictionary<string, string>
        {
            ["client_id"] = config["OsuApi:ClientId"],
            ["client_secret"] = config["OsuApi:ClientSecret"],
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = "http://localhost:5000/callback"
        };

        var res = await http.PostAsync(
            "https://osu.ppy.sh/oauth/token",
            new FormUrlEncodedContent(dict)
        );
        
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(json)!;
    }
    
    public record TokenResponse(
        string token_type,
        int expires_in,
        string access_token,
        string refresh_token
    );

}