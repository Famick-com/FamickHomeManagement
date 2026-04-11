#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.ProductGroups;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class ProductGroupMapper
{
    public static ProductGroupDto ToDto(ProductGroup source)
    {
        var dto = ToDtoPartial(source);
        dto.ProductCount = source.Products != null ? source.Products.Count : 0;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ProductGroupDto.ProductCount))]
    private static partial ProductGroupDto ToDtoPartial(ProductGroup source);

    [MapperIgnoreTarget(nameof(ProductGroup.Id))]
    [MapperIgnoreTarget(nameof(ProductGroup.TenantId))]
    [MapperIgnoreTarget(nameof(ProductGroup.CreatedAt))]
    [MapperIgnoreTarget(nameof(ProductGroup.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ProductGroup.Products))]
    public static partial ProductGroup FromCreateRequest(CreateProductGroupRequest source);

    [MapperIgnoreTarget(nameof(ProductGroup.Id))]
    [MapperIgnoreTarget(nameof(ProductGroup.TenantId))]
    [MapperIgnoreTarget(nameof(ProductGroup.CreatedAt))]
    [MapperIgnoreTarget(nameof(ProductGroup.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ProductGroup.Products))]
    public static partial void Update(UpdateProductGroupRequest source, ProductGroup target);

    public static ProductSummaryDto ToProductSummaryDto(Product source)
    {
        var dto = ToProductSummaryDtoPartial(source);
        dto.ProductGroupName = source.ProductGroup != null ? source.ProductGroup.Name : null;
        dto.ShoppingLocationName = source.ShoppingLocation != null ? source.ShoppingLocation.Name : null;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ProductSummaryDto.ProductGroupName))]
    [MapperIgnoreTarget(nameof(ProductSummaryDto.ShoppingLocationName))]
    private static partial ProductSummaryDto ToProductSummaryDtoPartial(Product source);
}
