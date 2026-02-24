namespace Famick.HomeManagement.Core.DTOs.ShoppingLocations;

/// <summary>
/// Request to update the custom aisle order for a store
/// </summary>
public class UpdateAisleOrderRequest
{
    /// <summary>
    /// Ordered list of aisles. Empty/null clears custom ordering (uses default).
    /// </summary>
    public List<string>? OrderedAisles { get; set; }
}
