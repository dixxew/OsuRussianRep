namespace OsuRussianRep.Models.ChatStatics;

public class WordDay
{
    public DateOnly Day { get; set; }
    public long WordId { get; set; }
    public long Cnt { get; set; }

    public Word Word { get; set; } = null!;
}