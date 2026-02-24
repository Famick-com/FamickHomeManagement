using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Home;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class HomeMappingProfile : Profile
{
    public HomeMappingProfile()
    {
        // Home -> HomeDto
        CreateMap<Home, HomeDto>()
            .ForMember(dest => dest.Utilities,
                opt => opt.MapFrom(src => src.Utilities));

        // PropertyLink -> PropertyLinkDto
        CreateMap<PropertyLink, PropertyLinkDto>();

        // HomeSetupRequest -> Home
        CreateMap<HomeSetupRequest, Home>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsSetupComplete, opt => opt.Ignore())
            .ForMember(dest => dest.Utilities, opt => opt.Ignore())
            .ForMember(dest => dest.PropertyLinks, opt => opt.Ignore())
            // Maintenance fields not in setup request
            .ForMember(dest => dest.AcFilterReplacementIntervalDays, opt => opt.Ignore())
            .ForMember(dest => dest.FridgeWaterFilterType, opt => opt.Ignore())
            .ForMember(dest => dest.UnderSinkFilterType, opt => opt.Ignore())
            .ForMember(dest => dest.WholeHouseFilterType, opt => opt.Ignore())
            .ForMember(dest => dest.HvacServiceSchedule, opt => opt.Ignore())
            .ForMember(dest => dest.PestControlSchedule, opt => opt.Ignore())
            // Insurance fields not in setup request
            .ForMember(dest => dest.InsuranceType, opt => opt.Ignore())
            .ForMember(dest => dest.InsurancePolicyNumber, opt => opt.Ignore())
            .ForMember(dest => dest.InsuranceAgentName, opt => opt.Ignore())
            .ForMember(dest => dest.InsuranceAgentPhone, opt => opt.Ignore())
            .ForMember(dest => dest.InsuranceAgentEmail, opt => opt.Ignore())
            .ForMember(dest => dest.MortgageInfo, opt => opt.Ignore())
            .ForMember(dest => dest.PropertyTaxAccountNumber, opt => opt.Ignore())
            .ForMember(dest => dest.EscrowDetails, opt => opt.Ignore())
            .ForMember(dest => dest.AppraisalValue, opt => opt.Ignore())
            .ForMember(dest => dest.AppraisalDate, opt => opt.Ignore());

        // UpdateHomeRequest -> Home
        CreateMap<UpdateHomeRequest, Home>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsSetupComplete, opt => opt.Ignore())
            .ForMember(dest => dest.Utilities, opt => opt.Ignore())
            .ForMember(dest => dest.PropertyLinks, opt => opt.Ignore());

        // HomeUtility -> HomeUtilityDto
        CreateMap<HomeUtility, HomeUtilityDto>();

        // CreateHomeUtilityRequest -> HomeUtility
        CreateMap<CreateHomeUtilityRequest, HomeUtility>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.HomeId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Home, opt => opt.Ignore());

        // UpdateHomeUtilityRequest -> HomeUtility
        CreateMap<UpdateHomeUtilityRequest, HomeUtility>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.HomeId, opt => opt.Ignore())
            .ForMember(dest => dest.UtilityType, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Home, opt => opt.Ignore());
    }
}
