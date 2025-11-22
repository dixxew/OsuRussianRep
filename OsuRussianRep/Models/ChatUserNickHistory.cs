namespace OsuRussianRep.Models;

public class ChatUserNickHistory
{
    public ChatUser ChatUser { get; set; }

    public Guid ChatUserId { get; set; }
    public string Nickname { get; set; }
}