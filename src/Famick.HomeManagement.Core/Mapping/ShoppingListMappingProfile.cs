using AutoMapper;
using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class ShoppingListMappingProfile : Profile
{
    public ShoppingListMappingProfile()
    {
        CreateMap<ShoppingList, ShoppingListDto>()
            .ForMember(dest => dest.ShoppingLocationName,
                opt => opt.MapFrom(src => src.ShoppingLocation != null ? src.ShoppingLocation.Name : null))
            .ForMember(dest => dest.HasStoreIntegration,
                opt => opt.MapFrom(src => src.ShoppingLocation != null && !string.IsNullOrEmpty(src.ShoppingLocation.IntegrationType)))
            .ForMember(dest => dest.CanAddToCart,
                opt => opt.MapFrom(src => src.ShoppingLocation != null && !string.IsNullOrEmpty(src.ShoppingLocation.IntegrationType)))
            .ForMember(dest => dest.ItemCount,
                opt => opt.MapFrom(src => src.Items != null ? src.Items.Count : 0))
            .ForMember(dest => dest.PurchasedCount,
                opt => opt.MapFrom(src => src.Items != null ? src.Items.Count(i => i.IsPurchased) : 0));

        CreateMap<ShoppingList, ShoppingListSummaryDto>()
            .ForMember(dest => dest.ShoppingLocationName,
                opt => opt.MapFrom(src => src.ShoppingLocation != null ? src.ShoppingLocation.Name : string.Empty))
            .ForMember(dest => dest.HasStoreIntegration,
                opt => opt.MapFrom(src => src.ShoppingLocation != null && !string.IsNullOrEmpty(src.ShoppingLocation.IntegrationType)))
            .ForMember(dest => dest.TotalItems,
                opt => opt.MapFrom(src => src.Items != null ? src.Items.Count : 0))
            .ForMember(dest => dest.PurchasedItems,
                opt => opt.MapFrom(src => src.Items != null ? src.Items.Count(i => i.IsPurchased) : 0));

        CreateMap<ShoppingListItem, ShoppingListItemDto>()
            .ForMember(dest => dest.ProductName,
                opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : src.ProductName))
            .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.ImageUrl)) // May be overridden with product image in service
            .ForMember(dest => dest.QuantityUnitName,
                opt => opt.MapFrom(src => src.Product != null && src.Product.QuantityUnitPurchase != null
                    ? src.Product.QuantityUnitPurchase.Name : null))
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Price))
            .ForMember(dest => dest.TracksBestBeforeDate,
                opt => opt.MapFrom(src => src.Product != null && src.Product.TracksBestBeforeDate))
            .ForMember(dest => dest.DefaultBestBeforeDays,
                opt => opt.MapFrom(src => src.Product != null ? src.Product.DefaultBestBeforeDays : 0))
            .ForMember(dest => dest.DefaultLocationId,
                opt => opt.MapFrom(src => src.Product != null ? src.Product.LocationId : (Guid?)null))
            // Child product fields - populated by service when needed
            .ForMember(dest => dest.IsParentProduct, opt => opt.Ignore())
            .ForMember(dest => dest.HasChildren, opt => opt.Ignore())
            .ForMember(dest => dest.ChildProductCount, opt => opt.Ignore())
            .ForMember(dest => dest.HasChildrenAtStore, opt => opt.Ignore())
            .ForMember(dest => dest.ChildPurchasedQuantity, opt => opt.Ignore())
            .ForMember(dest => dest.ChildProducts, opt => opt.Ignore())
            .ForMember(dest => dest.ChildPurchases, opt => opt.Ignore())
            .ForMember(dest => dest.Barcodes, opt => opt.Ignore());

        CreateMap<CreateShoppingListRequest, ShoppingList>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ShoppingLocation, opt => opt.Ignore())
            .ForMember(dest => dest.Items, opt => opt.Ignore());

        CreateMap<UpdateShoppingListRequest, ShoppingList>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ShoppingLocationId, opt => opt.Ignore()) // Cannot change store
            .ForMember(dest => dest.ShoppingLocation, opt => opt.Ignore())
            .ForMember(dest => dest.Items, opt => opt.Ignore());

        CreateMap<AddShoppingListItemRequest, ShoppingListItem>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.ShoppingListId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ShoppingList, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore())
            .ForMember(dest => dest.IsPurchased, opt => opt.Ignore())
            .ForMember(dest => dest.PurchasedAt, opt => opt.Ignore())
            .ForMember(dest => dest.PurchasedQuantity, opt => opt.Ignore())
            .ForMember(dest => dest.BestBeforeDate, opt => opt.Ignore())
            .ForMember(dest => dest.ChildPurchasesJson, opt => opt.Ignore());

        CreateMap<UpdateShoppingListItemRequest, ShoppingListItem>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.ShoppingListId, opt => opt.Ignore())
            .ForMember(dest => dest.ProductId, opt => opt.Ignore())
            .ForMember(dest => dest.ProductName, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ShoppingList, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore())
            .ForMember(dest => dest.IsPurchased, opt => opt.Ignore())
            .ForMember(dest => dest.PurchasedAt, opt => opt.Ignore())
            .ForMember(dest => dest.PurchasedQuantity, opt => opt.Ignore())
            .ForMember(dest => dest.BestBeforeDate, opt => opt.Ignore())
            .ForMember(dest => dest.Aisle, opt => opt.Ignore())
            .ForMember(dest => dest.Shelf, opt => opt.Ignore())
            .ForMember(dest => dest.Department, opt => opt.Ignore())
            .ForMember(dest => dest.ExternalProductId, opt => opt.Ignore())
            .ForMember(dest => dest.ChildPurchasesJson, opt => opt.Ignore());
    }
}
