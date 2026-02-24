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
    /// Whether the OAuth connection is active (token not expired)
    /// </summary>
    public bool IsConnected { get; set; }

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
}
