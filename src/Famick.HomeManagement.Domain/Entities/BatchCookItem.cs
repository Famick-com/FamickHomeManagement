namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a product marked as batch-cooked on a source meal plan entry.
/// For example, cooking chicken in bulk on Monday to use in different meals throughout the week.
/// </summary>
public class BatchCookItem : BaseEntity
{
    /// <summary>
    /// FK to the MealPlanEntry that is the source of the batch cooking.
    /// </summary>
    public Guid SourceEntryId { get; set; }

    /// <summary>
    /// The product being batch-cooked.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// How much total is being batch-cooked (null = unlimited/unspecified).
    /// </summary>
    public decimal? TotalQuantity { get; set; }

    /// <summary>
    /// Unit for TotalQuantity.
    /// </summary>
    public Guid? QuantityUnitId { get; set; }

    // Navigation properties
    public virtual MealPlanEntry SourceEntry { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual QuantityUnit? QuantityUnit { get; set; }
    public virtual ICollection<BatchCookItemUsage> Usages { get; set; } = new List<BatchCookItemUsage>();
}
