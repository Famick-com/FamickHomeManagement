namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Stock information for a child product variant within a parent product's overview.
/// </summary>
public class StockOverviewChildDto
{
    /// <summary>
    /// The child product ID.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Child product name.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Total amount in stock for this child product.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Name of the stock quantity unit.
    /// </summary>
    public string QuantityUnitName { get; set; } = string.Empty;

    /// <summary>
    /// Earliest best before date among all stock entries for this child.
    /// </summary>
    public DateTime? NextDueDate { get; set; }

    /// <summary>
    /// Days until the next due date (negative if past).
    /// </summary>
    public int? DaysUntilDue { get; set; }

    /// <summary>
    /// True if any stock entry has expired.
    /// </summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// True if expiring within the next 5 days.
    /// </summary>
    public bool IsDueSoon { get; set; }

    /// <summary>
    /// URL to the primary product image thumbnail.
    /// </summary>
    public string? PrimaryImageUrl { get; set; }
}
