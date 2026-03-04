namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a dependent meal plan entry consuming from a batch cook item.
/// For example, Wednesday's Chicken Salad using chicken batch-cooked on Monday.
/// </summary>
public class BatchCookItemUsage : BaseEntity
{
    /// <summary>
    /// FK to the BatchCookItem being consumed.
    /// </summary>
    public Guid BatchCookItemId { get; set; }

    /// <summary>
    /// FK to the MealPlanEntry that consumes from the batch.
    /// </summary>
    public Guid DependentEntryId { get; set; }

    /// <summary>
    /// How much is consumed by this entry (null = auto from meal's product quantity).
    /// </summary>
    public decimal? QuantityUsed { get; set; }

    // Navigation properties
    public virtual BatchCookItem BatchCookItem { get; set; } = null!;
    public virtual MealPlanEntry DependentEntry { get; set; } = null!;
}
