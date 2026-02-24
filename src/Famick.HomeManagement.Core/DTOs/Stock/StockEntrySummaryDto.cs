namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Lightweight stock entry summary for lists.
/// </summary>
public class StockEntrySummaryDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? BestBeforeDate { get; set; }
    public bool Open { get; set; }
    public string? LocationName { get; set; }
    public string QuantityUnitName { get; set; } = string.Empty;

    public bool IsExpired => BestBeforeDate.HasValue && BestBeforeDate.Value.Date < DateTime.UtcNow.Date;
}
