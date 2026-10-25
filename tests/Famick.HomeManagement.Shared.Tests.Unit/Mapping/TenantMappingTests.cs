using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Tenant;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class TenantMappingTests
{
    [Fact]
    public void Tenant_To_TenantDto_MapsSubscriptionTierAsString()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Household",
            SubscriptionTier = SubscriptionTier.Free,
            TrialEndsAt = null // no trial = IsTrialActive is false
        };

        var dto = TenantMapper.ToDto(tenant);

        dto.Id.Should().Be(tenant.Id);
        dto.Name.Should().Be("Test Household");
        dto.SubscriptionTier.Should().Be("Free");
        dto.IsExpired.Should().BeTrue(); // Free + not trial = expired
    }

    [Fact]
    public void Tenant_To_TenantDto_FreeWithTrial_NotExpired()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Trial Household",
            SubscriptionTier = SubscriptionTier.Free,
            TrialEndsAt = DateTime.UtcNow.AddDays(30) // future = IsTrialActive is true
        };

        var dto = TenantMapper.ToDto(tenant);

        dto.IsExpired.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(30, false)]
    public void Tenant_To_TenantDto_PaidHousehold_IsExpiredOnceItsSubscriptionEnds(int expiresInDays, bool expired)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Paid Household",
            SubscriptionTier = SubscriptionTier.Organize,
            SubscriptionExpiresAt = DateTime.UtcNow.AddDays(expiresInDays)
        };

        TenantMapper.ToDto(tenant).IsExpired.Should().Be(expired);
    }

    [Fact]
    public void Tenant_To_TenantDto_PaidHouseholdWithNoEndDate_NotExpired()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Paid Household", SubscriptionTier = SubscriptionTier.Home };

        TenantMapper.ToDto(tenant).IsExpired.Should().BeFalse();
    }

    [Fact]
    public void Address_To_AddressDto_MapsAllFields()
    {
        var address = new Address
        {
            Id = Guid.NewGuid(),
            AddressLine1 = "123 Main St",
            City = "Hamilton",
            StateProvince = "OH",
            PostalCode = "45015",
            Country = "United States",
            CountryCode = "US",
            Latitude = 39.3995,
            Longitude = -84.5613,
            ProviderPlaceId = "abc123",
            FormattedAddress = "123 Main St, Hamilton, OH 45015",
            NormalizedHash = "hash123"
        };

        var dto = TenantMapper.ToAddressDto(address);

        dto.Id.Should().Be(address.Id);
        dto.AddressLine1.Should().Be("123 Main St");
        dto.AddressLine2.Should().BeNull("unit lines belong to whoever references the row");
        dto.City.Should().Be("Hamilton");
        dto.StateProvince.Should().Be("OH");
        dto.PostalCode.Should().Be("45015");
        dto.Country.Should().Be("United States");
        dto.CountryCode.Should().Be("US");
        dto.Latitude.Should().Be(39.3995);
        dto.Longitude.Should().Be(-84.5613);
        dto.ProviderPlaceId.Should().Be("abc123");
        dto.FormattedAddress.Should().Be("123 Main St, Hamilton, OH 45015");
    }

    [Fact]
    public void Tenant_To_TenantDto_AddsTheHouseholdsOwnUnitLines()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Unit Household",
            Address = new Address { Id = Guid.NewGuid(), AddressLine1 = "1 Shared Way", City = "Dayton" },
            AddressLine2 = "Apt 2",
            AddressLine3 = "c/o Smith",
            AddressLine4 = "Rear entrance"
        };

        var dto = TenantMapper.ToDto(tenant);

        dto.Address!.AddressLine1.Should().Be("1 Shared Way");
        dto.Address.AddressLine2.Should().Be("Apt 2");
        dto.Address.AddressLine3.Should().Be("c/o Smith");
        dto.Address.AddressLine4.Should().Be("Rear entrance");
    }
}
