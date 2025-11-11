namespace OsuRussianRep.Models;

public class Message
{
    public long Seq { get; set; } 
    public Guid Id { get; set; }
    public string Text { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public Guid UserId { get; set; }
    public ChatUser? User { get; set; }
    public string ChatChannel { get; set; }
}
