namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Dashboard summary of shopping lists grouped by store
/// </summary>
public class ShoppingListDashboardDto
{
    public List<StoreShoppingListSummary> StoresSummary { get; set; } = new();
    public int TotalItems { get; set; }
    public int UnpurchasedItems { get; set; }
    public int TotalLists { get; set; }
}

public class StoreShoppingListSummary
{
    public Guid ShoppingLocationId { get; set; }
    public string ShoppingLocationName { get; set; } = string.Empty;
    public bool HasIntegration { get; set; }
    public List<ShoppingListSummaryDto> Lists { get; set; } = new();
    public int TotalItems { get; set; }
}
