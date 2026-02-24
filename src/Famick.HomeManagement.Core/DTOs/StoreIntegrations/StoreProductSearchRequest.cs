namespace Famick.HomeManagement.Core.DTOs.StoreIntegrations;

/// <summary>
/// Request to search for products at a store
/// </summary>
public class StoreProductSearchRequest
{
    /// <summary>
    /// Search query (product name or barcode)
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int MaxResults { get; set; } = 20;
}
