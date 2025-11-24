namespace OsuRussianRep.Dtos.OsuWebChat;

public record WebChatSender(
    long id,
    string username,
    string profile_colour,
    string avatar_url,
    string country_code,
    bool is_active,
    bool is_bot,
    bool is_online,
    bool is_supporter
);