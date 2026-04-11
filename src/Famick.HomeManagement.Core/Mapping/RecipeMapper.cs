#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class RecipeMapper
{
    // Recipe mappings
    [MapperIgnoreTarget(nameof(Recipe.Id))]
    [MapperIgnoreTarget(nameof(Recipe.TenantId))]
    [MapperIgnoreTarget(nameof(Recipe.CreatedAt))]
    [MapperIgnoreTarget(nameof(Recipe.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Recipe.CreatedByContact))]
    [MapperIgnoreTarget(nameof(Recipe.Steps))]
    [MapperIgnoreTarget(nameof(Recipe.Images))]
    [MapperIgnoreTarget(nameof(Recipe.NestedRecipes))]
    [MapperIgnoreTarget(nameof(Recipe.ParentRecipes))]
    [MapperIgnoreTarget(nameof(Recipe.ShareTokens))]
    public static partial Recipe FromCreateRequest(CreateRecipeRequest source);

    [MapperIgnoreTarget(nameof(Recipe.Id))]
    [MapperIgnoreTarget(nameof(Recipe.TenantId))]
    [MapperIgnoreTarget(nameof(Recipe.CreatedAt))]
    [MapperIgnoreTarget(nameof(Recipe.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Recipe.CreatedByContact))]
    [MapperIgnoreTarget(nameof(Recipe.Steps))]
    [MapperIgnoreTarget(nameof(Recipe.Images))]
    [MapperIgnoreTarget(nameof(Recipe.NestedRecipes))]
    [MapperIgnoreTarget(nameof(Recipe.ParentRecipes))]
    [MapperIgnoreTarget(nameof(Recipe.ShareTokens))]
    public static partial void Update(UpdateRecipeRequest source, Recipe target);

    // RecipeStep mappings
    [MapperIgnoreTarget(nameof(RecipeStep.Id))]
    [MapperIgnoreTarget(nameof(RecipeStep.TenantId))]
    [MapperIgnoreTarget(nameof(RecipeStep.RecipeId))]
    [MapperIgnoreTarget(nameof(RecipeStep.StepOrder))]
    [MapperIgnoreTarget(nameof(RecipeStep.CreatedAt))]
    [MapperIgnoreTarget(nameof(RecipeStep.UpdatedAt))]
    [MapperIgnoreTarget(nameof(RecipeStep.ImageFileName))]
    [MapperIgnoreTarget(nameof(RecipeStep.ImageOriginalFileName))]
    [MapperIgnoreTarget(nameof(RecipeStep.ImageContentType))]
    [MapperIgnoreTarget(nameof(RecipeStep.ImageFileSize))]
    [MapperIgnoreTarget(nameof(RecipeStep.ImageExternalUrl))]
    [MapperIgnoreTarget(nameof(RecipeStep.Recipe))]
    [MapperIgnoreTarget(nameof(RecipeStep.Ingredients))]
    public static partial RecipeStep FromCreateStepRequest(CreateRecipeStepRequest source);

    [MapperIgnoreTarget(nameof(RecipeStep.Id))]
    [MapperIgnoreTarget(nameof(RecipeStep.TenantId))]
    [MapperIgnoreTarget(nameof(RecipeStep.RecipeId))]
    [MapperIgnoreTarget(nameof(RecipeStep.StepOrder))]
    [MapperIgnoreTarget(nameof(RecipeStep.CreatedAt))]
    [MapperIgnoreTarget(nameof(RecipeStep.UpdatedAt))]
    [MapperIgnoreTarget(nameof(RecipeStep.ImageFileName))]
    [MapperIgnoreTarget(nameof(RecipeStep.ImageOriginalFileName))]
    [MapperIgnoreTarget(nameof(RecipeStep.ImageContentType))]
    [MapperIgnoreTarget(nameof(RecipeStep.ImageFileSize))]
    [MapperIgnoreTarget(nameof(RecipeStep.ImageExternalUrl))]
    [MapperIgnoreTarget(nameof(RecipeStep.Recipe))]
    [MapperIgnoreTarget(nameof(RecipeStep.Ingredients))]
    public static partial void UpdateStep(UpdateRecipeStepRequest source, RecipeStep target);

    // RecipePosition (ingredient) mappings
    [MapperIgnoreTarget(nameof(RecipePosition.Id))]
    [MapperIgnoreTarget(nameof(RecipePosition.TenantId))]
    [MapperIgnoreTarget(nameof(RecipePosition.RecipeStepId))]
    [MapperIgnoreTarget(nameof(RecipePosition.CreatedAt))]
    [MapperIgnoreTarget(nameof(RecipePosition.UpdatedAt))]
    [MapperIgnoreTarget(nameof(RecipePosition.RecipeStep))]
    [MapperIgnoreTarget(nameof(RecipePosition.Product))]
    [MapperIgnoreTarget(nameof(RecipePosition.QuantityUnit))]
    public static partial RecipePosition FromCreateIngredientRequest(CreateRecipeIngredientRequest source);

    [MapperIgnoreTarget(nameof(RecipePosition.Id))]
    [MapperIgnoreTarget(nameof(RecipePosition.TenantId))]
    [MapperIgnoreTarget(nameof(RecipePosition.RecipeStepId))]
    [MapperIgnoreTarget(nameof(RecipePosition.ProductId))]
    [MapperIgnoreTarget(nameof(RecipePosition.CreatedAt))]
    [MapperIgnoreTarget(nameof(RecipePosition.UpdatedAt))]
    [MapperIgnoreTarget(nameof(RecipePosition.RecipeStep))]
    [MapperIgnoreTarget(nameof(RecipePosition.Product))]
    [MapperIgnoreTarget(nameof(RecipePosition.QuantityUnit))]
    public static partial void UpdateIngredient(UpdateRecipeIngredientRequest source, RecipePosition target);
}
