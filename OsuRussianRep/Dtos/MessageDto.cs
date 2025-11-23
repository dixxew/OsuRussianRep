namespace OsuRussianRep.Dtos;

public class MessageDto
{
    public long Seq { get; set; }
    public string Text { get; set; }
    public DateTime Date { get; set; }
    public string ChatChannel { get; set; }
    public Guid UserId { get; set; }
}