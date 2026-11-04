using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Making a recipe takes its ingredients out of stock (#158): flour used by the cup, from a generic flour
/// and its brands, one of them kept in tablespoons.
/// </summary>
public class RecipeConsumeTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly HomeManagementDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly DateTime _today = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);

    private readonly Guid _kitchen;
    private readonly Guid _bag;
    private readonly Guid _cup;
    private readonly Guid _tablespoon;
    private readonly Guid _gram;
    private readonly Product _flour;
    private readonly Product _kingArthur;
    private readonly Product _oldFlour;
    private readonly Product _sugar;

    public RecipeConsumeTests()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        tenantProvider.Setup(t => t.UserId).Returns(_userId);
        _tenantProvider = tenantProvider.Object;
        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            _tenantProvider);

        var kitchen = new Location { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Kitchen" };
        _context.Locations.Add(kitchen);
        _kitchen = kitchen.Id;

        _bag = AddUnit("Bag", null);
        _cup = AddUnit("Cup", StandardUnit.Cup);
        _tablespoon = AddUnit("Tablespoon", StandardUnit.Tablespoon);
        _gram = AddUnit("Gram", StandardUnit.Gram);

        _flour = AddProduct("Flour", _cup);
        _kingArthur = AddProduct("King Arthur 5 lb", _cup, _flour);
        _oldFlour = AddProduct("Old flour", _tablespoon, _flour);
        _sugar = AddProduct("Sugar", _cup);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task It_takes_from_the_earliest_expiring_entry_first()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 2, _cup));
        var later = AddStock(_flour, 4m, bestBefore: _today.AddDays(30));
        var sooner = AddStock(_flour, 1m, bestBefore: _today.AddDays(2));
        await _context.SaveChangesAsync();

        var result = await Stock().ConsumeForRecipeAsync(recipeId, Request(Line(_flour, 2, _cup)));

        (await _context.Stock.AnyAsync(s => s.Id == sooner.Id)).Should().BeFalse("the entry expiring first is used up first");
        (await _context.Stock.SingleAsync(s => s.Id == later.Id)).Amount.Should().Be(3m);
        result.Ingredients.Single().ConsumedAmount.Should().Be(2m);
        result.Ingredients.Single().MissingAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Each_entry_it_takes_from_gets_a_journal_row_tagged_with_the_recipe()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 2, _cup));
        AddStock(_flour, 1m, bestBefore: _today.AddDays(2));
        AddStock(_flour, 4m, bestBefore: _today.AddDays(30));
        await _context.SaveChangesAsync();

        await Stock().ConsumeForRecipeAsync(recipeId, Request(Line(_flour, 2, _cup)));

        var rows = await _context.StockLog.ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.RecipeId == recipeId && r.TransactionType == "consume" && r.UserId == _userId);
        rows.Select(r => r.Amount).Should().BeEquivalentTo([-1m, -1m]);
        rows.Select(r => r.CorrelationId).Distinct().Should().ContainSingle("one meal's rows are grouped");

        var journal = await Stock().GetLogAsync();
        journal.Should().OnlyContain(l => l.RecipeId == recipeId && l.RecipeName == "Bread");
    }

    [Fact]
    public async Task A_line_short_of_stock_takes_what_there_is_and_reports_the_rest()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 2, _cup), Ingredient(_sugar, 1, _cup));
        AddStock(_flour, 0.5m);
        AddStock(_sugar, 3m);
        await _context.SaveChangesAsync();

        var result = await Stock().ConsumeForRecipeAsync(recipeId, Request(Line(_flour, 2, _cup), Line(_sugar, 1, _cup)));

        var flour = result.Ingredients[0];
        flour.ConsumedAmount.Should().Be(0.5m);
        flour.MissingAmount.Should().Be(1.5m);
        flour.QuantityUnitName.Should().Be("Cup");
        result.Ingredients[1].MissingAmount.Should().Be(0m, "the other lines are still consumed");
        (await _context.Stock.AnyAsync(s => s.ProductId == _flour.Id)).Should().BeFalse();
        (await _context.Stock.SingleAsync(s => s.ProductId == _sugar.Id)).Amount.Should().Be(2m);
    }

    [Fact]
    public async Task A_line_left_out_is_not_consumed_and_an_edited_amount_is_consumed_as_edited()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 2, _cup), Ingredient(_sugar, 1, _cup));
        AddStock(_flour, 5m);
        AddStock(_sugar, 3m);
        await _context.SaveChangesAsync();

        await Stock().ConsumeForRecipeAsync(recipeId, Request(Line(_sugar, 1.5m, _cup)));

        (await _context.Stock.SingleAsync(s => s.ProductId == _flour.Id)).Amount.Should().Be(5m);
        (await _context.Stock.SingleAsync(s => s.ProductId == _sugar.Id)).Amount.Should().Be(1.5m);
    }

    [Fact]
    public async Task A_line_in_another_unit_is_converted_to_the_stock_unit()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 2, _tablespoon));
        AddStock(_flour, 1m);
        await _context.SaveChangesAsync();

        var result = await Stock().ConsumeForRecipeAsync(recipeId, Request(Line(_flour, 2, _tablespoon)));

        (await _context.Stock.SingleAsync()).Amount.Should().Be(0.875m, "2 tablespoons is an eighth of a cup");
        result.Ingredients.Single().ConsumedAmount.Should().Be(2m, "reported in the line's own unit");
    }

    [Fact]
    public async Task A_generic_ingredient_takes_from_its_childrens_entries_in_their_own_units()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 1.5m, _cup));
        var kingArthur = AddStock(_kingArthur, 1m, bestBefore: _today.AddDays(30));
        var oldFlour = AddStock(_oldFlour, 16m, bestBefore: _today.AddDays(2));
        await _context.SaveChangesAsync();

        var result = await Stock().ConsumeForRecipeAsync(recipeId, Request(Line(_flour, 1.5m, _cup)));

        (await _context.Stock.AnyAsync(s => s.Id == oldFlour.Id)).Should().BeFalse("16 tablespoons is the first cup");
        (await _context.Stock.SingleAsync(s => s.Id == kingArthur.Id)).Amount.Should().Be(0.5m);
        result.Ingredients.Single().MissingAmount.Should().Be(0m);
        (await _context.StockLog.SingleAsync(l => l.ProductId == _oldFlour.Id)).Amount.Should().Be(-16m);
    }

    [Fact]
    public async Task A_child_line_goes_first_so_a_generic_line_does_not_use_up_its_stock()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 1, _cup), Ingredient(_kingArthur, 1, _cup));
        AddStock(_kingArthur, 1m, bestBefore: _today.AddDays(2));
        AddStock(_flour, 1m, bestBefore: _today.AddDays(30));
        await _context.SaveChangesAsync();

        var result = await Stock().ConsumeForRecipeAsync(recipeId, Request(Line(_flour, 1, _cup), Line(_kingArthur, 1, _cup)));

        result.Ingredients.Should().OnlyContain(i => i.MissingAmount == 0m);
        (await _context.Stock.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task A_line_without_a_unit_is_in_the_stock_unit()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 100, _gram));
        AddStock(_flour, 2m);
        await _context.SaveChangesAsync();

        var result = await Stock().ConsumeForRecipeAsync(recipeId, Request(Line(_flour, 0.75m, null)));

        (await _context.Stock.SingleAsync()).Amount.Should().Be(1.25m);
        result.Ingredients.Single().QuantityUnitName.Should().Be("Cup");
    }

    [Fact]
    public async Task A_unit_that_does_not_convert_is_refused_and_nothing_is_consumed()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 100, _gram), Ingredient(_sugar, 1, _cup));
        AddStock(_flour, 2m);
        AddStock(_sugar, 2m);
        await _context.SaveChangesAsync();

        var act = () => Stock().ConsumeForRecipeAsync(recipeId, Request(Line(_sugar, 1, _cup), Line(_flour, 100, _gram)));

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
        _context.ChangeTracker.Clear();
        (await _context.Stock.SumAsync(s => s.Amount)).Should().Be(4m);
        (await _context.StockLog.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task An_unknown_recipe_is_not_found()
    {
        var act = () => Stock().ConsumeForRecipeAsync(Guid.NewGuid(), Request(Line(_flour, 1, _cup)));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task The_stock_check_gives_each_line_its_unit_to_send_back()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 2, _tablespoon));
        await _context.SaveChangesAsync();

        var fulfillment = await Recipes().CheckFulfillmentAsync(recipeId);

        fulfillment.Ingredients.Single().QuantityUnitId.Should().Be(_tablespoon);
    }

    [Fact]
    public async Task Adding_what_was_short_to_a_list_adds_only_the_products_asked_for()
    {
        var recipeId = AddRecipe(Ingredient(_flour, 2, _cup), Ingredient(_sugar, 1, _cup));
        var list = AddList();
        await _context.SaveChangesAsync();

        await Recipes().AddToShoppingListAsync(recipeId, new AddToShoppingListRequest
        {
            ShoppingListId = list.Id,
            ProductIds = [_sugar.Id]
        });

        var items = await _context.ShoppingListItems.ToListAsync();
        items.Should().ContainSingle(i => i.ProductId == _sugar.Id && i.Amount == 1m);
    }

    private StockService Stock() => new(_context, _tenantProvider, Mock.Of<IFileUrlService>());

    private RecipeService Recipes() =>
        new(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(), NullLogger<RecipeService>.Instance);

    private static ConsumeRecipeRequest Request(params ConsumeRecipeLine[] lines) => new() { Lines = [.. lines] };

    private static ConsumeRecipeLine Line(Product product, decimal amount, Guid? unitId) =>
        new() { ProductId = product.Id, Amount = amount, QuantityUnitId = unitId };

    private Guid AddUnit(string name, StandardUnit? standard)
    {
        var unit = new QuantityUnit { Id = Guid.NewGuid(), TenantId = _tenantId, Name = name, NamePlural = name + "s", StandardUnit = standard };
        _context.QuantityUnits.Add(unit);
        return unit.Id;
    }

    private Product AddProduct(string name, Guid stockUnit, Product? parent = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = name, LocationId = _kitchen, IsActive = true,
            QuantityUnitIdPurchase = _bag, QuantityUnitIdStock = stockUnit,
            QuantityUnitFactorPurchaseToStock = 1m, ParentProductId = parent?.Id
        };
        _context.Products.Add(product);
        return product;
    }

    private StockEntry AddStock(Product product, decimal amount, DateTime? bestBefore = null)
    {
        var entry = new StockEntry
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductId = product.Id, Amount = amount,
            LocationId = _kitchen, StockId = Guid.NewGuid().ToString(), BestBeforeDate = bestBefore,
            PurchasedDate = _today
        };
        _context.Stock.Add(entry);
        return entry;
    }

    private ShoppingList AddList()
    {
        var store = new ShoppingLocation { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Store" };
        var list = new ShoppingList { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Groceries", ShoppingLocationId = store.Id };
        _context.ShoppingLocations.Add(store);
        _context.ShoppingLists.Add(list);
        return list;
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
