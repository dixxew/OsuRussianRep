namespace OsuRussianRep.Dtos.OsuAuth;

public class OsuTokenState
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}