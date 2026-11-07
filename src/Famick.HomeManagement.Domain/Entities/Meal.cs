using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a reusable meal composed of recipes, products, and freetext items.
/// </summary>
public class Meal : BaseTenantEntity, ISharedCopy
{
    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public bool IsFavorite { get; set; }

    /// <inheritdoc />
    public string? OriginKey { get; set; }

    /// <inheritdoc />
    public string? SharedByHouseholdName { get; set; }

    // Navigation properties
    public virtual ICollection<MealItem> Items { get; set; } = new List<MealItem>();
    public virtual ICollection<MealPlanEntry> MealPlanEntries { get; set; } = new List<MealPlanEntry>();
}
