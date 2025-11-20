namespace OsuRussianRep.Helpers;

public class StopWordsProvider : IStopWordsProvider
{
    private readonly string[] Words =
    {
        "я", "ты", "он", "она", "мы", "вы", "они", "как", "это", "что", "бы", "вот", "чё", "че",
        "в", "во", "на", "по", "из", "и", "или", "а", "но", "же", "то", "щас", "всё", "когда", "уже",
        "к", "с", "у", "о", "об", "от", "до", "за", "для", "под", "там", "чтобы", "чтоб", "только", "тока",
        "osu", "pp", "https", "http", "twitch", "discord", "sh", "ещё", "какойто", "какоето",
        "com", "ru", "org", "net", "youtu", "youtube", "ss", "так", "да", "какието", "тоже", "его", "их",
        "www", "ppy", "osu.ppy", "osu.ppy.sh", "мне", "ну", "меня", "live", "65535", "io", "ul", "eu",
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "staticflickr", "jpg", "png", "beatmapsets",
        "если", "даже", "нибудь", "ща", "кто", "зачем", "где", "нет", "еще", "есть", "без", "s", "не"
    };

    public bool Contains(string word) => Words.Contains(word);
    public IReadOnlyCollection<string> All => Words;
}