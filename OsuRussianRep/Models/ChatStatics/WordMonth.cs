namespace OsuRussianRep.Models.ChatStatics;

public class WordMonth
{
    public DateOnly Month { get; set; }
    public long WordId { get; set; }
    public long Cnt { get; set; }

    public Word Word { get; set; } = null!;
}