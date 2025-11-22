namespace OsuRussianRep.Services;

public interface IIrcLogEnqueuer
{
    void EnqueueMessage(string channel, string nick, string text, DateTime dateUtc);
}