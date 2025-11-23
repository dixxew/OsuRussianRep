using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;
using OsuRussianRep.Interfaces;
using OsuRussianRep.Models;

namespace OsuRussianRep.Services;

public sealed class UsersService(
    AppDbContext context,
    OsuService osuService,
    IMapper mapper,
    OsuUserCache osuUserCache,
    ILogger<UsersService> logger)
    : IUsersService
{
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
        if (pageSize <= 0) pageSize = 10;
        pageSize = Math.Min(pageSize, MaxPageSize);

        var baseQuery = context.ChatUsers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            baseQuery = baseQuery.Where(u =>
                u.Nickname.ToLower().Contains(term));
        }

        var sort = (sortField ?? "").Trim().ToLowerInvariant();
        IQueryable<ChatUser> ordered = sort switch
        {
            "messages" => baseQuery
                .OrderByDescending(u => u.MessagesCount)
                .ThenByDescending(u => u.Reputation),

            _ => baseQuery
                .OrderByDescending(u => u.Reputation)
                .ThenByDescending(u => u.MessagesCount)
        };

        var totalRecords = await baseQuery.CountAsync(ct);

        var pageQuery = ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        var dtoPage = await pageQuery
            .ProjectTo<ChatUserDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        await EnrichBatchAsync(dtoPage, ct);

        var totalPages = (int) Math.Ceiling(totalRecords / (double) pageSize);

        return new PagedResult<ChatUserDto>
        {
            TotalRecords = totalRecords,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            Data = dtoPage
        };
    }


    private async Task EnrichBatchAsync(
        List<ChatUserDto> dtos,
        CancellationToken ct)
    {
        using var sem = new SemaphoreSlim(MaxDegreeOfParallelism);

        var results = new List<(ChatUserDto Dto, CachedOsuUserDto? Osu)>();
        var lockObj = new object();

        var tasks = dtos.Select(async dto =>
        {
            await sem.WaitAsync(ct);
            try
            {
                CachedOsuUserDto? osu = null;

                try
                {
                    osu = await osuUserCache.GetUserAsync(dto.Nickname, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to fetch osu data for {Nick}", dto.Nickname);
                }

                lock (lockObj)
                    results.Add((dto, osu));
            }
            finally
            {
                sem.Release();
            }
        });

        await Task.WhenAll(tasks);

        foreach (var (dto, osu) in results)
        {
            if (osu == null)
            {
                dto.Avatar = $"https://a.ppy.sh/{dto.OsuId}?1753301069.jpeg";;
                continue;
            }

            // DTO fill
            dto.OsuId = osu.Id;
            dto.PrevNicknames = osu.PreviousUsernames?.ToList() ?? [];
            dto.CountryCode = osu.CountryCode;
            dto.Playstyle = osu.Playstyle?.ToList() ?? [];
            dto.Avatar = $"https://a.ppy.sh/{osu.Id}?1753301069.jpeg";
            dto.OsuMode = osu.GameMode.ToString();

            var stats = osu.Statistics;
            if (stats != null)
            {
                dto.Accuracy = stats.HitAccuracy ?? 0;
                dto.Level = stats.Level ?? 0;
                dto.PlayCount = stats.PlayCount ?? 0;
                dto.PlayTime = stats.PlayTimeHours ?? 0;
                dto.Pp = stats.Pp ?? 0;
                dto.Rank = stats.GlobalRank ?? 0;
            }
        }
    }
}