namespace Famick.HomeManagement.Mobile.Models;

// Meal Planner mobile models - lightweight DTOs matching API responses

public class MealSummaryMobile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MealDetailMobile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public List<MealItemMobile> Items { get; set; } = new();
}

public class MealItemMobile
{
    public Guid Id { get; set; }
    public int ItemType { get; set; } // 0=Recipe, 1=Product, 2=Freetext
    public Guid? RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal? ProductQuantity { get; set; }
    public string? ProductQuantityUnitName { get; set; }
    public string? FreetextDescription { get; set; }
    public int SortOrder { get; set; }

    public string DisplayName => ItemType switch
    {
        0 => RecipeName ?? "Recipe",
        1 => ProductName ?? "Product",
        2 => FreetextDescription ?? "Item",
        _ => "Unknown"
    };
}

public class MealTypeMobile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public string? Color { get; set; }
}

public class MealPlanMobile
{
    public Guid Id { get; set; }
    public DateTime WeekStartDate { get; set; }
    public uint Version { get; set; }
    public List<MealPlanEntryMobile> Entries { get; set; } = new();
}

public class MealPlanEntryMobile
{
    public Guid Id { get; set; }
    public Guid? MealId { get; set; }
    public string? MealName { get; set; }
    public string? InlineNote { get; set; }
    public Guid MealTypeId { get; set; }
    public string? MealTypeName { get; set; }
    public int DayOfWeek { get; set; }
    public int SortOrder { get; set; }
    public bool IsBatchSource { get; set; }
    public Guid? BatchSourceEntryId { get; set; }

    public string DisplayName => MealName ?? InlineNote ?? string.Empty;
    public bool IsInlineNote => MealId == null && InlineNote != null;
}

public class MealNutritionMobile
{
    public Guid MealId { get; set; }
    public decimal TotalCalories { get; set; }
    public decimal TotalProteinGrams { get; set; }
    public decimal TotalCarbsGrams { get; set; }
    public decimal TotalFatGrams { get; set; }
}

public class TodaysMealsMobile
{
    public DateTime Date { get; set; }
    public List<TodaysMealGroupMobile> MealGroups { get; set; } = new();
}

public class TodaysMealGroupMobile
{
    public string MealTypeName { get; set; } = string.Empty;
    public string? MealTypeColor { get; set; }
    public List<TodaysMealEntryMobile> Entries { get; set; } = new();
}

public class TodaysMealEntryMobile
{
    public Guid? MealId { get; set; }
    public string? MealName { get; set; }
    public string? InlineNote { get; set; }

    public string DisplayName => MealName ?? InlineNote ?? string.Empty;
    public bool IsInlineNote => MealId == null && InlineNote != null;
}

public class OnboardingStateMobile
{
    public bool HasCompletedOnboarding { get; set; }
    public int? PlanningStyle { get; set; }
    public List<Guid> CollapsedMealTypeIds { get; set; } = new();
}

public class CreateMealPlanEntryRequest
{
    public Guid? MealId { get; set; }
    public string? InlineNote { get; set; }
    public Guid MealTypeId { get; set; }
    public int DayOfWeek { get; set; }
    public bool IsBatchSource { get; set; }
    public Guid? BatchSourceEntryId { get; set; }
}

public class CreateMealMobileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public List<CreateMealItemMobileRequest> Items { get; set; } = new();
}

public class CreateMealItemMobileRequest
{
    public int ItemType { get; set; } // 0=Recipe, 1=Product, 2=Freetext
    public Guid? RecipeId { get; set; }
    public Guid? ProductId { get; set; }
    public string? FreetextDescription { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateMealMobileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public List<CreateMealItemMobileRequest> Items { get; set; } = new();
}

public class UpdateMealPlanEntryMobileRequest
{
    public Guid? MealId { get; set; }
    public string? InlineNote { get; set; }
    public Guid MealTypeId { get; set; }
    public int DayOfWeek { get; set; }
    public int SortOrder { get; set; }
    public bool IsBatchSource { get; set; }
    public Guid? BatchSourceEntryId { get; set; }
}

public class SaveOnboardingMobileRequest
{
    public int? PlanningStyle { get; set; }
    public List<MealTypeSelection>? MealTypes { get; set; }
}

public class MealTypeSelection
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}

public class AllergenWarningMobile
{
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public int AllergenType { get; set; }
    public int Severity { get; set; }
    public string ProductName { get; set; } = string.Empty;
}

public class MealPlanAllergenWarningsMobile
{
    public Guid MealPlanId { get; set; }
    public bool HasWarnings { get; set; }
    public List<AllergenWarningMobile> Warnings { get; set; } = new();
}
