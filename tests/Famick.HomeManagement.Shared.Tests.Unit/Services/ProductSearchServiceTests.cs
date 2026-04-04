using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
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
using AutoMapper;
using Famick.HomeManagement.Core.Mapping;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Unit tests for ProductSearchService.
/// Note: EF query tests (SearchAsync, AutocompleteAsync, etc.) require PostgreSQL
/// due to EF.Functions.ILike() and belong in integration tests. These unit tests
/// cover caching, invalidation, input validation, and non-EF logic.
/// </summary>
public class ProductSearchServiceTests
{
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly MemoryDistributedCache _cache;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly Mock<IFileUrlService> _mockFileUrlService;
    private readonly Mock<IMasterProductImageResolver> _mockImageResolver;
    private readonly Mock<IDbContextFactory<HomeManagementDbContext>> _mockContextFactory;
    private readonly IMapper _mapper;

    public ProductSearchServiceTests()
    {
        _cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));

        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        _mockFileUrlService = new Mock<IFileUrlService>();
        _mockImageResolver = new Mock<IMasterProductImageResolver>();
        _mockContextFactory = new Mock<IDbContextFactory<HomeManagementDbContext>>();

        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ProductMappingProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        _mapper = mapperConfig.CreateMapper();
    }


    #region SearchAsync - Input Validation

    [Fact]
    public async Task SearchAsync_WithNullSearchTerm_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.SearchAsync(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WithEmptySearchTerm_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.SearchAsync("");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WithWhitespaceSearchTerm_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.SearchAsync("   ");

        result.Should().BeEmpty();
    }

    #endregion

    #region AutocompleteAsync - Input Validation

    [Fact]
    public async Task AutocompleteAsync_WithNullSearchTerm_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.AutocompleteAsync(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AutocompleteAsync_WithEmptySearchTerm_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.AutocompleteAsync("");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AutocompleteAsync_WithWhitespaceSearchTerm_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.AutocompleteAsync("   ");

        result.Should().BeEmpty();
    }

    #endregion

    #region AutocompleteAsync - Caching

    [Fact]
    public async Task AutocompleteAsync_CachesResults_SecondCallReturnsCached()
    {
        // Arrange: Set up cache with a known value
        var service = CreateService();

        // Seed the version key so cache keys are deterministic
        var versionKey = $"product-ac-version:{_tenantId}";
        await _cache.SetStringAsync(versionKey, "42");

        // Manually set a cached value
        var cacheKey = $"product-ac:{_tenantId}:v42:milk:10";
        var cachedData = System.Text.Json.JsonSerializer.Serialize(new List<ProductAutocompleteDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Whole Milk", ProductGroupName = "Dairy" },
            new() { Id = Guid.NewGuid(), Name = "Skim Milk", ProductGroupName = "Dairy" }
        });
        await _cache.SetStringAsync(cacheKey, cachedData);

        // Act
        var result = await service.AutocompleteAsync("milk", 10);

        // Assert: Should return cached results without hitting the database
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Whole Milk");
        result[1].Name.Should().Be("Skim Milk");
    }

    [Fact]
    public async Task AutocompleteAsync_NormalizesSearchTerm_CaseInsensitive()
    {
        // Arrange
        var service = CreateService();

        var versionKey = $"product-ac-version:{_tenantId}";
        await _cache.SetStringAsync(versionKey, "1");

        // Cache with lowercase key
        var cacheKey = $"product-ac:{_tenantId}:v1:milk:10";
        var cachedData = System.Text.Json.JsonSerializer.Serialize(new List<ProductAutocompleteDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Milk" }
        });
        await _cache.SetStringAsync(cacheKey, cachedData);

        // Act: Search with uppercase
        var result = await service.AutocompleteAsync("MILK", 10);

        // Assert: Should find the cached entry because term is lowercased
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Milk");
    }

    [Fact]
    public async Task AutocompleteAsync_DifferentMaxResults_UsesDifferentCacheKey()
    {
        // Arrange
        var service = CreateService();

        var versionKey = $"product-ac-version:{_tenantId}";
        await _cache.SetStringAsync(versionKey, "1");

        // Cache with maxResults=5
        var cacheKey5 = $"product-ac:{_tenantId}:v1:milk:5";
        var cachedData5 = System.Text.Json.JsonSerializer.Serialize(new List<ProductAutocompleteDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Cached with 5" }
        });
        await _cache.SetStringAsync(cacheKey5, cachedData5);

        // Cache with maxResults=10
        var cacheKey10 = $"product-ac:{_tenantId}:v1:milk:10";
        var cachedData10 = System.Text.Json.JsonSerializer.Serialize(new List<ProductAutocompleteDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Cached with 10" },
            new() { Id = Guid.NewGuid(), Name = "Second result" }
        });
        await _cache.SetStringAsync(cacheKey10, cachedData10);

        // Act
        var result5 = await service.AutocompleteAsync("milk", 5);
        var result10 = await service.AutocompleteAsync("milk", 10);

        // Assert
        result5.Should().HaveCount(1);
        result5[0].Name.Should().Be("Cached with 5");
        result10.Should().HaveCount(2);
        result10[0].Name.Should().Be("Cached with 10");
    }

    #endregion

    #region InvalidateCache

    [Fact]
    public async Task InvalidateCache_IncreasesVersion_OldCacheEntriesNoLongerHit()
    {
        // Arrange
        var service = CreateService();

        var versionKey = $"product-ac-version:{_tenantId}";
        await _cache.SetStringAsync(versionKey, "1");

        // Seed old cache entry
        var oldCacheKey = $"product-ac:{_tenantId}:v1:milk:10";
        var cachedData = System.Text.Json.JsonSerializer.Serialize(new List<ProductAutocompleteDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Old Result" }
        });
        await _cache.SetStringAsync(oldCacheKey, cachedData);

        // Act: Invalidate cache
        service.InvalidateCache();

        // Assert: Version should have changed
        var newVersion = await _cache.GetStringAsync(versionKey);
        newVersion.Should().NotBe("1");

        // The old cache key should still exist but won't be found because
        // the new version generates a different cache key
        var oldEntry = await _cache.GetStringAsync(oldCacheKey);
        oldEntry.Should().NotBeNull("old entry is still in cache, just unreachable via new version key");
    }

    [Fact]
    public void InvalidateCache_WithNoTenant_DoesNotThrow()
    {
        // Arrange
        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(t => t.TenantId).Returns((Guid?)null);

        var service = CreateService(tenantProvider: mockTenantProvider.Object);

        // Act & Assert: Should not throw
        var action = () => service.InvalidateCache();
        action.Should().NotThrow();
    }

    [Fact]
    public async Task InvalidateCache_CalledTwice_VersionChangesEachTime()
    {
        // Arrange
        var service = CreateService();
        var versionKey = $"product-ac-version:{_tenantId}";

        // Act
        service.InvalidateCache();
        var version1 = await _cache.GetStringAsync(versionKey);

        // Small delay to ensure different tick value
        await Task.Delay(1);

        service.InvalidateCache();
        var version2 = await _cache.GetStringAsync(versionKey);

        // Assert
        version1.Should().NotBeNull();
        version2.Should().NotBeNull();
        version2.Should().NotBe(version1, "each invalidation should produce a new version");
    }

    #endregion

    #region SearchLocalForLookupAsync - Input Handling

    [Fact]
    public async Task SearchLocalForLookupAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Arrange: Use InMemory DB with no products
        // Note: Name search requires ILike / PostgreSQL, so we test barcode path here.
        // Full search behavior should be tested in integration tests.
        var context = CreateInMemoryContext();
        var service = CreateService(context);

        var result = await service.SearchLocalForLookupAsync("012345678905", 20);
        result.Should().BeEmpty();
    }

    #endregion

    #region ProductsService Delegation Tests

    [Fact]
    public async Task ProductsService_SearchAsync_DelegatesToSearchService()
    {
        // Arrange
        var mockSearchService = new Mock<IProductSearchService>();
        var expectedResults = new List<ProductDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Test Product" }
        };

        mockSearchService
            .Setup(s => s.SearchAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var productsService = CreateProductsService(mockSearchService.Object);

        // Act
        var result = await productsService.SearchAsync("test");

        // Assert
        result.Should().BeEquivalentTo(expectedResults);
        mockSearchService.Verify(s => s.SearchAsync("test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductsService_AutocompleteAsync_DelegatesToSearchService()
    {
        // Arrange
        var mockSearchService = new Mock<IProductSearchService>();
        var expectedResults = new List<ProductAutocompleteDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Milk" }
        };

        mockSearchService
            .Setup(s => s.AutocompleteAsync("milk", 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var productsService = CreateProductsService(mockSearchService.Object);

        // Act
        var result = await productsService.AutocompleteAsync("milk", 15);

        // Assert
        result.Should().BeEquivalentTo(expectedResults);
        mockSearchService.Verify(
            s => s.AutocompleteAsync("milk", 15, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductsService_GetByBarcodeAsync_DelegatesToSearchService()
    {
        // Arrange
        var mockSearchService = new Mock<IProductSearchService>();
        var expectedProduct = new ProductDto
        {
            Id = Guid.NewGuid(),
            Name = "Barcode Product",
            Barcodes = new List<ProductBarcodeDto>
            {
                new() { Barcode = "012345678905" }
            }
        };

        mockSearchService
            .Setup(s => s.GetByBarcodeAsync("012345678905", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProduct);

        var productsService = CreateProductsService(mockSearchService.Object);

        // Act
        var result = await productsService.GetByBarcodeAsync("012345678905");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Barcode Product");
        mockSearchService.Verify(
            s => s.GetByBarcodeAsync("012345678905", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductsService_SearchParentProductsAsync_DelegatesToSearchService()
    {
        // Arrange
        var mockSearchService = new Mock<IProductSearchService>();
        var expectedResults = new List<ParentProductSearchResultDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Parent Product", Source = "tenant" },
            new() { Id = Guid.NewGuid(), Name = "Master Product", Source = "master" }
        };

        mockSearchService
            .Setup(s => s.SearchParentProductsAsync("product", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var productsService = CreateProductsService(mockSearchService.Object);

        // Act
        var result = await productsService.SearchParentProductsAsync("product");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Source == "tenant");
        result.Should().Contain(r => r.Source == "master");
        mockSearchService.Verify(
            s => s.SearchParentProductsAsync("product", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductsService_GetByBarcodeAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var mockSearchService = new Mock<IProductSearchService>();
        mockSearchService
            .Setup(s => s.GetByBarcodeAsync("999999999999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);

        var productsService = CreateProductsService(mockSearchService.Object);

        // Act
        var result = await productsService.GetByBarcodeAsync("999999999999");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Cache Isolation by Tenant

    [Fact]
    public async Task AutocompleteAsync_CacheIsIsolatedByTenant()
    {
        // Arrange: Set up cache for tenant 1
        var tenant1Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tenant2Id = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var versionKey1 = $"product-ac-version:{tenant1Id}";
        var versionKey2 = $"product-ac-version:{tenant2Id}";
        await _cache.SetStringAsync(versionKey1, "1");
        await _cache.SetStringAsync(versionKey2, "1");

        // Cache result for tenant 1
        var cacheKey1 = $"product-ac:{tenant1Id}:v1:milk:10";
        var cached1 = System.Text.Json.JsonSerializer.Serialize(new List<ProductAutocompleteDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Tenant 1 Milk" }
        });
        await _cache.SetStringAsync(cacheKey1, cached1);

        // Cache result for tenant 2
        var cacheKey2 = $"product-ac:{tenant2Id}:v1:milk:10";
        var cached2 = System.Text.Json.JsonSerializer.Serialize(new List<ProductAutocompleteDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Tenant 2 Milk" }
        });
        await _cache.SetStringAsync(cacheKey2, cached2);

        // Act: Query as tenant 1
        var mockTenant1 = new Mock<ITenantProvider>();
        mockTenant1.Setup(t => t.TenantId).Returns(tenant1Id);
        var service1 = CreateService(tenantProvider: mockTenant1.Object);
        var result1 = await service1.AutocompleteAsync("milk", 10);

        // Act: Query as tenant 2
        var mockTenant2 = new Mock<ITenantProvider>();
        mockTenant2.Setup(t => t.TenantId).Returns(tenant2Id);
        var service2 = CreateService(tenantProvider: mockTenant2.Object);
        var result2 = await service2.AutocompleteAsync("milk", 10);

        // Assert: Each tenant gets their own cached results
        result1.Should().HaveCount(1);
        result1[0].Name.Should().Be("Tenant 1 Milk");
        result2.Should().HaveCount(1);
        result2[0].Name.Should().Be("Tenant 2 Milk");
    }

    [Fact]
    public async Task InvalidateCache_OnlyAffectsCurrentTenant()
    {
        // Arrange
        var tenant1Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tenant2Id = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var versionKey1 = $"product-ac-version:{tenant1Id}";
        var versionKey2 = $"product-ac-version:{tenant2Id}";
        await _cache.SetStringAsync(versionKey1, "1");
        await _cache.SetStringAsync(versionKey2, "1");

        // Act: Invalidate tenant 1
        var mockTenant1 = new Mock<ITenantProvider>();
        mockTenant1.Setup(t => t.TenantId).Returns(tenant1Id);
        var service1 = CreateService(tenantProvider: mockTenant1.Object);
        service1.InvalidateCache();

        // Assert: Tenant 1 version changed, tenant 2 unchanged
        var newVersion1 = await _cache.GetStringAsync(versionKey1);
        var version2 = await _cache.GetStringAsync(versionKey2);

        newVersion1.Should().NotBe("1");
        version2.Should().Be("1");
    }

    #endregion

    #region Helper Methods

    private HomeManagementDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HomeManagementDbContext(options, null);
    }

    private ProductSearchService CreateService(
        HomeManagementDbContext? context = null,
        ITenantProvider? tenantProvider = null)
    {
        context ??= CreateInMemoryContext();

        var contextFactory = new Mock<IDbContextFactory<HomeManagementDbContext>>();
        contextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        return new ProductSearchService(
            context,
            contextFactory.Object,
            _mapper,
            _mockFileUrlService.Object,
            _mockImageResolver.Object,
            _cache,
            tenantProvider ?? _mockTenantProvider.Object,
            Mock.Of<ILogger<ProductSearchService>>());
    }

    private ProductsService CreateProductsService(IProductSearchService searchService)
    {
        var context = CreateInMemoryContext();

        return new ProductsService(
            context,
            _mapper,
            new Mock<IFileStorageService>().Object,
            _mockFileUrlService.Object,
            Mock.Of<IHttpClientFactory>(),
            _mockImageResolver.Object,
            searchService,
            _mockTenantProvider.Object);
    }

    #endregion
}
