namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class CreateShoppingListRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ShoppingLocationId { get; set; }
}
