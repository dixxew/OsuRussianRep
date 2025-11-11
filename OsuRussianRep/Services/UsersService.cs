using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;
using OsuRussianRep.Interfaces;

namespace OsuRussianRep.Services;

public sealed class UsersService(
    AppDbContext context,
    OsuService osuService,
    IMapper mapper,
    OsuUserCache osuUserCache,
    ILogger<UsersService> logger)
    : IUsersService
{
    // чтобы не жарить внешнее API безлимитно
    private const int MaxDegreeOfParallelism = 8;
    private const int MaxPageSize = 100;

    public async Task<PagedResult<ChatUserDto>> GetUsersAsync(
        string sortField,
        int pageNumber,
        int pageSize,
        string? search = null,
        CancellationToken ct = default)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize   <= 0) pageSize   = 10;
        pageSize = Math.Min(pageSize, MaxPageSize);

        var baseQuery = context.ChatUsers
            .AsNoTracking();
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            baseQuery = baseQuery.Where(u =>
                u.Nickname.ToLower().Contains(term));
        }

        var sort = (sortField ?? "").Trim().ToLowerInvariant();
        IQueryable<Models.ChatUser> ordered = sort switch
        {
            "messages" => baseQuery
                .OrderByDescending(u => u.Messages.Count())
                .ThenByDescending(u => u.Reputation),
            _ => baseQuery
                .OrderByDescending(u => u.Reputation)
                .ThenByDescending(u => u.Messages.Count())
        };

        var totalRecords = await baseQuery.CountAsync(ct);

        var pageQuery = ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        var dtoPage = await pageQuery
            .ProjectTo<ChatUserDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        // ограничиваем конкуренцию
        using var sem = new SemaphoreSlim(MaxDegreeOfParallelism);
        var tasks = dtoPage.Select(async dto =>
        {
            await sem.WaitAsync(ct);
            try
            {
                await EnrichWithOsuAsync(dto, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to enrich osu data for {Nick}", dto.Nickname);
            }
            finally
            {
                sem.Release();
            }
        });

        await Task.WhenAll(tasks);

        var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

        return new PagedResult<ChatUserDto>
        {
            TotalRecords = totalRecords,
            PageNumber   = pageNumber,
            PageSize     = pageSize,
            TotalPages   = totalPages,
            Data         = dtoPage
        };
    }

    private async Task EnrichWithOsuAsync(ChatUserDto dto, CancellationToken ct)
    {
        var osuUser = await osuUserCache.GetUserAsync(dto.Nickname, ct);
        if (osuUser == null)
        {
            dto.Avatar = string.Empty;
            return;
        }

        dto.OsuId = osuUser.Id;
        dto.PrevNicknames = osuUser.PreviousUsernames.ToList();
        dto.CountryCode = osuUser.CountryCode;
        dto.Playstyle = osuUser.Playstyle.ToList();
        
        var stats = osuUser. Statistics;

        dto.Avatar = osuUser.AvatarUrl?.ToString() ?? dto.Avatar ?? string.Empty;

        if (stats != null)
        {
            dto.Accuracy  = stats.HitAccuracy ?? 0;
            dto.Level     = stats.Level ?? 0;
            dto.PlayCount = stats.PlayCount ?? 0;
            dto.PlayTime  = stats.PlayTimeHours ?? 0; // sec -> hours
            dto.Pp        = stats.Pp ?? 0;
            dto.Rank      = stats.GlobalRank ?? 0;
        }

        dto.OsuMode = osuUser.GameMode.ToString();
    }
}
