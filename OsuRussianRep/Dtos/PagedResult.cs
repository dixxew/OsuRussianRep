namespace OsuRussianRep.Dtos;

public sealed class PagedResult<T>
{
    public  int TotalRecords { get; init; }
    public  int PageNumber   { get; init; }
    public  int PageSize     { get; init; }
    public  int TotalPages   { get; init; }
    public  IReadOnlyList<T> Data { get; init; } = Array.Empty<T>();
}