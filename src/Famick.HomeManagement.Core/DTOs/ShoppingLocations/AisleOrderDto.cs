namespace Famick.HomeManagement.Core.DTOs.ShoppingLocations;

/// <summary>
/// Represents the aisle ordering configuration for a store
/// </summary>
public class AisleOrderDto
{
    /// <summary>
    /// List of aisles in the order they should be displayed (walking order)
    /// </summary>
    public List<string> OrderedAisles { get; set; } = new();

    /// <summary>
    /// All known aisles for this store (from ProductStoreMetadata)
    /// </summary>
    public List<string> KnownAisles { get; set; } = new();
}
