namespace Famick.HomeManagement.Core.DTOs.ShoppingLocations;

public class UpdateShoppingLocationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Address fields - editable for ALL stores
    public string? StoreAddress { get; set; }
    public string? StorePhone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
