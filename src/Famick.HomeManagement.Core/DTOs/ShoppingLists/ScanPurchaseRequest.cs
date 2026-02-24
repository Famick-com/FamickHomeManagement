namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class ScanPurchaseRequest
{
    /// <summary>
    /// Quantity to mark as purchased per scan (default 1)
    /// </summary>
    public decimal Quantity { get; set; } = 1;

    /// <summary>
    /// Optional best-before date for inventory tracking
    /// </summary>
    public DateTime? BestBeforeDate { get; set; }
}
