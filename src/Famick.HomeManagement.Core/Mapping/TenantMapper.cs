#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Tenant;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class TenantMapper
{
    public static TenantDto ToDto(Tenant source)
    {
        var dto = ToDtoPartial(source);
        dto.SubscriptionTier = source.SubscriptionTier.ToString();
        dto.IsExpired = source.SubscriptionTier == Domain.Enums.SubscriptionTier.Free && !source.IsTrialActive;
        return dto;
    }

    [MapperIgnoreTarget(nameof(TenantDto.SubscriptionTier))]
    [MapperIgnoreTarget(nameof(TenantDto.IsExpired))]
    private static partial TenantDto ToDtoPartial(Tenant source);

    public static partial AddressDto ToAddressDto(Address source);

    [MapperIgnoreTarget(nameof(Address.Id))]
    [MapperIgnoreTarget(nameof(Address.NormalizedHash))]
    [MapperIgnoreTarget(nameof(Address.CreatedAt))]
    [MapperIgnoreTarget(nameof(Address.UpdatedAt))]
    public static partial Address FromCreateAddressRequest(CreateAddressRequest source);

    [MapperIgnoreTarget(nameof(Address.Id))]
    [MapperIgnoreTarget(nameof(Address.NormalizedHash))]
    [MapperIgnoreTarget(nameof(Address.CreatedAt))]
    [MapperIgnoreTarget(nameof(Address.UpdatedAt))]
    public static partial void UpdateAddress(UpdateAddressRequest source, Address target);

    [MapperIgnoreTarget(nameof(Address.Id))]
    [MapperIgnoreTarget(nameof(Address.NormalizedHash))]
    [MapperIgnoreTarget(nameof(Address.CreatedAt))]
    [MapperIgnoreTarget(nameof(Address.UpdatedAt))]
    public static partial Address FromNormalizedAddressResult(NormalizedAddressResult source);
}
