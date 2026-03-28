using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.StoreIntegrations;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Unit tests for store integration caching in StoreIntegrationService.
/// Tests cache hit/miss behavior for SearchProductsAtStoreAsync and GetProductAtStoreAsync.
/// </summary>
public class StoreIntegrationCacheTests
{
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _shoppingLocationId = Guid.NewGuid();
    private readonly MemoryDistributedCache _cache;
    private readonly Mock<ITenantProvider> _mockTenantProvider;

    public StoreIntegrationCacheTests()
    {
        _cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));

        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
    }

    #region SearchProductsAtStoreAsync - Cache Hits

    [Fact]
    public async Task SearchProductsAtStoreAsync_CacheHit_ReturnsCachedResults()
    {
        // Arrange
        var service = CreateService();
        var query = "milk";
        var maxResults = 20;

        // Seed cache with results
        var cachedResults = new List<StoreProductResult>
        {
            new() { ExternalProductId = "123", Name = "Whole Milk", Price = 3.99m },
            new() { ExternalProductId = "456", Name = "2% Milk", Price = 3.49m }
        };

        var cacheKey = $"store-search:{_tenantId}:{_shoppingLocationId}:{query}:{maxResults}";
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(cachedResults));

        // Act
        var results = await service.SearchProductsAtStoreAsync(
            _shoppingLocationId,
            new StoreProductSearchRequest { Query = query, MaxResults = maxResults });

        // Assert: Should return cached results without hitting the plugin
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Whole Milk");
        results[0].Price.Should().Be(3.99m);
        results[1].Name.Should().Be("2% Milk");
    }

    [Fact]
    public async Task SearchProductsAtStoreAsync_CacheHit_NormalizesQueryCase()
    {
        // Arrange
        var service = CreateService();

        // Cache with lowercase key
        var cacheKey = $"store-search:{_tenantId}:{_shoppingLocationId}:milk:20";
        var cachedResults = new List<StoreProductResult>
        {
            new() { ExternalProductId = "123", Name = "Milk" }
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(cachedResults));

        // Act: Search with uppercase
        var results = await service.SearchProductsAtStoreAsync(
            _shoppingLocationId,
            new StoreProductSearchRequest { Query = "MILK", MaxResults = 20 });

        // Assert: Should match the cached entry
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Milk");
    }

    [Fact]
    public async Task SearchProductsAtStoreAsync_CacheHit_TrimsQuery()
    {
        // Arrange
        var service = CreateService();

        var cacheKey = $"store-search:{_tenantId}:{_shoppingLocationId}:milk:20";
        var cachedResults = new List<StoreProductResult>
        {
            new() { ExternalProductId = "123", Name = "Milk" }
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(cachedResults));

        // Act: Search with whitespace
        var results = await service.SearchProductsAtStoreAsync(
            _shoppingLocationId,
            new StoreProductSearchRequest { Query = "  milk  ", MaxResults = 20 });

        // Assert
        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchProductsAtStoreAsync_DifferentLocations_UseDifferentCacheKeys()
    {
        // Arrange
        var service = CreateService();
        var location1 = Guid.NewGuid();
        var location2 = Guid.NewGuid();

        var cached1 = new List<StoreProductResult>
        {
            new() { ExternalProductId = "loc1", Name = "Location 1 Milk" }
        };
        var cached2 = new List<StoreProductResult>
        {
            new() { ExternalProductId = "loc2", Name = "Location 2 Milk" }
        };

        await _cache.SetStringAsync(
            $"store-search:{_tenantId}:{location1}:milk:20",
            JsonSerializer.Serialize(cached1));
        await _cache.SetStringAsync(
            $"store-search:{_tenantId}:{location2}:milk:20",
            JsonSerializer.Serialize(cached2));

        // Act
        var results1 = await service.SearchProductsAtStoreAsync(
            location1, new StoreProductSearchRequest { Query = "milk", MaxResults = 20 });
        var results2 = await service.SearchProductsAtStoreAsync(
            location2, new StoreProductSearchRequest { Query = "milk", MaxResults = 20 });

        // Assert
        results1.Should().HaveCount(1);
        results1[0].Name.Should().Be("Location 1 Milk");
        results2.Should().HaveCount(1);
        results2[0].Name.Should().Be("Location 2 Milk");
    }

    #endregion

    #region GetProductAtStoreAsync - Cache Hits

    [Fact]
    public async Task GetProductAtStoreAsync_CacheHit_ReturnsCachedResult()
    {
        // Arrange
        var service = CreateService();
        var externalProductId = "PROD-123";

        var cachedProduct = new StoreProductResult
        {
            ExternalProductId = externalProductId,
            Name = "Organic Milk",
            Price = 5.99m,
            Aisle = "Dairy A3",
            InStock = true
        };

        var cacheKey = $"store-product:{_tenantId}:{_shoppingLocationId}:{externalProductId}";
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(cachedProduct));

        // Act
        var result = await service.GetProductAtStoreAsync(_shoppingLocationId, externalProductId);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Organic Milk");
        result.Price.Should().Be(5.99m);
        result.Aisle.Should().Be("Dairy A3");
        result.InStock.Should().BeTrue();
    }

    #endregion

    #region Cache Isolation by Tenant

    [Fact]
    public async Task SearchProductsAtStoreAsync_CacheIsIsolatedByTenant()
    {
        // Arrange
        var tenant1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tenant2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var locationId = Guid.NewGuid();

        // Cache for tenant 1
        await _cache.SetStringAsync(
            $"store-search:{tenant1}:{locationId}:milk:20",
            JsonSerializer.Serialize(new List<StoreProductResult>
            {
                new() { ExternalProductId = "t1", Name = "Tenant 1 Milk" }
            }));

        // Cache for tenant 2
        await _cache.SetStringAsync(
            $"store-search:{tenant2}:{locationId}:milk:20",
            JsonSerializer.Serialize(new List<StoreProductResult>
            {
                new() { ExternalProductId = "t2", Name = "Tenant 2 Milk" }
            }));

        // Act: Query as tenant 1
        var mockTenant1 = new Mock<ITenantProvider>();
        mockTenant1.Setup(t => t.TenantId).Returns(tenant1);
        var service1 = CreateService(tenantProvider: mockTenant1.Object);
        var results1 = await service1.SearchProductsAtStoreAsync(
            locationId, new StoreProductSearchRequest { Query = "milk", MaxResults = 20 });

        // Act: Query as tenant 2
        var mockTenant2 = new Mock<ITenantProvider>();
        mockTenant2.Setup(t => t.TenantId).Returns(tenant2);
        var service2 = CreateService(tenantProvider: mockTenant2.Object);
        var results2 = await service2.SearchProductsAtStoreAsync(
            locationId, new StoreProductSearchRequest { Query = "milk", MaxResults = 20 });

        // Assert
        results1.Should().HaveCount(1);
        results1[0].Name.Should().Be("Tenant 1 Milk");
        results2.Should().HaveCount(1);
        results2[0].Name.Should().Be("Tenant 2 Milk");
    }

    #endregion

    #region Cache Key Structure

    [Fact]
    public async Task SearchProductsAtStoreAsync_DifferentMaxResults_UseDifferentCacheKeys()
    {
        // Arrange
        var service = CreateService();

        await _cache.SetStringAsync(
            $"store-search:{_tenantId}:{_shoppingLocationId}:milk:5",
            JsonSerializer.Serialize(new List<StoreProductResult>
            {
                new() { ExternalProductId = "1", Name = "Result for 5" }
            }));

        await _cache.SetStringAsync(
            $"store-search:{_tenantId}:{_shoppingLocationId}:milk:20",
            JsonSerializer.Serialize(new List<StoreProductResult>
            {
                new() { ExternalProductId = "1", Name = "Result for 20" },
                new() { ExternalProductId = "2", Name = "Second result" }
            }));

        // Act
        var results5 = await service.SearchProductsAtStoreAsync(
            _shoppingLocationId, new StoreProductSearchRequest { Query = "milk", MaxResults = 5 });
        var results20 = await service.SearchProductsAtStoreAsync(
            _shoppingLocationId, new StoreProductSearchRequest { Query = "milk", MaxResults = 20 });

        // Assert
        results5.Should().HaveCount(1);
        results5[0].Name.Should().Be("Result for 5");
        results20.Should().HaveCount(2);
    }

    #endregion

    #region Helper Methods

    private StoreIntegrationService CreateService(ITenantProvider? tenantProvider = null)
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new HomeManagementDbContext(options, null);

        return new StoreIntegrationService(
            context,
            Mock.Of<IPluginLoader>(),
            tenantProvider ?? _mockTenantProvider.Object,
            _cache,
            Mock.Of<ILogger<StoreIntegrationService>>());
    }

    #endregion
}
