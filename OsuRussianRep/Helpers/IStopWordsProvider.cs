namespace OsuRussianRep.Helpers;

public interface IStopWordsProvider
{
    bool Contains(string word);
    IReadOnlyCollection<string> All { get; }
}