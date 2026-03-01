using AutoMapper;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class MealPlannerMappingProfile : Profile
{
    public MealPlannerMappingProfile()
    {
        // MealType mappings
        CreateMap<CreateMealTypeRequest, MealType>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.IsDefault, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.MealPlanEntries, opt => opt.Ignore());

        CreateMap<UpdateMealTypeRequest, MealType>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.IsDefault, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.MealPlanEntries, opt => opt.Ignore());

        CreateMap<MealType, MealTypeDto>();

        // Meal mappings
        CreateMap<CreateMealRequest, Meal>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Items, opt => opt.Ignore())
            .ForMember(dest => dest.MealPlanEntries, opt => opt.Ignore());

        CreateMap<UpdateMealRequest, Meal>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Items, opt => opt.Ignore())
            .ForMember(dest => dest.MealPlanEntries, opt => opt.Ignore());

        CreateMap<CreateMealItemRequest, MealItem>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.MealId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Meal, opt => opt.Ignore())
            .ForMember(dest => dest.Recipe, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore())
            .ForMember(dest => dest.ProductQuantityUnit, opt => opt.Ignore());

        // MealPlan entry mappings
        CreateMap<CreateMealPlanEntryRequest, MealPlanEntry>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.MealPlanId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.MealPlan, opt => opt.Ignore())
            .ForMember(dest => dest.Meal, opt => opt.Ignore())
            .ForMember(dest => dest.MealType, opt => opt.Ignore())
            .ForMember(dest => dest.BatchSourceEntry, opt => opt.Ignore())
            .ForMember(dest => dest.BatchDependentEntries, opt => opt.Ignore());

        CreateMap<UpdateMealPlanEntryRequest, MealPlanEntry>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.MealPlanId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.MealPlan, opt => opt.Ignore())
            .ForMember(dest => dest.Meal, opt => opt.Ignore())
            .ForMember(dest => dest.MealType, opt => opt.Ignore())
            .ForMember(dest => dest.BatchSourceEntry, opt => opt.Ignore())
            .ForMember(dest => dest.BatchDependentEntries, opt => opt.Ignore());
    }
}
