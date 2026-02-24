namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class ShoppingListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ShoppingLocationId { get; set; }
    public string? ShoppingLocationName { get; set; }
    public bool HasStoreIntegration { get; set; }
    public bool CanAddToCart { get; set; }
    public List<ShoppingListItemDto> Items { get; set; } = new();
    public int ItemCount { get; set; }
    public int PurchasedCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
