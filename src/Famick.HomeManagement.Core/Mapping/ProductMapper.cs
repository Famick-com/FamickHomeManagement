#pragma warning disable RMG020 // Unmapped source member
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class ProductMapper
{
    // Product -> ProductDto (computed fields: navigation names, ChildProducts inline, OverriddenFields deserialization)
    public static ProductDto ToDto(Product source)
    {
        var dto = MapProductToDto(source);
        dto.LocationName = source.Location.Name;
        dto.QuantityUnitPurchaseName = source.QuantityUnitPurchase.Name;
        dto.QuantityUnitStockName = source.QuantityUnitStock.Name;
        dto.ProductGroupName = source.ProductGroup != null ? source.ProductGroup.Name : null;
        dto.ShoppingLocationName = source.ShoppingLocation != null ? source.ShoppingLocation.Name : null;
        dto.ParentProductName = source.ParentProduct != null ? source.ParentProduct.Name : null;
        dto.ChildProductCount = source.ChildProducts != null ? source.ChildProducts.Count : 0;
        dto.IsParentProduct = source.ChildProducts != null && source.ChildProducts.Any();
        dto.ChildProducts = source.ChildProducts != null
            ? source.ChildProducts.Select(c => new ProductChildSummaryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                TotalStockAmount = 0,
                QuantityUnitStockName = c.QuantityUnitStock != null ? c.QuantityUnitStock.Name : string.Empty,
                PrimaryImageUrl = null
            }).ToList()
            : new List<ProductChildSummaryDto>();
        dto.MasterProductName = source.MasterProduct != null ? source.MasterProduct.Name : null;
        dto.OverriddenFields = DeserializeOverriddenFields(source.OverriddenFields);
        return dto;
    }

    [MapperIgnoreTarget(nameof(ProductDto.LocationName))]
    [MapperIgnoreTarget(nameof(ProductDto.QuantityUnitPurchaseName))]
    [MapperIgnoreTarget(nameof(ProductDto.QuantityUnitStockName))]
    [MapperIgnoreTarget(nameof(ProductDto.ProductGroupName))]
    [MapperIgnoreTarget(nameof(ProductDto.ShoppingLocationName))]
    [MapperIgnoreTarget(nameof(ProductDto.ParentProductName))]
    [MapperIgnoreTarget(nameof(ProductDto.ChildProductCount))]
    [MapperIgnoreTarget(nameof(ProductDto.IsParentProduct))]
    [MapperIgnoreTarget(nameof(ProductDto.ChildProducts))]
    [MapperIgnoreTarget(nameof(ProductDto.MasterProductName))]
    [MapperIgnoreTarget(nameof(ProductDto.OverriddenFields))]
    [MapperIgnoreTarget(nameof(ProductDto.TotalStockAmount))]
    [MapperIgnoreTarget(nameof(ProductDto.StockByLocation))]
    [MapperIgnoreTarget(nameof(ProductDto.MasterProductImageUrl))]
    private static partial ProductDto MapProductToDto(Product source);

    // CreateProductRequest -> Product
    [MapperIgnoreTarget(nameof(Product.Id))]
    [MapperIgnoreTarget(nameof(Product.TenantId))]
    [MapperIgnoreTarget(nameof(Product.CreatedAt))]
    [MapperIgnoreTarget(nameof(Product.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Product.OverriddenFields))]
    [MapperIgnoreTarget(nameof(Product.Location))]
    [MapperIgnoreTarget(nameof(Product.QuantityUnitPurchase))]
    [MapperIgnoreTarget(nameof(Product.QuantityUnitStock))]
    [MapperIgnoreTarget(nameof(Product.ProductGroup))]
    [MapperIgnoreTarget(nameof(Product.ShoppingLocation))]
    [MapperIgnoreTarget(nameof(Product.ParentProduct))]
    [MapperIgnoreTarget(nameof(Product.ChildProducts))]
    [MapperIgnoreTarget(nameof(Product.MasterProduct))]
    [MapperIgnoreTarget(nameof(Product.Barcodes))]
    [MapperIgnoreTarget(nameof(Product.Images))]
    [MapperIgnoreTarget(nameof(Product.Nutrition))]
    [MapperIgnoreTarget(nameof(Product.StoreMetadata))]
    [MapperIgnoreTarget(nameof(Product.Allergens))]
    [MapperIgnoreTarget(nameof(Product.DietaryConflicts))]
    [MapperIgnoreTarget(nameof(Product.DataSourceAttribution))]
    public static partial Product FromCreateRequest(CreateProductRequest source);

    // UpdateProductRequest -> Product
    [MapperIgnoreTarget(nameof(Product.Id))]
    [MapperIgnoreTarget(nameof(Product.TenantId))]
    [MapperIgnoreTarget(nameof(Product.CreatedAt))]
    [MapperIgnoreTarget(nameof(Product.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Product.MasterProductId))]
    [MapperIgnoreTarget(nameof(Product.OverriddenFields))]
    [MapperIgnoreTarget(nameof(Product.Location))]
    [MapperIgnoreTarget(nameof(Product.QuantityUnitPurchase))]
    [MapperIgnoreTarget(nameof(Product.QuantityUnitStock))]
    [MapperIgnoreTarget(nameof(Product.ProductGroup))]
    [MapperIgnoreTarget(nameof(Product.ShoppingLocation))]
    [MapperIgnoreTarget(nameof(Product.ParentProduct))]
    [MapperIgnoreTarget(nameof(Product.ChildProducts))]
    [MapperIgnoreTarget(nameof(Product.MasterProduct))]
    [MapperIgnoreTarget(nameof(Product.Barcodes))]
    [MapperIgnoreTarget(nameof(Product.Images))]
    [MapperIgnoreTarget(nameof(Product.Nutrition))]
    [MapperIgnoreTarget(nameof(Product.StoreMetadata))]
    [MapperIgnoreTarget(nameof(Product.Allergens))]
    [MapperIgnoreTarget(nameof(Product.DietaryConflicts))]
    [MapperIgnoreTarget(nameof(Product.DataSourceAttribution))]
    public static partial Product FromUpdateRequest(UpdateProductRequest source);

    // ProductBarcode -> ProductBarcodeDto
    public static partial ProductBarcodeDto ToBarcodeDto(ProductBarcode source);

    // ProductImage -> ProductImageDto (Url is computed by the service)
    [MapperIgnoreTarget(nameof(ProductImageDto.Url))]
    public static partial ProductImageDto ToImageDto(ProductImage source);

    private static List<string> DeserializeOverriddenFields(string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]")
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
