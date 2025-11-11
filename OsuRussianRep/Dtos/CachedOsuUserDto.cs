using OsuSharp.Domain;

namespace OsuRussianRep.Dtos;

public sealed class CachedOsuUserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string? DefaultGroup { get; set; }
    public Uri? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
    public bool IsBot { get; set; }
    public bool IsOnline { get; set; }
    public bool IsSupporter { get; set; }
    public DateTimeOffset? LastVisit { get; set; }
    public bool PmFriendsOnly { get; set; }
    public string? ProfileColour { get; set; }

    // Core user info
    public DateTimeOffset JoinDate { get; set; }
    public string? Discord { get; set; }
    public bool HasSupported { get; set; }
    public string? Interests { get; set; }
    public string? Location { get; set; }
    public string? Occupation { get; set; }
    public GameMode GameMode { get; set; }
    public IReadOnlyList<string> Playstyle { get; set; } = Array.Empty<string>();
    public string? Title { get; set; }
    public string? Twitter { get; set; }
    public string? Website { get; set; }
    public string? TitleUrl { get; set; }

    // Activity
    public long? PostCount { get; set; }
    public long? CommentsCount { get; set; }
    public long? MappingFollowerCount { get; set; }
    public long? FollowerCount { get; set; }
    public long? LovedBeatmapsetCount { get; set; }
    public long? RankedBeatmapsetCount { get; set; }
    public long? PendingBeatmapsetCount { get; set; }
    public long? GraveyardBeatmapsetCount { get; set; }
    public long? FavouriteBeatmapsetCount { get; set; }
    public long? ScoresBestCount { get; set; }
    public long? ScoresFirstCount { get; set; }
    public long? ScoresRecentCount { get; set; }
    public long? BeatmapPlaycountsCount { get; set; }

    // Moderation
    public bool? IsAdmin { get; set; }
    public bool? IsBng { get; set; }
    public bool? IsFullBng { get; set; }
    public bool? IsGmt { get; set; }
    public bool? IsLimitedBn { get; set; }
    public bool? IsModerator { get; set; }
    public bool? IsNat { get; set; }
    public bool? IsRestricted { get; set; }
    public bool? IsSilenced { get; set; }

    // Limits
    public long MaxBlocks { get; set; }
    public long MaxFriends { get; set; }

    // Extras
    public bool? IsDeleted { get; set; }
    public long? SupportLevel { get; set; }
    public IReadOnlyList<string> PreviousUsernames { get; set; } = Array.Empty<string>();
    public List<string> ProfileOrder { get; set; } = new();

    // ⚙️ статистика
    public CachedUserStatsDto? Statistics { get; set; }
}
public sealed class CachedUserStatsDto
{
    public double? Pp { get; set; }
    public long? GlobalRank { get; set; }
    public long? CountryRank { get; set; }
    public double? Accuracy { get; set; }
    public long? Level { get; set; }
    public long? PlayCount { get; set; }
    public double? PlayTimeHours { get; set; }
    public long? RankedScore { get; set; }
    public long? TotalScore { get; set; }
    public double? TotalHits { get; set; }
    public long? MaximumCombo { get; set; }
    public double? HitAccuracy { get; set; }
    public double? AverageRankedAccuracy { get; set; }
}