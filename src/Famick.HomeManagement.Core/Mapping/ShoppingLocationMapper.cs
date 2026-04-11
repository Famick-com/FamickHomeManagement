#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.ShoppingLocations;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class ShoppingLocationMapper
{
    public static ShoppingLocationDto ToDto(ShoppingLocation source)
    {
        var dto = ToDtoPartial(source);
        dto.ProductCount = source.Products != null ? source.Products.Count : 0;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ShoppingLocationDto.ProductCount))]
    [MapperIgnoreTarget(nameof(ShoppingLocationDto.IsConnected))]
    private static partial ShoppingLocationDto ToDtoPartial(ShoppingLocation source);

    [MapperIgnoreTarget(nameof(ShoppingLocation.Id))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.TenantId))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.CreatedAt))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.Products))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.ProductStoreMetadata))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.OAuthAccessToken))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.OAuthRefreshToken))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.OAuthTokenExpiresAt))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.AisleOrder))]
    [MapProperty(nameof(CreateShoppingLocationRequest.PluginId), nameof(ShoppingLocation.IntegrationType))]
    public static partial ShoppingLocation FromCreateRequest(CreateShoppingLocationRequest source);

    [MapperIgnoreTarget(nameof(ShoppingLocation.Id))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.TenantId))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.CreatedAt))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.Products))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.ProductStoreMetadata))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.IntegrationType))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.ExternalLocationId))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.ExternalChainId))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.OAuthAccessToken))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.OAuthRefreshToken))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.OAuthTokenExpiresAt))]
    [MapperIgnoreTarget(nameof(ShoppingLocation.AisleOrder))]
    public static partial void Update(UpdateShoppingLocationRequest source, ShoppingLocation target);
}
