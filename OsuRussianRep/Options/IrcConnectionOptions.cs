namespace OsuRussianRep.Options;

public class IrcConnectionOptions
{
    public string Nickname { get; set; } = "dixxew";
    public string Server { get; set; } = "irc.ppy.sh";
    public ushort Port { get; set; } = 6667;
    public string Password { get; set; } = "password";
    public string Channel { get; set; } = "#russian";
    public bool UseSsl { get; set; } = false;
}