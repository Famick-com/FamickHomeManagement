using System.Text;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Against real Postgres, where the name match runs as lower() and the generic rule as a regular
/// expression: an imported line links the household's generic product, or adopts the catalog's.
/// </summary>
public class RecipeImportMatchingPostgresTests(PostgresContainerFixture fixture)
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

    private static RecipeImportService ImportService(HomeManagementDbContext db, Guid tenantId, string url, string html)
    {
        var fetcher = new Mock<IOutboundHttpFetcher>();
        fetcher.Setup(f => f.GetAsync(new Uri(url), It.IsAny<long>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboundFetchResult(Encoding.UTF8.GetBytes(html), "text/html", new Uri(url)));

        var tenantProvider = Mock.Of<ITenantProvider>(t => t.TenantId == tenantId);
        var recipes = new RecipeService(db, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(), NullLogger<RecipeService>.Instance);
        var products = new ProductsService(db, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(), fetcher.Object,
            Mock.Of<IMasterProductImageResolver>(), Mock.Of<IProductSearchService>(), tenantProvider,
            NullLogger<ProductsService>.Instance);
        return new RecipeImportService(db, fetcher.Object, products, recipes, NullLogger<RecipeImportService>.Instance);
    }

    private static string Page(params string[] ingredients) =>
        $$"""
        <script type="application/ld+json">
        { "@type": "Recipe", "name": "Postgres Test", "recipeIngredient": {{JsonSerializer.Serialize(ingredients)}} }
        </script>
        """;

    [Fact]
    public async Task Lines_link_generic_household_products_and_adopt_generic_catalog_ones()
    {
        // Names unique to this test, since the catalog is shared by every test on the container.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var butter = $"Butter{suffix}";
        var grain = $"Grain{suffix}";
        var branded = $"Stock{suffix}";

        var tenantId = Guid.NewGuid();
        await using (var seed = ContextFor(null))
        {
            seed.Tenants.Add(new Tenant { Id = tenantId, Name = "Import household" });
            seed.MasterProducts.AddRange(
                new MasterProduct { Id = Guid.NewGuid(), Name = grain, Category = "Baking" },
                new MasterProduct { Id = Guid.NewGuid(), Name = branded, Brand = "Acme", Category = "Soup" });
            await seed.SaveChangesAsync();
        }

        await using var db = ContextFor(tenantId);
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pantry", IsActive = true };
        var cup = new QuantityUnit
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Cup", NamePlural = "Cups", IsActive = true,
            StandardUnit = StandardUnit.Cup
        };
        db.Locations.Add(location);
        db.QuantityUnits.Add(cup);

        Product NewProduct(string name, string? brand) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = name, Brand = brand, LocationId = location.Id,
            QuantityUnitIdPurchase = cup.Id, QuantityUnitIdStock = cup.Id, IsActive = true, OverriddenFields = "[]"
        };
        var brandedButter = NewProduct(butter.ToUpperInvariant(), "Acme");
        var genericButter = NewProduct(butter.ToUpperInvariant(), " ");
        db.Products.AddRange(brandedButter, genericButter);
        await db.SaveChangesAsync();

        var url = $"https://example.test/{suffix}";
        var result = await ImportService(db, tenantId, url, Page(
                $"1 cup unsalted {butter.ToLowerInvariant()}", $"2 cups {grain.ToLowerInvariant()}s", $"1 cup {branded}"))
            .ImportAsync(url, false, null);

        result.Ingredients.Select(i => i.Outcome).Should().Equal(
            ImportedIngredientOutcome.LinkedFromHousehold, ImportedIngredientOutcome.AddedFromCatalog, ImportedIngredientOutcome.Unlinked);

        var lines = result.Recipe.Steps[0].Ingredients;
        lines[0].ProductId.Should().Be(genericButter.Id);
        lines[1].ProductName.Should().Be(grain);
        lines[1].QuantityUnitId.Should().Be(cup.Id);
        lines[2].ProductId.Should().BeNull();
    }
}
