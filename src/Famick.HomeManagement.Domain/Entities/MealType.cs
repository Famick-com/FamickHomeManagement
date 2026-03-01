namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a meal type category (e.g., Breakfast, Lunch, Dinner, Snack).
/// </summary>
public class MealType : BaseTenantEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display order in the planner grid.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this is a system-seeded default type.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Optional display color (hex string).
    /// </summary>
    public string? Color { get; set; }

    // Navigation properties
    public virtual ICollection<MealPlanEntry> MealPlanEntries { get; set; } = new List<MealPlanEntry>();
}
