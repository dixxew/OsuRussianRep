namespace OsuRussianRep.Dtos.OsuWebChat;

public record WebChatMessage(
    long message_id,
    long sender_id,
    int channel_id,
    DateTime timestamp,
    string content,
    string type,
    bool is_action,
    WebChatSender sender
);
