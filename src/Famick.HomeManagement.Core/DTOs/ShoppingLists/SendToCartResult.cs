namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Result of sending shopping list items to the store's cart
/// </summary>
public class SendToCartResult
{
    public bool Success { get; set; }
    public int ItemsSent { get; set; }
    public int ItemsFailed { get; set; }
    public List<SendToCartItemResult> Details { get; set; } = new();
}

public class SendToCartItemResult
{
    public Guid ItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
