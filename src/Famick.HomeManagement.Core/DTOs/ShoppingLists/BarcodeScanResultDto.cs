namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class BarcodeScanResultDto
{
    public bool Found { get; set; }
    public Guid? ItemId { get; set; }
    public string? ProductName { get; set; }
    public bool IsChildProduct { get; set; }
    public Guid? ChildProductId { get; set; }
    public string? ChildProductName { get; set; }
    public bool NeedsChildSelection { get; set; }

    /// <summary>Embedded price from a Type 2 barcode (null if not price-embedded)</summary>
    public decimal? EmbeddedPrice { get; set; }

    /// <summary>Embedded weight in lbs from a Type 2 barcode (null if not weight-embedded)</summary>
    public decimal? EmbeddedWeight { get; set; }

    /// <summary>Whether the matched product is sold by weight</summary>
    public bool IsSoldByWeight { get; set; }
}
