namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Filter parameters for stock overview queries.
/// </summary>
public class StockOverviewFilterRequest
{
    /// <summary>
    /// Filter by location ID.
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Filter by product group ID.
    /// </summary>
    public Guid? ProductGroupId { get; set; }

    /// <summary>
    /// Filter by status: "expired", "overdue", "dueSoon", "belowMinStock".
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Search term to filter by product name.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Field to sort by: "productName", "amount", "nextDueDate", "value".
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort in descending order.
    /// </summary>
    public bool Descending { get; set; }
}
