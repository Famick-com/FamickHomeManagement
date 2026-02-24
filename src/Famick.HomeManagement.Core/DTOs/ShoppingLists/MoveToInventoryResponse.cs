namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Response from moving shopping items to inventory
/// </summary>
public class MoveToInventoryResponse
{
    /// <summary>
    /// Number of items added to stock
    /// </summary>
    public int ItemsAddedToStock { get; set; }

    /// <summary>
    /// Number of TODO items created for products needing setup
    /// </summary>
    public int TodoItemsCreated { get; set; }

    /// <summary>
    /// IDs of created stock entries
    /// </summary>
    public List<Guid> StockEntryIds { get; set; } = new();

    /// <summary>
    /// IDs of created TODO items
    /// </summary>
    public List<Guid> TodoItemIds { get; set; } = new();

    /// <summary>
    /// Any errors that occurred during processing
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
