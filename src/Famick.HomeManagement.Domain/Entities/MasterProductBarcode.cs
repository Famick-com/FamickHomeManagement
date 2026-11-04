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

    /// <summary>
    /// For Type 2 (weight/price-embedded) barcodes, the 2-digit prefix (e.g., "20"-"29").
    /// When non-null, the Barcode value is a 5-digit item number, not a full UPC.
    /// </summary>
    public string? Type2Prefix { get; set; }

    /// <summary>
    /// True when this is the barcode of a specific product that would be a child of this
    /// (generic) master product, e.g. one brand's UPC under "2% Milk". A lookup of the barcode
    /// still finds the master product, but the barcode is never copied onto a household's copy
    /// of it, and it is not used to link or enrich the master from a household product.
    /// <see cref="Note"/> carries the variant's label.
    /// </summary>
    public bool IsChildCandidate { get; set; }

    // Navigation properties
    public MasterProduct MasterProduct { get; set; } = null!;
}
