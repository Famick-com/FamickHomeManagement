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

    /// <summary>
    /// Weight (in the product's stock unit) read from a by-weight barcode.
    /// When set, the purchased amount is incremented by this weight instead of by
    /// <see cref="Quantity"/>, and the item is treated as a completed single purchase.
    /// </summary>
    public decimal? EmbeddedWeight { get; set; }

    /// <summary>
    /// Price read from a price-embedded (Type 2) barcode. When set, it is recorded as
    /// the item's purchase price and flows through to inventory on shopping completion.
    /// </summary>
    public decimal? EmbeddedPrice { get; set; }
}
