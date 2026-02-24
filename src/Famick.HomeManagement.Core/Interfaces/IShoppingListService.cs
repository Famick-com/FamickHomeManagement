using Famick.HomeManagement.Core.DTOs.ShoppingLists;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing shopping lists and their items
/// </summary>
public interface IShoppingListService
{
    // List management
    Task<ShoppingListDto> CreateListAsync(
        CreateShoppingListRequest request,
        CancellationToken cancellationToken = default);

    Task<ShoppingListDto?> GetListByIdAsync(
        Guid id,
        bool includeItems = true,
        CancellationToken cancellationToken = default);

    Task<List<ShoppingListSummaryDto>> ListAllAsync(
        CancellationToken cancellationToken = default);

    Task<List<ShoppingListSummaryDto>> ListByStoreAsync(
        Guid shoppingLocationId,
        CancellationToken cancellationToken = default);

    Task<ShoppingListDashboardDto> GetDashboardSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<ShoppingListDto> UpdateListAsync(
        Guid id,
        UpdateShoppingListRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteListAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // Item management
    Task<ShoppingListItemDto> AddItemAsync(
        Guid listId,
        AddShoppingListItemRequest request,
        CancellationToken cancellationToken = default);

    Task<ShoppingListItemDto> UpdateItemAsync(
        Guid itemId,
        UpdateShoppingListItemRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task MarkItemAsPurchasedAsync(
        Guid itemId,
        MarkItemPurchasedRequest request,
        CancellationToken cancellationToken = default);

    Task ClearPurchasedItemsAsync(
        Guid listId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a preview of purchased items for the clear purchased dialog
    /// </summary>
    Task<ClearPurchasedPreviewDto> GetClearPurchasedPreviewAsync(
        Guid listId,
        CancellationToken cancellationToken = default);

    // Quick add from Stock Overview or barcode scan
    Task<ShoppingListItemDto> QuickAddItemAsync(
        AddToShoppingListRequest request,
        CancellationToken cancellationToken = default);

    // Store integration
    Task<ShoppingListItemDto> LookupItemInStoreAsync(
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task<SendToCartResult> SendToCartAsync(
        Guid listId,
        SendToCartRequest request,
        CancellationToken cancellationToken = default);

    // Scan-to-decrement purchase
    /// <summary>
    /// Increments PurchasedQuantity by request.Quantity (default 1).
    /// Auto-marks item as purchased when PurchasedQuantity >= Amount.
    /// </summary>
    Task<ScanPurchaseResultDto> ScanPurchaseAsync(
        Guid itemId,
        ScanPurchaseRequest request,
        CancellationToken cancellationToken = default);

    // Toggle purchased status
    Task<ShoppingListItemDto> TogglePurchasedAsync(
        Guid itemId,
        MarkItemPurchasedRequest? request = null,
        CancellationToken cancellationToken = default);

    // Barcode scanning in shopping session
    Task<BarcodeScanResultDto> ScanBarcodeAsync(
        Guid listId,
        string barcode,
        CancellationToken cancellationToken = default);

    // Smart features
    Task<List<ProductSuggestionDto>> SuggestProductsAsync(
        Guid listId,
        CancellationToken cancellationToken = default);

    Task<ShoppingListByLocationDto> GroupItemsByLocationAsync(
        Guid listId,
        CancellationToken cancellationToken = default);

    // Move to inventory
    Task<MoveToInventoryResponse> MoveToInventoryAsync(
        MoveToInventoryRequest request,
        CancellationToken cancellationToken = default);

    // Child product management (for parent products with variants)
    /// <summary>
    /// Gets child products for a parent product on a shopping list item.
    /// Only returns children with store metadata for the shopping list's store.
    /// </summary>
    Task<List<ShoppingListItemChildDto>> GetChildProductsForItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks off a specific child product with quantity.
    /// Updates the parent item's ChildPurchasesJson and potentially marks as purchased.
    /// </summary>
    Task<ShoppingListItemDto> CheckOffChildAsync(
        Guid itemId,
        CheckOffChildRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unchecks a child product purchase entry.
    /// </summary>
    Task<ShoppingListItemDto> UncheckChildAsync(
        Guid itemId,
        Guid childProductId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a specific child product to the store's online cart.
    /// </summary>
    Task<SendToCartResult> SendChildToCartAsync(
        Guid listId,
        Guid itemId,
        SendChildToCartRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a child product to a parent item (from store search or existing product).
    /// </summary>
    Task<ShoppingListItemDto> AddChildToParentAsync(
        Guid itemId,
        AddChildToParentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches the store for products that can be added as children to a parent item.
    /// </summary>
    Task<List<StoreProductSearchResult>> SearchStoreForChildAsync(
        Guid itemId,
        string query,
        CancellationToken cancellationToken = default);
}
