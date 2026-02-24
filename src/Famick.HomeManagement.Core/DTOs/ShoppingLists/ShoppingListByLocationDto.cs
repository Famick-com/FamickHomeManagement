namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class ShoppingListByLocationDto
{
    public Guid ShoppingListId { get; set; }
    public string ShoppingListName { get; set; } = string.Empty;
    public List<LocationItemGroup> ItemsByLocation { get; set; } = new();
}

public class LocationItemGroup
{
    public Guid? ShoppingLocationId { get; set; }
    public string ShoppingLocationName { get; set; } = "Unassigned";
    public List<ShoppingListItemDto> Items { get; set; } = new();
}
