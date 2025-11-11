using AutoMapper;
using OsuRussianRep.Dtos;
using OsuRussianRep.Models;
using OsuSharp.Interfaces;

namespace OsuRussianRep.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ChatUser, ChatUserDto>()
            .ForMember(d => d.MessagesCount, o => o.MapFrom(s => s.Messages.LongCount()))
            // osu-поля заливаем потом вручную
            .ForMember(d => d.Avatar, o => o.Ignore())
            .ForMember(d => d.Accuracy, o => o.Ignore())
            .ForMember(d => d.Level, o => o.Ignore())
            .ForMember(d => d.PlayCount, o => o.Ignore())
            .ForMember(d => d.PlayTime, o => o.Ignore())
            .ForMember(d => d.Pp, o => o.Ignore())
            .ForMember(d => d.Rank, o => o.Ignore())
            .ForMember(d => d.OsuMode, o => o.Ignore());
        
        CreateMap<IUser, CachedOsuUserDto>()
            .ForMember(d => d.Statistics, o => o.MapFrom(s => s.Statistics))
            .ReverseMap();

        CreateMap<IUserStatistics, CachedUserStatsDto>()
            .ForMember(d => d.Pp, o => o.MapFrom(s => s.Pp))
            .ForMember(d => d.GlobalRank, o => o.MapFrom(s => (long?)s.GlobalRank))
            .ForMember(d => d.CountryRank, o => o.MapFrom(s => (long?)s.CountryRank))
            .ForMember(d => d.Accuracy, o => o.MapFrom(s => s.HitAccuracy))
            .ForMember(d => d.Level, o => o.MapFrom(s => (long?)s.UserLevel.Current))
            .ForMember(d => d.PlayCount, o => o.MapFrom(s => (long?)s.PlayCount))
            .ForMember(d => d.PlayTimeHours, o => o.MapFrom(s => s.PlayTime / 3600.0))
            .ForMember(d => d.RankedScore, o => o.MapFrom(s => (long?)s.RankedScore))
            .ForMember(d => d.TotalScore, o => o.MapFrom(s => (long?)s.TotalScore))
            .ForMember(d => d.TotalHits, o => o.MapFrom(s => (double?)s.TotalHits))
            .ForMember(d => d.MaximumCombo, o => o.MapFrom(s => (long?)s.MaximumCombo))
            .ForMember(d => d.HitAccuracy, o => o.MapFrom(s => s.HitAccuracy))
            .ForMember(d => d.AverageRankedAccuracy, o => o.Ignore())
            .ReverseMap();

    }
}