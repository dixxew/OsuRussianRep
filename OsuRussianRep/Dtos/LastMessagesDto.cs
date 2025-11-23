namespace OsuRussianRep.Dtos;

public class LastMessagesDto
{
    public long Offset { get; set; }
    public int Limit { get; set; }
    public IEnumerable<MessageDto> Messages { get; set; } = [];
}