namespace Famick.HomeManagement.Core.DTOs.Products;

public class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid LocationId { get; set; }
    public Guid QuantityUnitIdPurchase { get; set; }
    public Guid QuantityUnitIdStock { get; set; }
    public decimal QuantityUnitFactorPurchaseToStock { get; set; }
    public decimal MinStockAmount { get; set; }
    public int DefaultBestBeforeDays { get; set; }
    public bool TracksBestBeforeDate { get; set; }
    public bool IsActive { get; set; }

    // Per-product expiry warning threshold override
    public int? ExpiryWarningDays { get; set; }

    // Serving/package information
    public decimal? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public decimal? ServingsPerContainer { get; set; }

    // Phase 2 properties
    public Guid? ProductGroupId { get; set; }
    public Guid? ShoppingLocationId { get; set; }

    // Parent product for variants
    public Guid? ParentProductId { get; set; }
}
