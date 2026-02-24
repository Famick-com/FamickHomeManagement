namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class UpdateShoppingListRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
