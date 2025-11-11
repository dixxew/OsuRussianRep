namespace OsuRussianRep.Dtos;

public class ChatUserDto
{
    public Guid Id { get; set; }
    public long OsuId { get; set; }
    public List<string> Playstyle { get; set; }
    public string Nickname { get; set; }
    public List<string> PrevNicknames { get; set; }
    public string CountryCode { get; set; }
    public long? Reputation { get; set; }
    public string? Avatar { get; set; }
    public string? LastRepNickname { get; set; }
    public DateTime? LastRepTime { get; set; }
    public long MessagesCount { get; set; } = 0;
    public long Level { get; set; } // Строка для уровня
    public double Pp { get; set; } // Целое число для PP
    public long Rank { get; set; } // Целое число для ранга
    public long PlayCount { get; set; } // Целое число для количества игр
    public double Accuracy { get; set; } // Число с плавающей запятой для точности
    public double PlayTime { get; set; } // Число с плавающей запятой для времени игры
    public string OsuMode { get; set; }
}
