using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
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
/// generic products first (#145), and every other caller keeps plain name order.
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
    /// A household with Flour (parent of two branded variants), an unbranded Rye Flour, and a
    /// branded Bob's Red Mill Flour with no parent.
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
            "Flour", "Rye Flour",
            "Bob's Red Mill Flour", "Gold Medal Flour", "King Arthur Flour");
        results.Select(r => r.IsGeneric).Should().Equal(true, true, false, false, false);
    }

    [Fact]
    public async Task Without_generic_first_the_order_is_by_name()
    {
        var tenantId = await SeedFlourHouseholdAsync();

        var results = await SearchAsync(tenantId, new ProductFilterRequest { SearchTerm = "flour", SortBy = "name" });

        results.Select(r => r.Name).Should().Equal(
            "Bob's Red Mill Flour", "Flour", "Gold Medal Flour", "King Arthur Flour", "Rye Flour");
    }
}
