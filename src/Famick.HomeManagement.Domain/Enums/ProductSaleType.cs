namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Indicates how a product is sold/priced at the store.
/// </summary>
public enum ProductSaleType
{
    /// <summary>Standard fixed-weight/unit items (default)</summary>
    Unit = 0,

    /// <summary>Sold by weight (meats, deli, bulk, produce)</summary>
    Weight = 1
}
