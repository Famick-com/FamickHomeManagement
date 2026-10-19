using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Against real Postgres, where catalog name search runs as ILIKE: a shared product
/// does not reach another household's product search until it has been approved. The share
/// itself is covered more fully by the unit tests in <c>ProductShareTests</c>.
/// </summary>
public class MasterProductReviewPostgresTests(PostgresContainerFixture fixture)
    : IClassFixture<PostgresContainerFixture>
{
    private HomeManagementDbContext ContextFor(Guid? tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(tenantId);
        return new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseNpgsql(fixture.ConnectionString).Options,
            tenantProvider.Object);
    }

    private ProductSearchService SearchFor(Guid tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(tenantId);
        var factory = new Mock<IDbContextFactory<HomeManagementDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ContextFor(tenantId));
        return new ProductSearchService(
            ContextFor(tenantId), factory.Object, Mock.Of<IFileUrlService>(), Mock.Of<IMasterProductImageResolver>(),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            tenantProvider.Object, NullLogger<ProductSearchService>.Instance);
    }

    /// <summary>A 12-digit UPC-A with a valid check digit, unique to this run.</summary>
    private static string NewUpc()
    {
        var digits = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();
        var sum = digits.Select((c, i) => (c - '0') * (i % 2 == 0 ? 3 : 1)).Sum();
        return digits + (10 - sum % 10) % 10;
    }

    private async Task<(Guid tenantA, Guid tenantB, Guid masterId, string name, string upc)> ShareFromHouseholdAAsync()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var name = $"Moderated Oat Milk {Guid.NewGuid():N}";
        var upc = NewUpc();

        await using (var seed = ContextFor(null))
        {
            seed.Tenants.AddRange(new Tenant { Id = tenantA, Name = "A" }, new Tenant { Id = tenantB, Name = "B" });
            await seed.SaveChangesAsync();
        }

        Guid productId;
        await using (var a = ContextFor(tenantA))
        {
            var location = new Location { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Fridge" };
            var unit = new QuantityUnit { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Carton", NamePlural = "Cartons" };
            var product = new Product
            {
                Id = Guid.NewGuid(), TenantId = tenantA, Name = name, LocationId = location.Id,
                QuantityUnitIdPurchase = unit.Id, QuantityUnitIdStock = unit.Id, IsActive = true, OverriddenFields = "[]"
            };
            productId = product.Id;
            a.Locations.Add(location);
            a.QuantityUnits.Add(unit);
            a.Products.Add(product);
            a.ProductBarcodes.Add(new ProductBarcode { Id = Guid.NewGuid(), TenantId = tenantA, ProductId = product.Id, Barcode = upc });
            await a.SaveChangesAsync();
        }

        Guid masterId;
        await using (var a = ContextFor(tenantA))
        {
            var tenantProvider = new Mock<ITenantProvider>();
            tenantProvider.SetupGet(t => t.TenantId).Returns(tenantA);
            var service = new ProductsService(
                a, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(), Mock.Of<IOutboundHttpFetcher>(),
                Mock.Of<IMasterProductImageResolver>(), Mock.Of<IProductSearchService>(),
                tenantProvider.Object, NullLogger<ProductsService>.Instance);
            masterId = (await service.ShareAsync(productId)).MasterProductId!.Value;
        }

        return (tenantA, tenantB, masterId, name, upc);
    }

    private static bool Includes(IEnumerable<ProductLookupResult> results, Guid masterId) =>
        results.Any(r => r.DataSources.TryGetValue(ProductSearchService.MasterCatalogDataSource, out var id)
                         && id == masterId.ToString());

    [Fact]
    public async Task A_shared_product_reaches_other_households_only_once_approved()
    {
        var (_, tenantB, masterId, name, upc) = await ShareFromHouseholdAAsync();

        Includes(await SearchFor(tenantB).SearchLocalForLookupAsync(name, 10), masterId).Should().BeFalse();
        Includes(await SearchFor(tenantB).SearchLocalForLookupAsync(upc, 10), masterId).Should().BeFalse();

        await using (var admin = ContextFor(null))
        {
            var master = await admin.MasterProducts.IgnoreQueryFilters().SingleAsync(m => m.Id == masterId);
            master.ReviewStatus.Should().Be(MasterProductReviewStatus.Pending);
            master.ReviewStatus = MasterProductReviewStatus.Approved;
            await admin.SaveChangesAsync();
        }

        Includes(await SearchFor(tenantB).SearchLocalForLookupAsync(name, 10), masterId).Should().BeTrue();
        Includes(await SearchFor(tenantB).SearchLocalForLookupAsync(upc, 10), masterId).Should().BeTrue();
    }
}
