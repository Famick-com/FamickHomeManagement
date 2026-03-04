namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a single slot in a meal plan: either a meal reference or an inline note.
/// Exactly one of MealId or InlineNote must be set.
/// </summary>
public class MealPlanEntry : BaseEntity
{
    public Guid MealPlanId { get; set; }

    /// <summary>
    /// Reference to a meal (mutually exclusive with InlineNote).
    /// </summary>
    public Guid? MealId { get; set; }

    /// <summary>
    /// Inline note text (mutually exclusive with MealId). Max 200 characters.
    /// </summary>
    public string? InlineNote { get; set; }

    public Guid MealTypeId { get; set; }

    /// <summary>
    /// Day of the week (0 = Monday, 6 = Sunday).
    /// </summary>
    public int DayOfWeek { get; set; }

    /// <summary>
    /// Display order within the same day and meal type.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this entry is the source of a batch cook (makes extras for other entries).
    /// </summary>
    public bool IsBatchSource { get; set; }

    /// <summary>
    /// Self-referencing FK to the batch source entry (this entry uses leftovers from that entry).
    /// Mutually exclusive with IsBatchSource.
    /// </summary>
    public Guid? BatchSourceEntryId { get; set; }

    // Navigation properties
    public virtual MealPlan MealPlan { get; set; } = null!;
    public virtual Meal? Meal { get; set; }
    public virtual MealType MealType { get; set; } = null!;
    public virtual MealPlanEntry? BatchSourceEntry { get; set; }
    public virtual ICollection<MealPlanEntry> BatchDependentEntries { get; set; } = new List<MealPlanEntry>();

    /// <summary>
    /// Ingredient-level batch cook items sourced from this entry.
    /// </summary>
    public virtual ICollection<BatchCookItem> BatchCookItems { get; set; } = new List<BatchCookItem>();

    /// <summary>
    /// Ingredient-level batch cook usages consumed by this entry.
    /// </summary>
    public virtual ICollection<BatchCookItemUsage> BatchCookItemUsages { get; set; } = new List<BatchCookItemUsage>();
}
