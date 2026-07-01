namespace Famick.HomeManagement.Core.DTOs.ProductLookup;

/// <summary>
/// One contributing source for a (possibly merged) product search result.
/// A single result row can carry several of these — e.g. a product that exists in the
/// master catalog, in the local database, and at the linked store all merge into one row
/// with three <see cref="ResultSourceDto"/> entries. Drives the per-source badges in the UI.
/// </summary>
public class ResultSourceDto
{
    /// <summary>
    /// Stable source identifier (e.g. "local", "master", "usda", "kroger").
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable source name (e.g. "Local Database", "Master Catalog",
    /// "USDA FoodData Central", "Kroger").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Source category: "LocalProduct", "MasterCatalog", "ProductPlugin", or "StoreIntegration".
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// External identifier from this source (local product id, master product id,
    /// fdcId from USDA, product id from the store, etc.).
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;
}
