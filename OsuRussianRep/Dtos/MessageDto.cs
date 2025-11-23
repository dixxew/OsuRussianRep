namespace OsuRussianRep.Dtos;

public class MessageDto
{
    public string Nickname { get; set; }
    public string Text { get; set; }
    public DateTime Date { get; set; }
    public string ChatChannel { get; set; }
    public Guid UserId { get; set; }
    public long? UserOsuId { get; set; }
}