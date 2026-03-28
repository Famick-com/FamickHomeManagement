namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a barcode associated with a product
/// A product can have multiple barcodes
/// </summary>
public class ProductBarcode : BaseTenantEntity
{
    public Guid ProductId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? Note { get; set; }

    /// <summary>
    /// For Type 2 (weight/price-embedded) barcodes, the 2-digit prefix (e.g., "20"-"29").
    /// When non-null, the Barcode value is a 5-digit item number, not a full UPC.
    /// </summary>
    public string? Type2Prefix { get; set; }

    // Navigation properties
    public Product Product { get; set; } = null!;
}
