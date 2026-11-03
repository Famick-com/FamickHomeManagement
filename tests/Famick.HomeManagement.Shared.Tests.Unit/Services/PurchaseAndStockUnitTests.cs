using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RecipeAddToShoppingListRequest = Famick.HomeManagement.Core.DTOs.Recipes.AddToShoppingListRequest;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// A product's purchase and stock units, across a generic parent and its brands: flour bought by the bag
/// and used by the cup, with King Arthur at 18 cups a bag and Bob's at 7.
/// </summary>
public class PurchaseAndStockUnitTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly HomeManagementDbContext _context;
    private readonly Mock<IStockService> _stockService = new();
    private readonly List<AddStockRequest> _stocked = new();

    private readonly Guid _kitchen;
    private readonly Guid _bag;
    private readonly Guid _cup;
    private readonly Guid _tablespoon;
    private readonly Guid _gram;
    private readonly Guid _pinch;
    private readonly Product _flour;
    private readonly Product _kingArthur;
    private readonly Product _bobs;

    public PurchaseAndStockUnitTests()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantProvider.Object);

        _stockService
            .Setup(s => s.AddStockAsync(It.IsAny<AddStockRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AddStockRequest, CancellationToken>((r, _) => _stocked.Add(r))
            .ReturnsAsync(() => new StockEntryDto { Id = Guid.NewGuid() });

        var kitchen = new Location { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Kitchen" };
        _context.Locations.Add(kitchen);
        _kitchen = kitchen.Id;

        _bag = AddUnit("Bag", null);
        _cup = AddUnit("Cup", StandardUnit.Cup);
        _tablespoon = AddUnit("Tablespoon", StandardUnit.Tablespoon);
        _gram = AddUnit("Gram", StandardUnit.Gram);
        _pinch = AddUnit("Pinch", null);

        _flour = AddProduct("Flour", _bag, _cup, 18m);
        _kingArthur = AddProduct("King Arthur 5 lb", _bag, _cup, 18m, _flour);
        _bobs = AddProduct("Bob's 2 lb", _bag, _cup, 7m, _flour);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    #region Recipe stock check and Add missing

    [Fact]
    public async Task A_generic_ingredient_counts_its_childrens_stock()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 2, _cup));
        AddStock(_kingArthur, 18m);
        await _context.SaveChangesAsync();

        var fulfillment = await Recipes().CheckFulfillmentAsync(recipeId);

        var line = fulfillment.Ingredients.Single();
        line.IsSufficient.Should().BeTrue();
        line.AvailableAmount.Should().Be(18m);
        fulfillment.CanBeMade.Should().BeTrue();
    }

    [Fact]
    public async Task Add_missing_buys_whole_purchase_units()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 2, _cup));
        var list = AddList();
        await _context.SaveChangesAsync();

        await Recipes().AddToShoppingListAsync(recipeId, new RecipeAddToShoppingListRequest { ShoppingListId = list.Id });

        var item = await _context.ShoppingListItems.SingleAsync();
        item.ProductId.Should().Be(_flour.Id);
        item.Amount.Should().Be(1m, "2 cups of flour is one 18-cup bag");
    }

    [Fact]
    public async Task Lines_in_converting_units_share_the_stock_in_order()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 1, _cup), Ingredient(_flour, 2, _tablespoon));
        AddStock(_flour, 1m);
        var list = AddList();
        await _context.SaveChangesAsync();

        var fulfillment = await Recipes().CheckFulfillmentAsync(recipeId);

        fulfillment.Ingredients.Select(i => (i.QuantityUnitName, i.AvailableAmount, i.AvailableQuantityUnitName, i.IsSufficient, i.CanCompare))
            .Should().Equal(("Cup", 1m, "Cup", true, true), ("Tablespoon", 0m, "Tablespoon", false, true));
        fulfillment.CanBeMade.Should().BeFalse();
        fulfillment.HasUncomparableIngredients.Should().BeFalse();

        await Recipes().AddToShoppingListAsync(recipeId, new RecipeAddToShoppingListRequest { ShoppingListId = list.Id });
        (await _context.ShoppingListItems.SingleAsync()).Amount.Should().Be(1m, "⅛ cup short rounds up to a bag");
    }

    [Fact]
    public async Task A_line_in_the_purchase_unit_converts_with_the_factor()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 1, _bag));
        AddStock(_flour, 9m);
        await _context.SaveChangesAsync();

        var line = (await Recipes().CheckFulfillmentAsync(recipeId)).Ingredients.Single();

        line.IsSufficient.Should().BeFalse("1 bag is 18 cups and there are 9");
        line.AvailableAmount.Should().Be(0.5m);
        line.AvailableQuantityUnitName.Should().Be("Bag");
    }

    [Fact]
    public async Task A_line_that_does_not_convert_is_compared_as_written()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 100, _gram));
        AddStock(_flour, 150m);
        await _context.SaveChangesAsync();

        var fulfillment = await Recipes().CheckFulfillmentAsync(recipeId);

        var line = fulfillment.Ingredients.Single();
        line.CanCompare.Should().BeFalse("grams don't convert to cups");
        line.IsSufficient.Should().BeTrue();
        line.AvailableAmount.Should().Be(150m);
        line.AvailableQuantityUnitName.Should().Be("Cup");
        fulfillment.HasUncomparableIngredients.Should().BeTrue();
    }

    [Fact]
    public async Task Add_missing_does_not_add_a_shortfall_in_another_unit_to_purchase_units()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 2, _cup), Ingredient(_flour, 100, _gram));
        var list = AddList();
        await _context.SaveChangesAsync();

        await Recipes().AddToShoppingListAsync(recipeId, new RecipeAddToShoppingListRequest { ShoppingListId = list.Id });

        (await _context.ShoppingListItems.SingleAsync()).Amount.Should().Be(1m, "the bag for 2 cups, not 1 bag plus 100");
    }

    [Fact]
    public async Task Add_missing_adds_a_shortfall_that_does_not_convert_as_written_when_nothing_else_is_short()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 100, _gram));
        var list = AddList();
        await _context.SaveChangesAsync();

        await Recipes().AddToShoppingListAsync(recipeId, new RecipeAddToShoppingListRequest { ShoppingListId = list.Id });

        (await _context.ShoppingListItems.SingleAsync()).Amount.Should().Be(100m);
    }

    [Fact]
    public async Task A_child_kept_in_another_converting_unit_counts_converted()
    {
        var legacy = AddProduct("Old flour", _bag, _tablespoon, 1m, _flour);
        var recipeId = AddRecipe(Ingredient(_flour, 1, _cup));
        AddStock(legacy, 16m);
        await _context.SaveChangesAsync();

        var line = (await Recipes().CheckFulfillmentAsync(recipeId)).Ingredients.Single();

        line.AvailableAmount.Should().Be(1m, "16 tablespoons is a cup");
        line.IsSufficient.Should().BeTrue();
    }

    [Fact]
    public async Task A_child_kept_in_a_unit_that_does_not_convert_is_left_out()
    {
        var legacy = AddProduct("Odd flour", _bag, _pinch, 1m, _flour);
        var recipeId = AddRecipe(Ingredient(_flour, 1, _cup));
        AddStock(legacy, 500m);
        await _context.SaveChangesAsync();

        var line = (await Recipes().CheckFulfillmentAsync(recipeId)).Ingredients.Single();

        line.AvailableAmount.Should().Be(0m);
        line.IsSufficient.Should().BeFalse();
    }

    #endregion

    #region Moving purchases into stock

    [Fact]
    public async Task A_purchase_is_stocked_in_the_stock_unit()
    {
        var list = AddList();
        var item = AddListItem(list, _flour, 2m);
        await _context.SaveChangesAsync();

        await Shopping().MoveToInventoryAsync(new MoveToInventoryRequest
        {
            ShoppingListId = list.Id,
            Items = [new() { ShoppingListItemId = item.Id, ProductId = _flour.Id, Amount = 2m, Price = 9m }]
        });

        var stocked = _stocked.Single();
        stocked.ProductId.Should().Be(_flour.Id);
        stocked.Amount.Should().Be(36m);
        stocked.Price.Should().Be(0.5m, "$9 a bag is 50 cents a cup");
    }

    [Fact]
    public async Task A_brand_bought_against_a_generic_item_is_stocked_as_that_brand()
    {
        var list = AddList();
        var item = AddListItem(list, _flour, 1m);
        item.ChildPurchasesJson = JsonSerializer.Serialize(new List<ChildPurchaseEntry>
        {
            new() { ChildProductId = _bobs.Id, ChildProductName = _bobs.Name, Quantity = 1m }
        });
        await _context.SaveChangesAsync();

        await Shopping().MoveToInventoryAsync(new MoveToInventoryRequest
        {
            ShoppingListId = list.Id,
            Items = [new() { ShoppingListItemId = item.Id, ProductId = _flour.Id, Amount = 1m }]
        });

        var stocked = _stocked.Single();
        stocked.ProductId.Should().Be(_bobs.Id);
        stocked.Amount.Should().Be(7m, "a bag of Bob's is 7 cups");
    }

    [Fact]
    public async Task One_brand_bought_against_a_generic_item_keeps_the_price()
    {
        var list = AddList();
        var item = AddListItem(list, _flour, 1m);
        item.ChildPurchasesJson = JsonSerializer.Serialize(new List<ChildPurchaseEntry>
        {
            new() { ChildProductId = _bobs.Id, ChildProductName = _bobs.Name, Quantity = 1m }
        });
        await _context.SaveChangesAsync();

        await Shopping().MoveToInventoryAsync(new MoveToInventoryRequest
        {
            ShoppingListId = list.Id,
            Items = [new() { ShoppingListItemId = item.Id, ProductId = _flour.Id, Amount = 1m, Price = 7m }]
        });

        _stocked.Single().Price.Should().Be(1m, "$7 for a 7-cup bag is a dollar a cup");
    }

    [Fact]
    public async Task Several_brands_bought_against_a_generic_item_are_stocked_without_a_price()
    {
        var list = AddList();
        var item = AddListItem(list, _flour, 2m);
        item.ChildPurchasesJson = JsonSerializer.Serialize(new List<ChildPurchaseEntry>
        {
            new() { ChildProductId = _bobs.Id, ChildProductName = _bobs.Name, Quantity = 1m },
            new() { ChildProductId = _kingArthur.Id, ChildProductName = _kingArthur.Name, Quantity = 1m }
        });
        await _context.SaveChangesAsync();

        await Shopping().MoveToInventoryAsync(new MoveToInventoryRequest
        {
            ShoppingListId = list.Id,
            Items = [new() { ShoppingListItemId = item.Id, ProductId = _flour.Id, Amount = 2m, Price = 7m }]
        });

        _stocked.Should().HaveCount(2).And.OnlyContain(s => s.Price == null);
    }

    #endregion

    #region Children take their parent's stock unit

    [Fact]
    public async Task A_child_created_while_shopping_takes_its_parents_units()
    {
        var list = AddList();
        var item = AddListItem(list, _flour, 1m);
        await _context.SaveChangesAsync();

        await Shopping().AddChildToParentAsync(item.Id, new AddChildToParentRequest { ProductName = "Store flour", Quantity = 1 });

        var child = await _context.Products.SingleAsync(p => p.Name == "Store flour");
        (child.QuantityUnitIdPurchase, child.QuantityUnitIdStock, child.QuantityUnitFactorPurchaseToStock)
            .Should().Be((_bag, _cup, 18m));
    }

    [Fact]
    public async Task A_product_adopted_as_a_child_takes_its_parents_stock_unit()
    {
        var loose = AddProduct("Loose flour", _bag, _gram, 1m);
        var list = AddList();
        var item = AddListItem(list, _flour, 1m);
        await _context.SaveChangesAsync();

        await Shopping().AddChildToParentAsync(item.Id, new AddChildToParentRequest { ProductId = loose.Id, Quantity = 1 });

        loose.QuantityUnitIdStock.Should().Be(_cup);
    }

    [Fact]
    public async Task A_product_with_stock_adopted_as_a_child_keeps_its_unit()
    {
        var loose = AddProduct("Loose flour", _bag, _gram, 1m);
        AddStock(loose, 500m);
        var list = AddList();
        var item = AddListItem(list, _flour, 1m);
        await _context.SaveChangesAsync();

        await Shopping().AddChildToParentAsync(item.Id, new AddChildToParentRequest { ProductId = loose.Id, Quantity = 1 });

        loose.ParentProductId.Should().Be(_flour.Id);
        loose.QuantityUnitIdStock.Should().Be(_gram, "500 grams mustn't silently become 500 cups");
    }

    [Fact]
    public async Task A_product_created_with_a_parent_takes_its_stock_unit()
    {
        var created = await Products().CreateAsync(CreateRequest("Gold Medal", stockUnit: _gram, parent: _flour.Id));

        created.QuantityUnitIdStock.Should().Be(_cup);
    }

    [Fact]
    public async Task Giving_a_product_a_parent_sets_its_stock_unit()
    {
        var loose = AddProduct("Loose flour", _bag, _gram, 1m);
        await _context.SaveChangesAsync();

        var updated = await Products().UpdateAsync(loose.Id, UpdateRequest(loose, stockUnit: _gram, parent: _flour.Id));

        updated.QuantityUnitIdStock.Should().Be(_cup);
    }

    [Fact]
    public async Task A_child_holding_stock_keeps_its_unit_unless_the_parents_is_asked_for()
    {
        var legacy = AddProduct("Old flour", _bag, _gram, 1m, _flour);
        AddStock(legacy, 500m);
        await _context.SaveChangesAsync();
        var products = Products();

        (await products.UpdateAsync(legacy.Id, UpdateRequest(legacy, stockUnit: _gram, parent: _flour.Id)))
            .QuantityUnitIdStock.Should().Be(_gram);
        (await products.UpdateAsync(legacy.Id, UpdateRequest(legacy, stockUnit: _cup, parent: _flour.Id)))
            .QuantityUnitIdStock.Should().Be(_cup);
    }

    [Fact]
    public async Task Children_without_stock_follow_a_change_to_their_parents_stock_unit()
    {
        AddStock(_bobs, 7m);
        await _context.SaveChangesAsync();

        await Products().UpdateAsync(_flour.Id, UpdateRequest(_flour, stockUnit: _tablespoon, parent: null));

        _kingArthur.QuantityUnitIdStock.Should().Be(_tablespoon);
        _bobs.QuantityUnitIdStock.Should().Be(_cup, "its 7 cups mustn't silently become 7 tablespoons");
    }

    [Fact]
    public async Task A_product_cannot_be_its_own_parent()
    {
        var act = () => Products().UpdateAsync(_flour.Id, UpdateRequest(_flour, stockUnit: _cup, parent: _flour.Id));

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task A_variant_cannot_be_a_parent()
    {
        var gold = await Products().CreateAsync(CreateRequest("Gold Medal", stockUnit: _cup, parent: null));

        var act = () => Products().UpdateAsync(gold.Id, UpdateRequest(_context.Products.Single(p => p.Id == gold.Id), stockUnit: _cup, parent: _bobs.Id));

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
        (await _context.Products.SingleAsync(p => p.Id == gold.Id)).ParentProductId.Should().BeNull();
    }

    [Fact]
    public async Task A_variant_cannot_be_created_under_a_variant()
    {
        var act = () => Products().CreateAsync(CreateRequest("Gold Medal", stockUnit: _cup, parent: _bobs.Id));

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task A_product_with_variants_cannot_become_a_variant()
    {
        var baking = AddProduct("Baking supplies", _bag, _cup, 1m);
        await _context.SaveChangesAsync();

        var act = () => Products().UpdateAsync(_flour.Id, UpdateRequest(_flour, stockUnit: _cup, parent: baking.Id));

        await act.Should().ThrowAsync<BusinessRuleViolationException>("Flour has variants, and a cycle starts this way");
    }

    [Fact]
    public async Task A_variant_keeping_its_parent_can_still_be_edited()
    {
        var updated = await Products().UpdateAsync(_bobs.Id, UpdateRequest(_bobs, stockUnit: _cup, parent: _flour.Id));

        updated.ParentProductId.Should().Be(_flour.Id);
    }

    #endregion

    #region Stock overview

    [Fact]
    public async Task A_parents_total_converts_its_childrens_stock_and_leaves_out_what_does_not_convert()
    {
        var tablespoons = AddProduct("Old flour", _bag, _tablespoon, 1m, _flour);
        var pinches = AddProduct("Odd flour", _bag, _pinch, 1m, _flour);
        AddStock(_flour, 1m);
        AddStock(_kingArthur, 18m);
        AddStock(tablespoons, 16m);
        AddStock(pinches, 100m);
        await _context.SaveChangesAsync();

        var stock = new StockService(_context, Mock.Of<ITenantProvider>(t => t.TenantId == _tenantId), Mock.Of<IFileUrlService>());
        var row = (await stock.GetOverviewAsync()).Single(r => r.ProductId == _flour.Id);

        row.TotalAmount.Should().Be(20m, "1 + 18 + 16 tablespoons as a cup; pinches don't convert");
        row.ChildProducts.Should().Contain(c => c.ProductId == pinches.Id && c.TotalAmount == 100m);
    }

    #endregion

    private RecipeService Recipes() =>
        new(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(), NullLogger<RecipeService>.Instance);

    private ShoppingListService Shopping() =>
        new(_context, NullLogger<ShoppingListService>.Instance, Mock.Of<IStoreIntegrationService>(), Mock.Of<IPluginLoader>(),
            _stockService.Object, Mock.Of<ITodoItemService>(), Mock.Of<IProductsService>(), Mock.Of<IFileUrlService>());

    private ProductsService Products() =>
        new(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(), Mock.Of<IOutboundHttpFetcher>(),
            Mock.Of<IMasterProductImageResolver>(), Mock.Of<IProductSearchService>(),
            Mock.Of<ITenantProvider>(t => t.TenantId == _tenantId), NullLogger<ProductsService>.Instance);

    private CreateProductRequest CreateRequest(string name, Guid stockUnit, Guid? parent) => new()
    {
        Name = name, LocationId = _kitchen, QuantityUnitIdPurchase = _bag, QuantityUnitIdStock = stockUnit,
        QuantityUnitFactorPurchaseToStock = 1m, ParentProductId = parent, IsActive = true
    };

    private static UpdateProductRequest UpdateRequest(Product product, Guid stockUnit, Guid? parent) => new()
    {
        Name = product.Name, LocationId = product.LocationId, QuantityUnitIdPurchase = product.QuantityUnitIdPurchase,
        QuantityUnitIdStock = stockUnit, QuantityUnitFactorPurchaseToStock = product.QuantityUnitFactorPurchaseToStock,
        ParentProductId = parent, IsActive = true
    };

    private Guid AddUnit(string name, StandardUnit? standard)
    {
        var unit = new QuantityUnit { Id = Guid.NewGuid(), TenantId = _tenantId, Name = name, NamePlural = name + "s", StandardUnit = standard };
        _context.QuantityUnits.Add(unit);
        return unit.Id;
    }

    private Product AddProduct(string name, Guid purchaseUnit, Guid stockUnit, decimal factor, Product? parent = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = name, LocationId = _kitchen, IsActive = true,
            QuantityUnitIdPurchase = purchaseUnit, QuantityUnitIdStock = stockUnit,
            QuantityUnitFactorPurchaseToStock = factor, ParentProductId = parent?.Id
        };
        _context.Products.Add(product);
        return product;
    }

    private void AddStock(Product product, decimal amount) =>
        _context.Stock.Add(new StockEntry
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductId = product.Id, Amount = amount,
            LocationId = _kitchen, StockId = Guid.NewGuid().ToString()
        });

    private ShoppingList AddList()
    {
        var store = new ShoppingLocation { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Store" };
        var list = new ShoppingList { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Groceries", ShoppingLocationId = store.Id };
        _context.ShoppingLocations.Add(store);
        _context.ShoppingLists.Add(list);
        return list;
    }

    private ShoppingListItem AddListItem(ShoppingList list, Product product, decimal amount)
    {
        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ShoppingListId = list.Id, ProductId = product.Id, Amount = amount
        };
        _context.ShoppingListItems.Add(item);
        return item;
    }

    private Guid AddRecipe(params RecipePosition[] ingredients)
    {
        var recipe = new Recipe { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Bread", Servings = 4 };
        var step = new RecipeStep { Id = Guid.NewGuid(), TenantId = _tenantId, RecipeId = recipe.Id, StepOrder = 1 };
        _context.Recipes.Add(recipe);
        _context.Set<RecipeStep>().Add(step);
        for (var i = 0; i < ingredients.Length; i++)
        {
            ingredients[i].RecipeStepId = step.Id;
            ingredients[i].SortOrder = i;
            _context.Set<RecipePosition>().Add(ingredients[i]);
        }
        return recipe.Id;
    }

    private RecipePosition Ingredient(Product product, decimal amount, Guid unitId) => new()
    {
        Id = Guid.NewGuid(), TenantId = _tenantId, ProductId = product.Id, Amount = amount, QuantityUnitId = unitId
    };
}
