using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class RecipeMappingProfile : Profile
{
    public RecipeMappingProfile()
    {
        // Request → Entity mappings (entity→DTO is done via private methods in RecipeService)

        CreateMap<CreateRecipeRequest, Recipe>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedByContact, opt => opt.Ignore())
            .ForMember(dest => dest.Steps, opt => opt.Ignore())
            .ForMember(dest => dest.Images, opt => opt.Ignore())
            .ForMember(dest => dest.NestedRecipes, opt => opt.Ignore())
            .ForMember(dest => dest.ParentRecipes, opt => opt.Ignore())
            .ForMember(dest => dest.ShareTokens, opt => opt.Ignore());

        CreateMap<UpdateRecipeRequest, Recipe>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedByContact, opt => opt.Ignore())
            .ForMember(dest => dest.Steps, opt => opt.Ignore())
            .ForMember(dest => dest.Images, opt => opt.Ignore())
            .ForMember(dest => dest.NestedRecipes, opt => opt.Ignore())
            .ForMember(dest => dest.ParentRecipes, opt => opt.Ignore())
            .ForMember(dest => dest.ShareTokens, opt => opt.Ignore());

        CreateMap<CreateRecipeStepRequest, RecipeStep>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.RecipeId, opt => opt.Ignore())
            .ForMember(dest => dest.StepOrder, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ImageFileName, opt => opt.Ignore())
            .ForMember(dest => dest.ImageOriginalFileName, opt => opt.Ignore())
            .ForMember(dest => dest.ImageContentType, opt => opt.Ignore())
            .ForMember(dest => dest.ImageFileSize, opt => opt.Ignore())
            .ForMember(dest => dest.ImageExternalUrl, opt => opt.Ignore())
            .ForMember(dest => dest.Recipe, opt => opt.Ignore())
            .ForMember(dest => dest.Ingredients, opt => opt.Ignore());

        CreateMap<UpdateRecipeStepRequest, RecipeStep>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.RecipeId, opt => opt.Ignore())
            .ForMember(dest => dest.StepOrder, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ImageFileName, opt => opt.Ignore())
            .ForMember(dest => dest.ImageOriginalFileName, opt => opt.Ignore())
            .ForMember(dest => dest.ImageContentType, opt => opt.Ignore())
            .ForMember(dest => dest.ImageFileSize, opt => opt.Ignore())
            .ForMember(dest => dest.ImageExternalUrl, opt => opt.Ignore())
            .ForMember(dest => dest.Recipe, opt => opt.Ignore())
            .ForMember(dest => dest.Ingredients, opt => opt.Ignore());

        CreateMap<CreateRecipeIngredientRequest, RecipePosition>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.RecipeStepId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.RecipeStep, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore())
            .ForMember(dest => dest.QuantityUnit, opt => opt.Ignore());

        CreateMap<UpdateRecipeIngredientRequest, RecipePosition>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.RecipeStepId, opt => opt.Ignore())
            .ForMember(dest => dest.ProductId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.RecipeStep, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore())
            .ForMember(dest => dest.QuantityUnit, opt => opt.Ignore());
    }
}
