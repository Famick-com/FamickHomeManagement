namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Aggregate statistics for stock overview page header and status badges.
/// </summary>
public class StockStatisticsDto
{
    /// <summary>
    /// Total number of unique products with stock entries.
    /// </summary>
    public int TotalProductCount { get; set; }

    /// <summary>
    /// Total value of all stock (sum of price * amount).
    /// </summary>
    public decimal TotalStockValue { get; set; }

    /// <summary>
    /// Number of products with expired stock (BestBeforeDate < Today).
    /// </summary>
    public int ExpiredCount { get; set; }

    /// <summary>
    /// Number of products that are overdue for consumption.
    /// </summary>
    public int OverdueCount { get; set; }

    /// <summary>
    /// Number of products expiring within the next 5 days.
    /// </summary>
    public int DueSoonCount { get; set; }

    /// <summary>
    /// Number of products below their defined minimum stock amount.
    /// </summary>
    public int BelowMinStockCount { get; set; }
}
