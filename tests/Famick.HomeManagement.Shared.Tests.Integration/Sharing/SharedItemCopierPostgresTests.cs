using Famick.HomeManagement.Core.DTOs.Sharing;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using ProductConfiguration = Famick.HomeManagement.Infrastructure.Data.Configurations.ProductConfiguration;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Infrastructure.Sharing;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;

namespace Famick.HomeManagement.Shared.Tests.Integration.Sharing;

/// <summary>
/// Copying a shared item against real Postgres, where the origin indexes are enforced and a copy's
/// transaction really rolls back (#195).
/// </summary>
public class SharedItemCopierPostgresTests : IClassFixture<PostgresContainerFixture>, IDisposable
{
    private readonly PostgresContainerFixture _fixture;
    private readonly ServiceProvider _services;
    private readonly Guid _smiths = Guid.NewGuid();
    private readonly Guid _jones = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Dictionary<Guid, (Guid Location, Guid Unit)> _basics = new();

    public SharedItemCopierPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        _services = Services();

        using var db = Seeding();
        db.Tenants.AddRange(new Tenant { Id = _smiths, Name = "The Smiths" }, new Tenant { Id = _jones, Name = "The Joneses" });
        db.Users.AddRange(
            new User { Id = _alice, TenantId = _smiths, Email = $"alice-{_alice:N}@example.com", Username = $"alice-{_alice:N}" },
            new User { Id = _bob, TenantId = _jones, Email = $"bob-{_bob:N}@example.com", Username = $"bob-{_bob:N}" });
        foreach (var tenant in new[] { _smiths, _jones })
        {
            var location = new Location { Id = Guid.NewGuid(), TenantId = tenant, Name = "Pantry", IsActive = true };
            var unit = new QuantityUnit { Id = Guid.NewGuid(), TenantId = tenant, Name = "Piece", NamePlural = "Pieces", IsActive = true };
            db.Locations.Add(location);
            db.QuantityUnits.Add(unit);
            _basics[tenant] = (location.Id, unit.Id);
        }
        db.SaveChanges();
    }

    public void Dispose() => _services.Dispose();

    private ISharedItemCopier Copier => _services.CreateScope().ServiceProvider.GetRequiredService<ISharedItemCopier>();

    private Task<SharedItemCopyResult?> CopyToJonesAsync(ShareableKind kind, Guid itemId) =>
        Copier.CopyAsync(new ShareCopyRequest(new SharedItemRef(kind, itemId, _smiths, _alice), _jones, _bob));

    [Fact]
    public async Task A_meal_that_fails_partway_leaves_none_of_its_recipes_behind()
    {
        var recipe = await AddRecipeAsync(_smiths, "Soup");
        var bread = await AddProductAsync(_smiths, "Bread");
        var meal = await AddMealAsync(_smiths, recipe, bread);

        // The recipe is written first; the product then has nowhere to go.
        await using (var db = Seeding())
        {
            await db.Products.Where(p => p.TenantId == _jones).ExecuteDeleteAsync();
            await db.Locations.Where(l => l.TenantId == _jones).ExecuteDeleteAsync();
        }

        await FluentActions.Awaiting(() => CopyToJonesAsync(ShareableKind.Meal, meal))
            .Should().ThrowAsync<DomainException>();

        await using var check = Seeding();
        (await check.Recipes.CountAsync(r => r.TenantId == _jones)).Should().Be(0);
        (await check.Meals.CountAsync(m => m.TenantId == _jones)).Should().Be(0);
    }

    [Fact]
    public async Task Two_adds_at_once_make_one_copy()
    {
        var recipe = await AddRecipeAsync(_smiths, "Roux");

        var copies = await Task.WhenAll(CopyToJonesAsync(ShareableKind.Recipe, recipe), CopyToJonesAsync(ShareableKind.Recipe, recipe));

        copies.Select(c => c!.ItemId).Distinct().Should().ContainSingle();
        await using var db = Seeding();
        (await db.Recipes.CountAsync(r => r.TenantId == _jones)).Should().Be(1);
    }

    [Fact]
    public async Task A_barcode_the_household_already_uses_stays_with_its_product()
    {
        var granola = await AddProductAsync(_smiths, "Granola", barcode: "0123456789");
        var oats = await AddProductAsync(_jones, "Oats", barcode: "0123456789");

        var copy = await CopyToJonesAsync(ShareableKind.Product, granola);

        await using var db = Seeding();
        (await db.ProductBarcodes.SingleAsync(b => b.TenantId == _jones && b.Barcode == "0123456789")).ProductId.Should().Be(oats);
        (await db.ProductBarcodes.CountAsync(b => b.ProductId == copy!.ItemId)).Should().Be(0);
    }

    [Fact]
    public async Task One_active_product_per_origin_and_household()
    {
        await using var db = Seeding();
        var (location, unit) = _basics[_jones];
        Product Copy(bool active) => new()
        {
            Id = Guid.NewGuid(), TenantId = _jones, Name = $"Granola {Guid.NewGuid():N}", IsActive = active, OriginKey = "same-origin",
            LocationId = location, QuantityUnitIdPurchase = unit, QuantityUnitIdStock = unit
        };

        // A deleted copy doesn't stop the item being added again.
        db.Products.AddRange(Copy(active: false), Copy(active: true));
        await db.SaveChangesAsync();

        db.Products.Add(Copy(active: true));
        var save = () => db.SaveChangesAsync();
        (await save.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.ConstraintName.Should().Be(ProductConfiguration.OriginIndexName);
    }

    private ServiceProvider Services()
    {
        var options = new MultiTenancyOptions { IsMultiTenantEnabled = true };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMultiTenancyOptions>(options);
        services.AddScoped<ITenantProvider, SettableTenantProvider>();
        services.AddScoped(sp => new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseNpgsql(_fixture.ConnectionString).Options,
            sp.GetRequiredService<ITenantProvider>(), options));
        services.AddSingleton(Mock.Of<IFileStorageService>());
        services.AddSingleton(Mock.Of<IFileUrlService>());
        services.AddSingleton(Mock.Of<IOutboundHttpFetcher>());
        services.AddSingleton(Mock.Of<IMasterProductImageResolver>());
        services.AddSingleton(Mock.Of<IProductSearchService>());
        services.AddSingleton(Mock.Of<IAddressHasher>());
        services.AddScoped<IProductsService, ProductsService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IngredientMatcher>();
        services.AddScoped<RecipeShareHandler>();
        services.AddScoped<ProductShareHandler>();
        services.AddScoped<MealShareHandler>();
        services.AddScoped<IShareableItemHandler>(sp => sp.GetRequiredService<RecipeShareHandler>());
        services.AddScoped<IShareableItemHandler>(sp => sp.GetRequiredService<ProductShareHandler>());
        services.AddScoped<IShareableItemHandler>(sp => sp.GetRequiredService<MealShareHandler>());
        services.AddScoped<ISharedItemCopier, SharedItemCopier>();
        return services.BuildServiceProvider();
    }

    private HomeManagementDbContext Seeding() => new(
        new DbContextOptionsBuilder<HomeManagementDbContext>().UseNpgsql(_fixture.ConnectionString).Options);

    private async Task<Guid> AddRecipeAsync(Guid tenant, string name)
    {
        await using var db = Seeding();
        var recipe = new Recipe { Id = Guid.NewGuid(), TenantId = tenant, Name = name, Servings = 2 };
        recipe.Steps.Add(new RecipeStep { Id = Guid.NewGuid(), TenantId = tenant, RecipeId = recipe.Id, StepOrder = 1, Instructions = "Stir" });
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe.Id;
    }

    private async Task<Guid> AddProductAsync(Guid tenant, string name, string? barcode = null)
    {
        await using var db = Seeding();
        var (location, unit) = _basics[tenant];
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenant, Name = name, IsActive = true,
            LocationId = location, QuantityUnitIdPurchase = unit, QuantityUnitIdStock = unit
        };
        if (barcode != null)
            product.Barcodes.Add(new ProductBarcode { Id = Guid.NewGuid(), TenantId = tenant, Barcode = barcode });
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private async Task<Guid> AddMealAsync(Guid tenant, Guid recipe, Guid product)
    {
        await using var db = Seeding();
        var meal = new Meal { Id = Guid.NewGuid(), TenantId = tenant, Name = "Soup night" };
        meal.Items.Add(new MealItem { Id = Guid.NewGuid(), MealId = meal.Id, ItemType = MealItemType.Recipe, RecipeId = recipe, SortOrder = 0 });
        meal.Items.Add(new MealItem { Id = Guid.NewGuid(), MealId = meal.Id, ItemType = MealItemType.Product, ProductId = product, SortOrder = 1 });
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        return meal.Id;
    }

    private sealed class SettableTenantProvider : ITenantProvider
    {
        public Guid? TenantId { get; private set; }
        public Guid? UserId { get; private set; }
        public void SetTenantId(Guid tenantId) => TenantId = tenantId;
        public void SetUserId(Guid userId) => UserId = userId;
        public void ClearTenantId() => TenantId = null;
        public void ClearUserId() => UserId = null;
    }
}
