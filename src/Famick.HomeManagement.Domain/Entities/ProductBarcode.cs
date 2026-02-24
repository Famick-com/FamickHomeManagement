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

    // Navigation properties
    public Product Product { get; set; } = null!;
}
