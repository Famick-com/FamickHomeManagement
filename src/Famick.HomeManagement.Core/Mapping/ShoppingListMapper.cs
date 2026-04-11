#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class ShoppingListMapper
{
    // ShoppingList -> ShoppingListDto (computed: ShoppingLocationName, HasStoreIntegration, CanAddToCart, ItemCount, PurchasedCount)
    public static ShoppingListDto ToDto(ShoppingList source)
    {
        var dto = MapShoppingListToDto(source);
        dto.ShoppingLocationName = source.ShoppingLocation != null ? source.ShoppingLocation.Name : null;
        dto.HasStoreIntegration = source.ShoppingLocation != null && !string.IsNullOrEmpty(source.ShoppingLocation.IntegrationType);
        dto.CanAddToCart = source.ShoppingLocation != null && !string.IsNullOrEmpty(source.ShoppingLocation.IntegrationType);
        dto.ItemCount = source.Items != null ? source.Items.Count : 0;
        dto.PurchasedCount = source.Items != null ? source.Items.Count(i => i.IsPurchased) : 0;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ShoppingListDto.ShoppingLocationName))]
    [MapperIgnoreTarget(nameof(ShoppingListDto.HasStoreIntegration))]
    [MapperIgnoreTarget(nameof(ShoppingListDto.CanAddToCart))]
    [MapperIgnoreTarget(nameof(ShoppingListDto.ItemCount))]
    [MapperIgnoreTarget(nameof(ShoppingListDto.PurchasedCount))]
    private static partial ShoppingListDto MapShoppingListToDto(ShoppingList source);

    // ShoppingList -> ShoppingListSummaryDto (computed: ShoppingLocationName, HasStoreIntegration, TotalItems, PurchasedItems)
    public static ShoppingListSummaryDto ToSummaryDto(ShoppingList source)
    {
        var dto = MapShoppingListToSummaryDto(source);
        dto.ShoppingLocationName = source.ShoppingLocation != null ? source.ShoppingLocation.Name : string.Empty;
        dto.HasStoreIntegration = source.ShoppingLocation != null && !string.IsNullOrEmpty(source.ShoppingLocation.IntegrationType);
        dto.TotalItems = source.Items != null ? source.Items.Count : 0;
        dto.PurchasedItems = source.Items != null ? source.Items.Count(i => i.IsPurchased) : 0;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ShoppingListSummaryDto.ShoppingLocationName))]
    [MapperIgnoreTarget(nameof(ShoppingListSummaryDto.HasStoreIntegration))]
    [MapperIgnoreTarget(nameof(ShoppingListSummaryDto.TotalItems))]
    [MapperIgnoreTarget(nameof(ShoppingListSummaryDto.PurchasedItems))]
    private static partial ShoppingListSummaryDto MapShoppingListToSummaryDto(ShoppingList source);

    // ShoppingListItem -> ShoppingListItemDto (computed: ProductName fallback, QuantityUnitName, TracksBestBeforeDate, DefaultBestBeforeDays, DefaultLocationId)
    [UserMapping(Default = true)]
    public static ShoppingListItemDto ToItemDto(ShoppingListItem source)
    {
        var dto = MapShoppingListItemToDto(source);
        dto.ProductName = source.Product != null ? source.Product.Name : source.ProductName;
        dto.QuantityUnitName = source.Product != null && source.Product.QuantityUnitPurchase != null
            ? source.Product.QuantityUnitPurchase.Name : null;
        dto.TracksBestBeforeDate = source.Product != null && source.Product.TracksBestBeforeDate;
        dto.DefaultBestBeforeDays = source.Product != null ? source.Product.DefaultBestBeforeDays : 0;
        dto.DefaultLocationId = source.Product != null ? source.Product.LocationId : null;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ShoppingListItemDto.ProductName))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.QuantityUnitName))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.TracksBestBeforeDate))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.DefaultBestBeforeDays))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.DefaultLocationId))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.IsParentProduct))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.HasChildren))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.ChildProductCount))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.HasChildrenAtStore))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.ChildPurchasedQuantity))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.ChildProducts))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.ChildPurchases))]
    [MapperIgnoreTarget(nameof(ShoppingListItemDto.Barcodes))]
    private static partial ShoppingListItemDto MapShoppingListItemToDto(ShoppingListItem source);

    // CreateShoppingListRequest -> ShoppingList
    [MapperIgnoreTarget(nameof(ShoppingList.Id))]
    [MapperIgnoreTarget(nameof(ShoppingList.TenantId))]
    [MapperIgnoreTarget(nameof(ShoppingList.CreatedAt))]
    [MapperIgnoreTarget(nameof(ShoppingList.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ShoppingList.ShoppingLocation))]
    [MapperIgnoreTarget(nameof(ShoppingList.Items))]
    public static partial ShoppingList FromCreateRequest(CreateShoppingListRequest source);

    // UpdateShoppingListRequest -> ShoppingList (manual: .Condition on ShoppingLocationId)
    public static ShoppingList ApplyUpdateRequest(UpdateShoppingListRequest source, ShoppingList target)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        if (source.ShoppingLocationId.HasValue)
        {
            target.ShoppingLocationId = source.ShoppingLocationId.Value;
        }
        return target;
    }

    // AddShoppingListItemRequest -> ShoppingListItem
    [MapperIgnoreTarget(nameof(ShoppingListItem.Id))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.TenantId))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.ShoppingListId))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.CreatedAt))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.ShoppingList))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.Product))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.IsPurchased))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.PurchasedAt))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.PurchasedQuantity))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.BestBeforeDate))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.ChildPurchasesJson))]
    public static partial ShoppingListItem FromAddItemRequest(AddShoppingListItemRequest source);

    // UpdateShoppingListItemRequest -> ShoppingListItem
    [MapperIgnoreTarget(nameof(ShoppingListItem.Id))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.TenantId))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.ShoppingListId))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.ProductId))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.ProductName))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.CreatedAt))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.ShoppingList))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.Product))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.IsPurchased))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.PurchasedAt))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.PurchasedQuantity))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.BestBeforeDate))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.Aisle))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.Shelf))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.Department))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.ExternalProductId))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.ChildPurchasesJson))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.ImageUrl))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.Price))]
    [MapperIgnoreTarget(nameof(ShoppingListItem.Barcode))]
    public static partial ShoppingListItem FromUpdateItemRequest(UpdateShoppingListItemRequest source);
}
