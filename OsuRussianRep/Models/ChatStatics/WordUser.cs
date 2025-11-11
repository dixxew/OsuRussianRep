namespace OsuRussianRep.Models.ChatStatics;

public class WordUser
{
    public Guid UserId { get; set; }
    public long WordId { get; set; }
    public long Cnt { get; set; }

    public Word Word { get; set; } = null!;
    public ChatUser User { get; set; } = null!;
    
}