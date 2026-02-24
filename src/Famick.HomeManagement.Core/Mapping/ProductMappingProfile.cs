using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class ProductMappingProfile : Profile
{
    public ProductMappingProfile()
    {
        CreateMap<Product, ProductDto>()
            .ForMember(dest => dest.LocationName,
                opt => opt.MapFrom(src => src.Location.Name))
            .ForMember(dest => dest.QuantityUnitPurchaseName,
                opt => opt.MapFrom(src => src.QuantityUnitPurchase.Name))
            .ForMember(dest => dest.QuantityUnitStockName,
                opt => opt.MapFrom(src => src.QuantityUnitStock.Name))
            .ForMember(dest => dest.ProductGroupName,
                opt => opt.MapFrom(src => src.ProductGroup != null ? src.ProductGroup.Name : null))
            .ForMember(dest => dest.ShoppingLocationName,
                opt => opt.MapFrom(src => src.ShoppingLocation != null ? src.ShoppingLocation.Name : null))
            .ForMember(dest => dest.ParentProductName,
                opt => opt.MapFrom(src => src.ParentProduct != null ? src.ParentProduct.Name : null))
            .ForMember(dest => dest.ChildProductCount,
                opt => opt.MapFrom(src => src.ChildProducts != null ? src.ChildProducts.Count : 0))
            .ForMember(dest => dest.IsParentProduct,
                opt => opt.MapFrom(src => src.ChildProducts != null && src.ChildProducts.Any()))
            .ForMember(dest => dest.ChildProducts,
                opt => opt.MapFrom(src => src.ChildProducts != null
                    ? src.ChildProducts.Select(c => new ProductChildSummaryDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Description = c.Description,
                        TotalStockAmount = 0,
                        QuantityUnitStockName = c.QuantityUnitStock != null ? c.QuantityUnitStock.Name : string.Empty,
                        PrimaryImageUrl = null
                    }).ToList()
                    : new List<ProductChildSummaryDto>()))
            .ForMember(dest => dest.Barcodes,
                opt => opt.MapFrom(src => src.Barcodes))
            .ForMember(dest => dest.Images,
                opt => opt.MapFrom(src => src.Images));

        CreateMap<CreateProductRequest, Product>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Location, opt => opt.Ignore())
            .ForMember(dest => dest.QuantityUnitPurchase, opt => opt.Ignore())
            .ForMember(dest => dest.QuantityUnitStock, opt => opt.Ignore())
            .ForMember(dest => dest.ProductGroup, opt => opt.Ignore())
            .ForMember(dest => dest.ShoppingLocation, opt => opt.Ignore())
            .ForMember(dest => dest.ParentProduct, opt => opt.Ignore())
            .ForMember(dest => dest.ChildProducts, opt => opt.Ignore())
            .ForMember(dest => dest.Barcodes, opt => opt.Ignore())
            .ForMember(dest => dest.Images, opt => opt.Ignore())
            .ForMember(dest => dest.Nutrition, opt => opt.Ignore());

        CreateMap<UpdateProductRequest, Product>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Location, opt => opt.Ignore())
            .ForMember(dest => dest.QuantityUnitPurchase, opt => opt.Ignore())
            .ForMember(dest => dest.QuantityUnitStock, opt => opt.Ignore())
            .ForMember(dest => dest.ProductGroup, opt => opt.Ignore())
            .ForMember(dest => dest.ShoppingLocation, opt => opt.Ignore())
            .ForMember(dest => dest.ParentProduct, opt => opt.Ignore())
            .ForMember(dest => dest.ChildProducts, opt => opt.Ignore())
            .ForMember(dest => dest.Barcodes, opt => opt.Ignore())
            .ForMember(dest => dest.Images, opt => opt.Ignore())
            .ForMember(dest => dest.Nutrition, opt => opt.Ignore());

        CreateMap<ProductBarcode, ProductBarcodeDto>();

        // ProductImage mapping - Note: Url is computed dynamically by the service
        CreateMap<ProductImage, ProductImageDto>()
            .ForMember(dest => dest.Url, opt => opt.Ignore());
    }
}
