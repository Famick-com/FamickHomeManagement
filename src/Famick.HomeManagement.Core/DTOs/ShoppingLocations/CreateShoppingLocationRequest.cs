namespace Famick.HomeManagement.Core.DTOs.ShoppingLocations;

public class CreateShoppingLocationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Address fields - available for ALL stores
    public string? StoreAddress { get; set; }
    public string? StorePhone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Integration fields - only used when creating from integration
    public string? PluginId { get; set; }
    public string? ExternalLocationId { get; set; }
    public string? ExternalChainId { get; set; }
}
