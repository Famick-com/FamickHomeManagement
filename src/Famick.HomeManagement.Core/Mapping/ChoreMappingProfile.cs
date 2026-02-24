using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Chores;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class ChoreMappingProfile : Profile
{
    public ChoreMappingProfile()
    {
        CreateMap<Chore, ChoreDto>()
            .ForMember(dest => dest.NextExecutionAssignedToUserName,
                opt => opt.MapFrom(src => src.NextExecutionAssignedToUser != null
                    ? $"{src.NextExecutionAssignedToUser.FirstName} {src.NextExecutionAssignedToUser.LastName}".Trim()
                    : null))
            .ForMember(dest => dest.ProductName,
                opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : null))
            .ForMember(dest => dest.NextExecutionDate,
                opt => opt.Ignore()); // Calculated in service

        CreateMap<Chore, ChoreSummaryDto>()
            .ForMember(dest => dest.AssignedToUserName,
                opt => opt.MapFrom(src => src.NextExecutionAssignedToUser != null
                    ? $"{src.NextExecutionAssignedToUser.FirstName} {src.NextExecutionAssignedToUser.LastName}".Trim()
                    : null))
            .ForMember(dest => dest.NextExecutionDate,
                opt => opt.Ignore()) // Calculated in service
            .ForMember(dest => dest.IsOverdue,
                opt => opt.Ignore()); // Calculated in service

        CreateMap<CreateChoreRequest, Chore>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.NextExecutionAssignedToUserId, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore())
            .ForMember(dest => dest.NextExecutionAssignedToUser, opt => opt.Ignore())
            .ForMember(dest => dest.LogEntries, opt => opt.Ignore());

        CreateMap<UpdateChoreRequest, Chore>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.NextExecutionAssignedToUserId, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore())
            .ForMember(dest => dest.NextExecutionAssignedToUser, opt => opt.Ignore())
            .ForMember(dest => dest.LogEntries, opt => opt.Ignore());

        CreateMap<ChoreLog, ChoreLogDto>()
            .ForMember(dest => dest.ChoreName,
                opt => opt.MapFrom(src => src.Chore != null ? src.Chore.Name : string.Empty))
            .ForMember(dest => dest.DoneByUserName,
                opt => opt.MapFrom(src => src.DoneByUser != null
                    ? $"{src.DoneByUser.FirstName} {src.DoneByUser.LastName}".Trim()
                    : null));
    }
}
