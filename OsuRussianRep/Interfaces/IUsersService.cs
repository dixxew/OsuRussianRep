using OsuRussianRep.Dtos;

namespace OsuRussianRep.Interfaces;

public interface IUsersService
{
    Task<PagedResult<ChatUserDto>> GetUsersAsync(
        string sortField,
        int pageNumber,
        int pageSize,
        string? search = null,
        CancellationToken ct = default);
}