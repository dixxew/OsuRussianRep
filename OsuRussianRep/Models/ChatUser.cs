namespace OsuRussianRep.Models;

public class ChatUser
{
    public Guid Id { get; set; }
    public string Nickname { get; set; }
    public long? Reputation { get; set; }
    public DateTime? LastRepTime { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAddRep { get; set; }
    public DateTime? LastUsedWordRate { get; set; }
    public string? LastRepNickname { get; set; }
    public ICollection<Message>? Messages { get; set; }
    public DateTime LastMessageDate { get; set; } = DateTime.UtcNow;
}
