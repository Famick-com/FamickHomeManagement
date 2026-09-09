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

    // The barcode can match a catalogue product that simply isn't on this list. The scan
    // already resolved the barcode to a product id to answer Found, so it reports the
    // product here rather than throwing the work away and making the caller re-derive it
    // through products/by-barcode. Only populated when Found is false.

    /// <summary>Id of the catalogue product this barcode resolved to, when it is not on the list.</summary>
    public Guid? ResolvedProductId { get; set; }

    /// <summary>Name of the catalogue product this barcode resolved to, when it is not on the list.</summary>
    public string? ResolvedProductName { get; set; }

    /// <summary>Whether the resolved product tracks a best-before date.</summary>
    public bool ResolvedTracksBestBeforeDate { get; set; }

    /// <summary>Default shelf life in days for the resolved product.</summary>
    public int ResolvedDefaultBestBeforeDays { get; set; }
}
