using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Core.Interfaces.Plugins;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for searching products using the plugin pipeline.
/// Plugins are executed in the order defined in config.json, each can add or enrich results.
/// </summary>
public interface IProductLookupService
{
    /// <summary>
    /// Search for products using plugins in the pipeline.
    /// Automatically detects if the query is a barcode (8-14 digits) or a name search.
    /// Each plugin can add new results or enrich existing ones (e.g., add images).
    /// </summary>
    /// <param name="query">Search query (barcode or product name)</param>
    /// <param name="maxResults">Maximum results to return</param>
    /// <param name="searchMode">Which plugin types to search (default: all sources)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Accumulated results from the plugin pipeline</returns>
    Task<List<ProductLookupResult>> SearchAsync(
        string query,
        int maxResults = 20,
        ProductSearchMode searchMode = ProductSearchMode.AllSources,
        CancellationToken ct = default);

    /// <summary>
    /// Apply a lookup result to an existing product, updating its nutrition data
    /// </summary>
    /// <param name="productId">The product ID to update</param>
    /// <param name="result">The lookup result to apply</param>
    /// <param name="ct">Cancellation token</param>
    Task ApplyLookupResultAsync(Guid productId, ProductLookupResult result, CancellationToken ct = default);

    /// <summary>
    /// Get available plugins for display in the UI
    /// </summary>
    IReadOnlyList<PluginInfo> GetAvailablePlugins();

}

/// <summary>
/// Plugin information for display
/// </summary>
public class PluginInfo
{
    public string PluginId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? AttributionUrl { get; set; }
    public string? LicenseText { get; set; }
    public string? Description { get; set; }
    public string? ProductUrlTemplate { get; set; }
}
