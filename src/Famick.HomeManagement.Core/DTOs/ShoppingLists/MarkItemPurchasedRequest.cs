namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class MarkItemPurchasedRequest
{
    public Guid? LocationId { get; set; } // Where purchased
    public DateTime? PurchasedAt { get; set; }
    public decimal? ActualAmount { get; set; } // If different from planned
    public DateTime? BestBeforeDate { get; set; } // Expiration date for inventory tracking
}
