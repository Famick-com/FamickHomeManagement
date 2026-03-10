namespace Famick.HomeManagement.Core.DTOs.Products;

/// <summary>
/// A combined search result for parent product selection,
/// including both tenant products and master catalog products.
/// </summary>
public class ParentProductSearchResultDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ProductGroupName { get; set; }
    public int ChildProductCount { get; set; }

    /// <summary>
    /// "tenant" for existing tenant products, "master" for master catalog products.
    /// </summary>
    public string Source { get; set; } = "tenant";

    /// <summary>
    /// Only set when Source is "master" — the master product ID to use with EnsureProductFromMasterAsync.
    /// </summary>
    public Guid? MasterProductId { get; set; }
}
