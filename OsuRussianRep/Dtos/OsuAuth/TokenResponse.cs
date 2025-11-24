namespace OsuRussianRep.Dtos.OsuAuth;

public record TokenResponse(
    string token_type,
    int expires_in,
    string access_token,
    string refresh_token
);