namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Request to check off a specific child product from a parent item on the shopping list.
/// </summary>
public class CheckOffChildRequest
{
    /// <summary>
    /// The child product ID being checked off
    /// </summary>
    public Guid ChildProductId { get; set; }

    /// <summary>
    /// Quantity being checked off (can exceed parent's original amount - it's a suggestion, not a limit)
    /// </summary>
    public decimal Quantity { get; set; } = 1;

    /// <summary>
    /// Optional best before date for inventory tracking
    /// </summary>
    public DateTime? BestBeforeDate { get; set; }
}
