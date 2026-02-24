namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Filter parameters for stock entry queries.
/// </summary>
public class StockFilterRequest
{
    /// <summary>
    /// Filter by product ID.
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// Filter by location ID.
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Filter by open status.
    /// </summary>
    public bool? Open { get; set; }

    /// <summary>
    /// Filter to only include expired items.
    /// </summary>
    public bool? ExpiredOnly { get; set; }

    /// <summary>
    /// Filter to include items expiring within this many days.
    /// </summary>
    public int? ExpiringWithinDays { get; set; }

    /// <summary>
    /// Search term to filter by product name.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Sort by field (Amount, BestBeforeDate, PurchasedDate, ProductName).
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort in descending order.
    /// </summary>
    public bool Descending { get; set; }
}
