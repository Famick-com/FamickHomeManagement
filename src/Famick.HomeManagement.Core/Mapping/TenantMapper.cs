#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Tenant;
using Famick.HomeManagement.Core.Subscription;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class TenantMapper
{
    public static TenantDto ToDto(Tenant source)
    {
        var dto = ToDtoPartial(source);
        dto.Address = ToHouseholdAddressDto(source);
        dto.SubscriptionTier = source.SubscriptionTier.ToString();
        dto.IsExpired = !SubscriptionFeatureMap.CanWrite(
            source.SubscriptionTier, source.IsTrialActive, source.SubscriptionExpiresAt, DateTime.UtcNow);
        return dto;
    }

    [MapperIgnoreTarget(nameof(TenantDto.SubscriptionTier))]
    [MapperIgnoreTarget(nameof(TenantDto.IsExpired))]
    [MapperIgnoreTarget(nameof(TenantDto.IsUnrestricted))]
    [MapperIgnoreTarget(nameof(TenantDto.Address))]
    private static partial TenantDto ToDtoPartial(Tenant source);

    /// <summary>
    /// The household's address: the shared row plus the unit lines the household keeps for
    /// itself. Null when the household has no address.
    /// </summary>
    public static AddressDto? ToHouseholdAddressDto(Tenant source)
    {
        if (source.Address == null)
            return null;

        var dto = ToAddressDto(source.Address);
        dto.AddressLine2 = source.AddressLine2;
        dto.AddressLine3 = source.AddressLine3;
        dto.AddressLine4 = source.AddressLine4;
        return dto;
    }

    /// <summary>
    /// A shared row alone. Lines 2-4 belong to whoever references the row, so the caller
    /// fills them in.
    /// </summary>
    [MapperIgnoreTarget(nameof(AddressDto.AddressLine2))]
    [MapperIgnoreTarget(nameof(AddressDto.AddressLine3))]
    [MapperIgnoreTarget(nameof(AddressDto.AddressLine4))]
    [MapperIgnoreTarget(nameof(AddressDto.SuggestedLine2))]
    public static partial AddressDto ToAddressDto(Address source);
}
