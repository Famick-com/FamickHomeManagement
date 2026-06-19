namespace Famick.HomeManagement.Core.DTOs.ShoppingLocations;

public class ShoppingLocationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProductCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Store Integration fields
    /// <summary>
    /// Integration type (e.g., "kroger"). Null for manual stores.
    /// </summary>
    public string? IntegrationType { get; set; }

    /// <summary>
    /// Whether this store has an active integration
    /// </summary>
    public bool HasIntegration => !string.IsNullOrEmpty(IntegrationType);

    /// <summary>
    /// Whether the integration is usable for its client-credentials features
    /// (product price/availability). True when the plugin is available; these
    /// features need no user OAuth link.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Whether this integration offers a user OAuth "link shopping cart" action
    /// (the plugin implements IOAuthClientAuthentication and supports a cart).
    /// </summary>
    public bool SupportsCartLink { get; set; }

    /// <summary>
    /// Whether the user OAuth link is complete (valid token), enabling cart features.
    /// </summary>
    public bool CartLinked { get; set; }

    /// <summary>
    /// Whether the OAuth token has expired and needs re-authentication (cart features)
    /// </summary>
    public bool RequiresReauth { get; set; }

    /// <summary>
    /// External store location ID
    /// </summary>
    public string? ExternalLocationId { get; set; }

    /// <summary>
    /// Chain/brand identifier
    /// </summary>
    public string? ExternalChainId { get; set; }

    /// <summary>
    /// Store street address
    /// </summary>
    public string? StoreAddress { get; set; }

    /// <summary>
    /// Store phone number
    /// </summary>
    public string? StorePhone { get; set; }

    /// <summary>
    /// Store latitude
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Store longitude
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Custom aisle order for this store, or null for default ordering
    /// </summary>
    public List<string>? AisleOrder { get; set; }

    /// <summary>
    /// Whether this store has a custom aisle order configured
    /// </summary>
    public bool HasCustomAisleOrder => AisleOrder != null && AisleOrder.Count > 0;

    /// <summary>
    /// Starting digit position for item number in Type 2 weight barcodes (1 = US standard, 2 = alternate)
    /// </summary>
    public int Type2ItemNumberStart { get; set; } = 1;
}
