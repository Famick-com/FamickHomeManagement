namespace Famick.HomeManagement.Core.DTOs.StoreIntegrations;

/// <summary>
/// Request to link a shopping location to an external store
/// </summary>
public class LinkStoreLocationRequest
{
    /// <summary>
    /// Plugin ID (e.g., "kroger")
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// External store location ID from the search results
    /// </summary>
    public string ExternalLocationId { get; set; } = string.Empty;

    /// <summary>
    /// Chain/brand identifier
    /// </summary>
    public string? ExternalChainId { get; set; }

    /// <summary>
    /// Store name
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// Store address
    /// </summary>
    public string? StoreAddress { get; set; }

    /// <summary>
    /// Store phone
    /// </summary>
    public string? StorePhone { get; set; }

    /// <summary>
    /// Latitude
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude
    /// </summary>
    public double? Longitude { get; set; }
}
