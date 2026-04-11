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

    [Fact]
    public void Address_To_AddressDto_MapsAllFields()
    {
        var address = new Address
        {
            Id = Guid.NewGuid(),
            AddressLine1 = "123 Main St",
            AddressLine2 = "Apt 4",
            City = "Hamilton",
            StateProvince = "OH",
            PostalCode = "45015",
            Country = "United States",
            CountryCode = "US",
            Latitude = 39.3995,
            Longitude = -84.5613,
            GeoapifyPlaceId = "abc123",
            FormattedAddress = "123 Main St, Hamilton, OH 45015",
            NormalizedHash = "hash123"
        };

        var dto = TenantMapper.ToAddressDto(address);

        dto.Id.Should().Be(address.Id);
        dto.AddressLine1.Should().Be("123 Main St");
        dto.AddressLine2.Should().Be("Apt 4");
        dto.City.Should().Be("Hamilton");
        dto.StateProvince.Should().Be("OH");
        dto.PostalCode.Should().Be("45015");
        dto.Country.Should().Be("United States");
        dto.CountryCode.Should().Be("US");
        dto.Latitude.Should().Be(39.3995);
        dto.Longitude.Should().Be(-84.5613);
        dto.GeoapifyPlaceId.Should().Be("abc123");
        dto.FormattedAddress.Should().Be("123 Main St, Hamilton, OH 45015");
    }

    [Fact]
    public void CreateAddressRequest_To_Address_IgnoresSystemFields()
    {
        var request = new CreateAddressRequest
        {
            AddressLine1 = "456 Oak Ave",
            City = "Cincinnati",
            StateProvince = "OH",
            PostalCode = "45202"
        };

        var entity = TenantMapper.FromCreateAddressRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.NormalizedHash.Should().BeNull();
        entity.AddressLine1.Should().Be("456 Oak Ave");
        entity.City.Should().Be("Cincinnati");
    }

    [Fact]
    public void UpdateAddressRequest_To_Address_IgnoresSystemFields()
    {
        var request = new UpdateAddressRequest
        {
            AddressLine1 = "789 Pine Rd",
            City = "Dayton"
        };

        var entity = new Address();
        TenantMapper.UpdateAddress(request, entity);

        entity.Id.Should().Be(Guid.Empty);
        entity.NormalizedHash.Should().BeNull();
        entity.AddressLine1.Should().Be("789 Pine Rd");
    }

    [Fact]
    public void NormalizedAddressResult_To_Address_IgnoresSystemFields()
    {
        var result = new NormalizedAddressResult
        {
            AddressLine1 = "123 Normalized St",
            City = "Columbus",
            StateProvince = "OH",
            PostalCode = "43215",
            Latitude = 39.96,
            Longitude = -82.99,
            GeoapifyPlaceId = "geo-place-id"
        };

        var entity = TenantMapper.FromNormalizedAddressResult(result);

        entity.Id.Should().Be(Guid.Empty);
        entity.NormalizedHash.Should().BeNull();
        entity.AddressLine1.Should().Be("123 Normalized St");
        entity.GeoapifyPlaceId.Should().Be("geo-place-id");
        entity.Latitude.Should().Be(39.96);
    }
}
