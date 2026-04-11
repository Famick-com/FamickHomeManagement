using AutoMapper;
using Famick.HomeManagement.Core.DTOs.ShoppingLocations;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class ShoppingLocationMappingTests
{
    private readonly IMapper _mapper;

    public ShoppingLocationMappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ShoppingLocationMappingProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        // Validation skipped: profiles are tested in isolation
        _mapper = config.CreateMapper();
    }

    #region ShoppingLocation -> ShoppingLocationDto

    [Fact]
    public void ShoppingLocation_To_ShoppingLocationDto_MapsAllProperties()
    {
        var locationId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var location = new ShoppingLocation
        {
            Id = locationId,
            TenantId = Guid.NewGuid(),
            Name = "Kroger Marketplace",
            Description = "Main grocery store",
            IntegrationType = "kroger",
            ExternalLocationId = "ext-loc-123",
            ExternalChainId = "kroger",
            StoreAddress = "123 Main St, Springfield, IL",
            StorePhone = "555-4567",
            Latitude = 39.7817,
            Longitude = -89.6501,
            AisleOrder = new List<string> { "Produce", "Dairy", "Bakery" },
            Type2ItemNumberStart = 2,
            CreatedAt = now.AddDays(-90),
            UpdatedAt = now,
            Products = new List<Product>
            {
                new() { Id = Guid.NewGuid(), Name = "Milk" },
                new() { Id = Guid.NewGuid(), Name = "Eggs" },
                new() { Id = Guid.NewGuid(), Name = "Bread" }
            }
        };

        var dto = _mapper.Map<ShoppingLocationDto>(location);

        dto.Id.Should().Be(locationId);
        dto.Name.Should().Be("Kroger Marketplace");
        dto.Description.Should().Be("Main grocery store");
        dto.ProductCount.Should().Be(3);
        dto.IntegrationType.Should().Be("kroger");
        dto.ExternalLocationId.Should().Be("ext-loc-123");
        dto.ExternalChainId.Should().Be("kroger");
        dto.StoreAddress.Should().Be("123 Main St, Springfield, IL");
        dto.StorePhone.Should().Be("555-4567");
        dto.Latitude.Should().Be(39.7817);
        dto.Longitude.Should().Be(-89.6501);
        dto.AisleOrder.Should().BeEquivalentTo(new[] { "Produce", "Dairy", "Bakery" });
        dto.Type2ItemNumberStart.Should().Be(2);
        dto.CreatedAt.Should().Be(now.AddDays(-90));
        dto.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void ShoppingLocation_To_ShoppingLocationDto_ProductCountFromCollection()
    {
        var location = new ShoppingLocation
        {
            Id = Guid.NewGuid(),
            Name = "Test Store",
            Products = new List<Product>
            {
                new() { Id = Guid.NewGuid() },
                new() { Id = Guid.NewGuid() }
            }
        };

        var dto = _mapper.Map<ShoppingLocationDto>(location);

        dto.ProductCount.Should().Be(2);
    }

    [Fact]
    public void ShoppingLocation_To_ShoppingLocationDto_NullProducts_ReturnsZeroCount()
    {
        var location = new ShoppingLocation
        {
            Id = Guid.NewGuid(),
            Name = "Empty Store",
            Products = null
        };

        var dto = _mapper.Map<ShoppingLocationDto>(location);

        dto.ProductCount.Should().Be(0);
    }

    [Fact]
    public void ShoppingLocation_To_ShoppingLocationDto_IsConnected_IsIgnored()
    {
        var location = new ShoppingLocation
        {
            Id = Guid.NewGuid(),
            Name = "Store"
        };

        var dto = _mapper.Map<ShoppingLocationDto>(location);

        dto.IsConnected.Should().BeFalse();
    }

    #endregion

    #region CreateShoppingLocationRequest -> ShoppingLocation

    [Fact]
    public void CreateShoppingLocationRequest_To_ShoppingLocation_MapsEditableFields()
    {
        var request = new CreateShoppingLocationRequest
        {
            Name = "Walmart Supercenter",
            Description = "24-hour location",
            StoreAddress = "456 Oak Ave",
            StorePhone = "555-9999",
            Latitude = 40.7128,
            Longitude = -74.0060,
            PluginId = "kroger",
            ExternalLocationId = "ext-456",
            ExternalChainId = "ralphs"
        };

        var entity = _mapper.Map<ShoppingLocation>(request);

        entity.Name.Should().Be("Walmart Supercenter");
        entity.Description.Should().Be("24-hour location");
        entity.StoreAddress.Should().Be("456 Oak Ave");
        entity.StorePhone.Should().Be("555-9999");
        entity.Latitude.Should().Be(40.7128);
        entity.Longitude.Should().Be(-74.0060);
        entity.IntegrationType.Should().Be("kroger");
        entity.ExternalLocationId.Should().Be("ext-456");
        entity.ExternalChainId.Should().Be("ralphs");
    }

    [Fact]
    public void CreateShoppingLocationRequest_To_ShoppingLocation_PluginIdMapsToIntegrationType()
    {
        var request = new CreateShoppingLocationRequest
        {
            Name = "Store",
            PluginId = "walmart"
        };

        var entity = _mapper.Map<ShoppingLocation>(request);

        entity.IntegrationType.Should().Be("walmart");
    }

    [Fact]
    public void CreateShoppingLocationRequest_To_ShoppingLocation_IgnoresSystemFields()
    {
        var request = new CreateShoppingLocationRequest
        {
            Name = "Test Store"
        };

        var entity = _mapper.Map<ShoppingLocation>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.Products.Should().BeNull();
        entity.ProductStoreMetadata.Should().BeNull();
    }

    [Fact]
    public void CreateShoppingLocationRequest_To_ShoppingLocation_IgnoresOAuthFields()
    {
        var request = new CreateShoppingLocationRequest
        {
            Name = "Test Store",
            PluginId = "kroger"
        };

        var entity = _mapper.Map<ShoppingLocation>(request);

        entity.OAuthAccessToken.Should().BeNull();
        entity.OAuthRefreshToken.Should().BeNull();
        entity.OAuthTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public void CreateShoppingLocationRequest_To_ShoppingLocation_IgnoresAisleOrder()
    {
        var request = new CreateShoppingLocationRequest
        {
            Name = "Test Store"
        };

        var entity = _mapper.Map<ShoppingLocation>(request);

        entity.AisleOrder.Should().BeNull();
    }

    #endregion

    #region UpdateShoppingLocationRequest -> ShoppingLocation

    [Fact]
    public void UpdateShoppingLocationRequest_To_ShoppingLocation_MapsEditableFields()
    {
        var request = new UpdateShoppingLocationRequest
        {
            Name = "Updated Store Name",
            Description = "Updated description",
            StoreAddress = "789 Elm St",
            StorePhone = "555-0000",
            Latitude = 34.0522,
            Longitude = -118.2437
        };

        var entity = _mapper.Map<ShoppingLocation>(request);

        entity.Name.Should().Be("Updated Store Name");
        entity.Description.Should().Be("Updated description");
        entity.StoreAddress.Should().Be("789 Elm St");
        entity.StorePhone.Should().Be("555-0000");
        entity.Latitude.Should().Be(34.0522);
        entity.Longitude.Should().Be(-118.2437);
    }

    [Fact]
    public void UpdateShoppingLocationRequest_To_ShoppingLocation_IgnoresSystemFields()
    {
        var request = new UpdateShoppingLocationRequest
        {
            Name = "Test"
        };

        var entity = _mapper.Map<ShoppingLocation>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.Products.Should().BeNull();
        entity.ProductStoreMetadata.Should().BeNull();
    }

    [Fact]
    public void UpdateShoppingLocationRequest_To_ShoppingLocation_IgnoresIntegrationFields()
    {
        var request = new UpdateShoppingLocationRequest
        {
            Name = "Test"
        };

        var entity = _mapper.Map<ShoppingLocation>(request);

        entity.IntegrationType.Should().BeNull();
        entity.ExternalLocationId.Should().BeNull();
        entity.ExternalChainId.Should().BeNull();
        entity.OAuthAccessToken.Should().BeNull();
        entity.OAuthRefreshToken.Should().BeNull();
        entity.OAuthTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public void UpdateShoppingLocationRequest_To_ShoppingLocation_IgnoresAisleOrder()
    {
        var request = new UpdateShoppingLocationRequest
        {
            Name = "Test"
        };

        var entity = _mapper.Map<ShoppingLocation>(request);

        entity.AisleOrder.Should().BeNull();
    }

    #endregion
}
