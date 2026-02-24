using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Full stock entry data with product information.
/// </summary>
public class StockEntryDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductBarcode { get; set; }
    public decimal Amount { get; set; }
    public DateTime? BestBeforeDate { get; set; }
    public DateTime PurchasedDate { get; set; }
    public string StockId { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public bool Open { get; set; }
    public DateTime? OpenedDate { get; set; }
    public OpenTrackingMode? OpenTrackingMode { get; set; }
    public decimal? OriginalAmount { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? ShoppingLocationId { get; set; }
    public string? ShoppingLocationName { get; set; }
    public string? Note { get; set; }
    public string QuantityUnitName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Calculated remaining percentage (0-100) when OpenTrackingMode is Percentage.
    /// </summary>
    public decimal? RemainingPercentage => OpenTrackingMode == Domain.Enums.OpenTrackingMode.Percentage && OriginalAmount.HasValue && OriginalAmount > 0
        ? Math.Round(Amount / OriginalAmount.Value * 100, 1)
        : null;

    /// <summary>
    /// Indicates if the item is expired based on BestBeforeDate.
    /// </summary>
    public bool IsExpired => BestBeforeDate.HasValue && BestBeforeDate.Value.Date < DateTime.UtcNow.Date;

    /// <summary>
    /// Days until expiry (negative if expired).
    /// </summary>
    public int? DaysUntilExpiry => BestBeforeDate.HasValue
        ? (int)(BestBeforeDate.Value.Date - DateTime.UtcNow.Date).TotalDays
        : null;
}
