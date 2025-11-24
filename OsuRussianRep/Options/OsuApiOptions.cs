namespace OsuRussianRep.Options;

public class OsuApiOptions
{
    public string ClientId { get; set; } = "12345";
    public string ClientSecret { get; set; } = "secret";
    public string RedirectUri { get; set; } = "https://domain/api/Auth/Callback";
    public string TokenFilePath { get; set; } = "data/osu.token.json";
    public long AllowedUserId { get; set; } = 13928601;
    public int ChannelId { get; set; } = 117;
}