using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
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
/// Against real Postgres, where product name search runs as ILIKE: recipe ingredient search asks for
/// generic products first (#145), with the catalog's after the household's generic ones, and every
/// other caller keeps plain name order.
/// </summary>
public class ProductGenericFirstPostgresTests(PostgresContainerFixture fixture)
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

    /// <summary>
    /// A household with Flour (parent of two branded variants), an unbranded Rye Flour, flours whose
    /// brand is only whitespace, and a branded Bob's Red Mill Flour with no parent.
    /// </summary>
    private async Task<Guid> SeedFlourHouseholdAsync()
    {
        var tenantId = Guid.NewGuid();
        await using (var seed = ContextFor(null))
        {
            seed.Tenants.Add(new Tenant { Id = tenantId, Name = "Flour household" });
            await seed.SaveChangesAsync();
        }

        await using var db = ContextFor(tenantId);
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pantry" };
        var unit = new QuantityUnit { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bag", NamePlural = "Bags" };
        db.Locations.Add(location);
        db.QuantityUnits.Add(unit);

        Product NewProduct(string name, string? brand = null, Guid? parentId = null) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = name, Brand = brand, ParentProductId = parentId,
            LocationId = location.Id, QuantityUnitIdPurchase = unit.Id, QuantityUnitIdStock = unit.Id,
            IsActive = true, OverriddenFields = "[]"
        };

        var flour = NewProduct("Flour");
        db.Products.AddRange(
            flour,
            NewProduct("King Arthur Flour", "King Arthur", flour.Id),
            NewProduct("Gold Medal Flour", "Gold Medal", flour.Id),
            NewProduct("Rye Flour"),
            NewProduct("Tab Flour", "\t"),
            NewProduct("Vertical Tab Flour", "\u000B"),
            NewProduct("No-Break Space Flour", "\u00A0"),
            NewProduct("Bob's Red Mill Flour", "Bob's Red Mill"));
        await db.SaveChangesAsync();
        return tenantId;
    }

    private async Task<List<ProductDto>> SearchAsync(Guid tenantId, ProductFilterRequest filter)
    {
        var query = await SearchFor(tenantId).BuildProductQueryAsync(filter);
        return (await query.ToListAsync()).Select(ProductMapper.ToDto).ToList();
    }

    [Fact]
    public async Task Generic_first_lists_generic_products_before_specific_ones()
    {
        var tenantId = await SeedFlourHouseholdAsync();

        var results = await SearchAsync(tenantId, new ProductFilterRequest { SearchTerm = "flour", GenericFirst = true });

        results.Select(r => r.Name).Should().Equal(
            "Flour", "No-Break Space Flour", "Rye Flour", "Tab Flour", "Vertical Tab Flour",
            "Bob's Red Mill Flour", "Gold Medal Flour", "King Arthur Flour");
        results.Select(r => r.IsGeneric).Should().Equal(true, true, true, true, true, false, false, false);
    }

    [Fact]
    public async Task Without_generic_first_the_order_is_by_name()
    {
        var tenantId = await SeedFlourHouseholdAsync();

        var results = await SearchAsync(tenantId, new ProductFilterRequest { SearchTerm = "flour", SortBy = "name" });

        results.Select(r => r.Name).Should().Equal(
            "Bob's Red Mill Flour", "Flour", "Gold Medal Flour", "King Arthur Flour", "No-Break Space Flour",
            "Rye Flour", "Tab Flour", "Vertical Tab Flour");
    }

    [Fact]
    public async Task Generic_first_adds_the_catalogs_products_the_household_lacks_after_its_generic_ones()
    {
        // Names unique to this test, since the catalog is shared by every test on the container.
        var tag = "gf" + Guid.NewGuid().ToString("N")[..8];
        var tenantId = Guid.NewGuid();
        var rawSugar = Guid.NewGuid();
        await using (var seed = ContextFor(null))
        {
            seed.Tenants.Add(new Tenant { Id = tenantId, Name = "Sugar household" });
            seed.MasterProducts.AddRange(
                new MasterProduct { Id = Guid.NewGuid(), Name = $"{tag} Brown Sugar", Category = "Baking" },
                new MasterProduct { Id = Guid.NewGuid(), Name = $"{tag} Sugar Packets", Brand = "Domino", Category = "Baking" },
                new MasterProduct { Id = Guid.NewGuid(), Name = $"{tag} Sugar", Category = "Baking" },
                new MasterProduct { Id = Guid.NewGuid(), Name = $"{tag} Cane Sugar", Category = "Baking",
                    ReviewStatus = MasterProductReviewStatus.Pending },
                new MasterProduct { Id = rawSugar, Name = $"{tag} Raw Sugar", Category = "Baking" });
            await seed.SaveChangesAsync();
        }

        await using (var db = ContextFor(tenantId))
        {
            var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pantry" };
            var unit = new QuantityUnit { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bag", NamePlural = "Bags" };
            db.Locations.Add(location);
            db.QuantityUnits.Add(unit);

            Product NewProduct(string name, string? brand = null, Guid? masterProductId = null) => new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = name, Brand = brand, MasterProductId = masterProductId,
                LocationId = location.Id, QuantityUnitIdPurchase = unit.Id, QuantityUnitIdStock = unit.Id,
                IsActive = true, OverriddenFields = "[]"
            };

            // The household has its own Sugar, so the catalog's isn't offered, and has added the
            // catalog's Raw Sugar under another name, so that isn't either.
            db.Products.AddRange(
                NewProduct($"{tag} Sugar"),
                NewProduct($"{tag} Sugar Acme", "Acme"),
                NewProduct("Raw sugar jar", masterProductId: rawSugar));
            await db.SaveChangesAsync();
        }

        await using var context = ContextFor(tenantId);
        var tenantProvider = Mock.Of<ITenantProvider>(t => t.TenantId == tenantId);
        var products = new ProductsService(context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            Mock.Of<IOutboundHttpFetcher>(), Mock.Of<IMasterProductImageResolver>(), SearchFor(tenantId), tenantProvider,
            NullLogger<ProductsService>.Instance);

        var results = await products.ListAsync(new ProductFilterRequest { SearchTerm = tag, GenericFirst = true });

        results.Select(r => (r.Name, r.IsCatalogProduct)).Should().Equal(
            ($"{tag} Sugar", false),
            ($"{tag} Brown Sugar", true),
            ($"{tag} Sugar Acme", false),
            ($"{tag} Sugar Packets", true));
        results.Where(r => r.IsCatalogProduct).Should().OnlyContain(r => r.Id == Guid.Empty && r.MasterProductId != null);
    }
}
