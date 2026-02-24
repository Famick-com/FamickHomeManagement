namespace Famick.HomeManagement.Core.DTOs.StoreIntegrations;

/// <summary>
/// Request to search for store locations
/// </summary>
public class StoreSearchRequest
{
    /// <summary>
    /// Plugin ID to search with (e.g., "kroger")
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// ZIP/postal code to search near (use this OR coordinates)
    /// </summary>
    public string? ZipCode { get; set; }

    /// <summary>
    /// Latitude to search near (use with Longitude)
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude to search near (use with Latitude)
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Search radius in miles (default 10)
    /// </summary>
    public int RadiusMiles { get; set; } = 10;
}
