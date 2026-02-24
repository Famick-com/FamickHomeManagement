namespace Famick.HomeManagement.Core.DTOs.ProductLookup;

/// <summary>
/// Response from unified product lookup that includes results from
/// both product plugins and store integrations
/// </summary>
public class ProductLookupResponse
{
    /// <summary>
    /// Results from all sources (product plugins and store integrations)
    /// </summary>
    public List<ProductLookupResultDto> Results { get; set; } = new();

    /// <summary>
    /// Status of store integrations (shows which stores need connection)
    /// </summary>
    public List<StoreConnectionStatus> StoreConnectionStatuses { get; set; } = new();
}

/// <summary>
/// Status of a store integration for the preferred shopping location
/// </summary>
public class StoreConnectionStatus
{
    /// <summary>
    /// Plugin ID (e.g., "kroger")
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable plugin name (e.g., "Kroger")
    /// </summary>
    public string PluginDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Shopping location ID that uses this store integration
    /// </summary>
    public Guid? ShoppingLocationId { get; set; }

    /// <summary>
    /// Shopping location name
    /// </summary>
    public string? ShoppingLocationName { get; set; }

    /// <summary>
    /// Whether the user has a valid OAuth connection to this store
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Whether the OAuth token has expired and needs re-authentication
    /// </summary>
    public bool RequiresReauth { get; set; }
}
