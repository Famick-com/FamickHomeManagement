namespace Famick.HomeManagement.Core.DTOs.ShoppingLocations;

public class ShoppingLocationFilterRequest
{
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; } = "Name"; // Name, CreatedAt, ProductCount
    public bool Descending { get; set; } = false;
}
