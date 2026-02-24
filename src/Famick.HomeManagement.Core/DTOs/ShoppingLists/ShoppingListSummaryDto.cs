namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class ShoppingListSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ShoppingLocationId { get; set; }
    public string ShoppingLocationName { get; set; } = string.Empty;
    public bool HasStoreIntegration { get; set; }
    public int TotalItems { get; set; }
    public int PurchasedItems { get; set; }
    public DateTime UpdatedAt { get; set; }
}
