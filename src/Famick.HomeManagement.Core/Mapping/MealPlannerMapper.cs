#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class MealPlannerMapper
{
    // MealType mappings
    [MapperIgnoreTarget(nameof(MealType.Id))]
    [MapperIgnoreTarget(nameof(MealType.TenantId))]
    [MapperIgnoreTarget(nameof(MealType.IsDefault))]
    [MapperIgnoreTarget(nameof(MealType.CreatedAt))]
    [MapperIgnoreTarget(nameof(MealType.UpdatedAt))]
    [MapperIgnoreTarget(nameof(MealType.MealPlanEntries))]
    public static partial MealType FromCreateMealTypeRequest(CreateMealTypeRequest source);

    [MapperIgnoreTarget(nameof(MealType.Id))]
    [MapperIgnoreTarget(nameof(MealType.TenantId))]
    [MapperIgnoreTarget(nameof(MealType.IsDefault))]
    [MapperIgnoreTarget(nameof(MealType.CreatedAt))]
    [MapperIgnoreTarget(nameof(MealType.UpdatedAt))]
    [MapperIgnoreTarget(nameof(MealType.MealPlanEntries))]
    public static partial void UpdateMealType(UpdateMealTypeRequest source, MealType target);

    public static partial MealTypeDto ToMealTypeDto(MealType source);

    // Meal mappings
    [MapperIgnoreTarget(nameof(Meal.Id))]
    [MapperIgnoreTarget(nameof(Meal.TenantId))]
    [MapperIgnoreTarget(nameof(Meal.CreatedAt))]
    [MapperIgnoreTarget(nameof(Meal.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Meal.Items))]
    [MapperIgnoreTarget(nameof(Meal.MealPlanEntries))]
    public static partial Meal FromCreateMealRequest(CreateMealRequest source);

    [MapperIgnoreTarget(nameof(Meal.Id))]
    [MapperIgnoreTarget(nameof(Meal.TenantId))]
    [MapperIgnoreTarget(nameof(Meal.CreatedAt))]
    [MapperIgnoreTarget(nameof(Meal.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Meal.Items))]
    [MapperIgnoreTarget(nameof(Meal.MealPlanEntries))]
    public static partial void UpdateMeal(UpdateMealRequest source, Meal target);

    [MapperIgnoreTarget(nameof(MealItem.Id))]
    [MapperIgnoreTarget(nameof(MealItem.MealId))]
    [MapperIgnoreTarget(nameof(MealItem.CreatedAt))]
    [MapperIgnoreTarget(nameof(MealItem.UpdatedAt))]
    [MapperIgnoreTarget(nameof(MealItem.Meal))]
    [MapperIgnoreTarget(nameof(MealItem.Recipe))]
    [MapperIgnoreTarget(nameof(MealItem.Product))]
    [MapperIgnoreTarget(nameof(MealItem.ProductQuantityUnit))]
    public static partial MealItem FromCreateMealItemRequest(CreateMealItemRequest source);

    // MealPlanEntry mappings
    [MapperIgnoreTarget(nameof(MealPlanEntry.Id))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.MealPlanId))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.CreatedAt))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.UpdatedAt))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.MealPlan))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.Meal))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.MealType))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.BatchSourceEntry))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.BatchDependentEntries))]
    public static partial MealPlanEntry FromCreateMealPlanEntryRequest(CreateMealPlanEntryRequest source);

    [MapperIgnoreTarget(nameof(MealPlanEntry.Id))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.MealPlanId))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.CreatedAt))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.UpdatedAt))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.MealPlan))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.Meal))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.MealType))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.BatchSourceEntry))]
    [MapperIgnoreTarget(nameof(MealPlanEntry.BatchDependentEntries))]
    public static partial void UpdateMealPlanEntry(UpdateMealPlanEntryRequest source, MealPlanEntry target);
}
