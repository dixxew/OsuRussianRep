namespace OsuRussianRep.Models.ChatStatics;

public class Word
{
    public long Id { get; set; }
    public required string Lemma { get; set; }
    public ICollection<WordDay> Days { get; set; } = new List<WordDay>();
    public ICollection<WordMonth> Months { get; set; } = new List<WordMonth>();
    public int WordScore { get; set; }
}