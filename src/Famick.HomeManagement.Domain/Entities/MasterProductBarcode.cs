namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// A universal barcode associated with a master product.
/// Barcodes (UPC/EAN) are universal identifiers shared across all tenants.
/// </summary>
public class MasterProductBarcode : BaseEntity
{
    public Guid MasterProductId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? Note { get; set; }

    // Navigation properties
    public MasterProduct MasterProduct { get; set; } = null!;
}
