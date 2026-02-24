namespace Famick.HomeManagement.Core.DTOs.ProductLookup;

/// <summary>
/// Controls which plugin types to search
/// </summary>
public enum ProductSearchMode
{
    /// <summary>
    /// Search local products first, then all enabled plugins (USDA, Open Food Facts, store integrations, etc.)
    /// Local products always appear first in results.
    /// </summary>
    AllSources = 0,

    /// <summary>
    /// Only search plugins that implement IStoreIntegrationPlugin (connected stores)
    /// </summary>
    StoreIntegrationsOnly = 1,

    /// <summary>
    /// Only search local products database (no external plugins)
    /// </summary>
    LocalProductsOnly = 2,

    /// <summary>
    /// Only search external sources (USDA, Open Food Facts, store integrations, etc.)
    /// Skips local products. Useful for updating existing products with external metadata.
    /// </summary>
    ExternalSourcesOnly = 3
}

/// <summary>
/// Request for product lookup search
/// </summary>
public class ProductLookupRequest
{
    /// <summary>
    /// Search query - can be a barcode (8-14 digits) or a product name.
    /// The system auto-detects which type of search to perform.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of results to return (default: 20)
    /// </summary>
    public int MaxResults { get; set; } = 20;

    /// <summary>
    /// Optional: Specific plugin ID to search (if null, searches all enabled plugins)
    /// </summary>
    public string? PluginId { get; set; }

    /// <summary>
    /// Optional: Preferred shopping location ID for store integration searches.
    /// If provided, searches will also include results from this store's integration.
    /// </summary>
    public Guid? PreferredShoppingLocationId { get; set; }

    /// <summary>
    /// Whether to include store integration results in unified search (default: true)
    /// </summary>
    public bool IncludeStoreResults { get; set; } = true;

    /// <summary>
    /// Search mode - controls which plugin types to search.
    /// Default is AllSources for backward compatibility.
    /// </summary>
    public ProductSearchMode SearchMode { get; set; } = ProductSearchMode.AllSources;
}
