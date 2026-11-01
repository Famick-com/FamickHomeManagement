using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// A recipe's ingredients summed across its steps: one line per product and unit, since there is no
/// unit conversion to combine "1 cup" with "2 tbsp".
/// </summary>
public class RecipeConsolidatedIngredientsTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly HomeManagementDbContext _context;
    private readonly RecipeService _service;

    private readonly Guid _cup;
    private readonly Guid _tablespoon;
    private readonly Guid _flour;
    private readonly Guid _butter;
    private readonly Guid _salt;

    public RecipeConsolidatedIngredientsTests()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantProvider.Object);
        _service = new RecipeService(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            NullLogger<RecipeService>.Instance);

        _cup = AddUnit("Cup");
        _tablespoon = AddUnit("Tablespoon");
        _flour = AddProduct("Flour");
        _butter = AddProduct("Butter");
        _salt = AddProduct("Salt");
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task The_same_product_and_unit_in_two_steps_is_summed_into_one_line()
    {
        var recipeId = AddRecipe("Bread",
            [Ingredient(_flour, 1, _cup)],
            [Ingredient(_butter, 2, _tablespoon)],
            [Ingredient(_flour, 1, _cup)]);
        await _context.SaveChangesAsync();

        var recipe = await _service.GetByIdAsync(recipeId);

        recipe!.ConsolidatedIngredients.Select(i => (i.ProductName, i.TotalAmount, i.QuantityUnitName))
            .Should().Equal(("Flour", 2m, "Cup"), ("Butter", 2m, "Tablespoon"));
    }

    [Fact]
    public async Task The_same_product_in_two_units_stays_as_two_lines()
    {
        var recipeId = AddRecipe("Roux",
            [Ingredient(_flour, 1, _cup)],
            [Ingredient(_flour, 2, _tablespoon)]);
        await _context.SaveChangesAsync();

        var recipe = await _service.GetByIdAsync(recipeId);

        recipe!.ConsolidatedIngredients.Select(i => (i.ProductName, i.TotalAmount, i.QuantityUnitName))
            .Should().Equal(("Flour", 1m, "Cup"), ("Flour", 2m, "Tablespoon"));
    }

    [Fact]
    public async Task Ingredients_that_are_not_stock_checked_are_still_listed()
    {
        var recipeId = AddRecipe("Pasta",
            [Ingredient(_salt, 1, _tablespoon, notStockChecked: true)]);
        await _context.SaveChangesAsync();

        var recipe = await _service.GetByIdAsync(recipeId);

        recipe!.ConsolidatedIngredients.Should().ContainSingle(i => i.ProductId == _salt);
    }

    [Fact]
    public async Task Lines_follow_step_order_and_carry_their_distinct_notes()
    {
        var recipeId = AddRecipe("Cookies",
            [Ingredient(_butter, 1, _cup, note: "softened")],
            [Ingredient(_flour, 2, _cup)],
            [Ingredient(_butter, 1, _cup, note: "Softened"), Ingredient(_butter, 1, _cup, note: "melted")]);
        await _context.SaveChangesAsync();

        var recipe = await _service.GetByIdAsync(recipeId);

        recipe!.ConsolidatedIngredients.Select(i => i.ProductName).Should().Equal("Butter", "Flour");
        recipe.ConsolidatedIngredients[0].TotalAmount.Should().Be(3m);
        recipe.ConsolidatedIngredients[0].Note.Should().Be("softened; melted");
        recipe.ConsolidatedIngredients[1].Note.Should().BeNull();
    }

    [Fact]
    public async Task Nested_recipes_contribute_their_ingredients()
    {
        var crustId = AddRecipe("Crust", [Ingredient(_flour, 2, _cup)]);
        var pieId = AddRecipe("Pie", [Ingredient(_flour, 1, _cup), Ingredient(_butter, 1, _cup)]);
        _context.Set<RecipeNesting>().Add(new RecipeNesting
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, RecipeId = pieId, IncludesRecipeId = crustId
        });
        await _context.SaveChangesAsync();

        var recipe = await _service.GetByIdAsync(pieId);

        recipe!.ConsolidatedIngredients.Select(i => (i.ProductName, i.TotalAmount))
            .Should().Equal(("Flour", 3m), ("Butter", 1m));
    }

    [Fact]
    public async Task Aggregated_ingredients_no_longer_sum_across_units()
    {
        var recipeId = AddRecipe("Roux",
            [Ingredient(_flour, 1, _cup)],
            [Ingredient(_flour, 2, _tablespoon), Ingredient(_salt, 1, _tablespoon, notStockChecked: true)]);
        await _context.SaveChangesAsync();

        var ingredients = await _service.GetAggregatedIngredientsAsync(recipeId, servings: 8);

        // 4 servings scaled to 8; salt is skipped because stock checks ignore it.
        ingredients.Select(i => (i.ProductName, i.TotalAmount, i.QuantityUnitName))
            .Should().Equal(("Flour", 2m, "Cup"), ("Flour", 4m, "Tablespoon"));
    }

    [Fact]
    public async Task A_product_in_two_units_lands_on_the_shopping_list_once()
    {
        var recipeId = AddRecipe("Roux",
            [Ingredient(_flour, 1, _cup)],
            [Ingredient(_flour, 2, _tablespoon)]);
        _context.Set<StockEntry>().Add(new StockEntry
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductId = _flour, Amount = 1.5m
        });
        var list = new ShoppingList { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Groceries" };
        _context.Set<ShoppingList>().Add(list);
        await _context.SaveChangesAsync();

        await _service.AddToShoppingListAsync(recipeId, new AddToShoppingListRequest { ShoppingListId = list.Id });

        // Each line is compared with the whole 1.5: the 1 cup is covered, the 2 tbsp is 0.5 short.
        var item = await _context.Set<ShoppingListItem>().SingleAsync();
        item.ProductId.Should().Be(_flour);
        item.Amount.Should().Be(0.5m);
    }

    [Fact]
    public async Task Fulfillment_compares_each_unit_line_with_the_whole_stock()
    {
        var recipeId = AddRecipe("Roux",
            [Ingredient(_flour, 1, _cup)],
            [Ingredient(_flour, 2, _tablespoon)]);
        _context.Set<StockEntry>().Add(new StockEntry
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductId = _flour, Amount = 2.5m
        });
        await _context.SaveChangesAsync();

        var fulfillment = await _service.CheckFulfillmentAsync(recipeId);

        // There is no unit conversion, so 1 cup is not taken off the stock before the 2 tbsp line is checked.
        fulfillment.Ingredients.Select(i => (i.QuantityUnitName, i.AvailableAmount, i.IsSufficient))
            .Should().Equal(("Cup", 2.5m, true), ("Tablespoon", 2.5m, true));
        fulfillment.CanBeMade.Should().BeTrue();
    }

    private Guid AddUnit(string name)
    {
        var unit = new QuantityUnit { Id = Guid.NewGuid(), TenantId = _tenantId, Name = name, NamePlural = name + "s" };
        _context.Set<QuantityUnit>().Add(unit);
        return unit.Id;
    }

    private Guid AddProduct(string name)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = name,
            QuantityUnitIdPurchase = _cup, QuantityUnitIdStock = _cup
        };
        _context.Set<Product>().Add(product);
        return product.Id;
    }

    private Guid AddRecipe(string name, params RecipePosition[][] steps)
    {
        var recipe = new Recipe { Id = Guid.NewGuid(), TenantId = _tenantId, Name = name, Servings = 4 };
        _context.Recipes.Add(recipe);

        // Added in reverse so the result depends on StepOrder, not insertion order.
        for (var s = steps.Length - 1; s >= 0; s--)
        {
            var step = new RecipeStep { Id = Guid.NewGuid(), TenantId = _tenantId, RecipeId = recipe.Id, StepOrder = s + 1 };
            _context.Set<RecipeStep>().Add(step);
            for (var i = 0; i < steps[s].Length; i++)
            {
                steps[s][i].RecipeStepId = step.Id;
                steps[s][i].SortOrder = i;
                _context.Set<RecipePosition>().Add(steps[s][i]);
            }
        }

        return recipe.Id;
    }

    private RecipePosition Ingredient(Guid productId, decimal amount, Guid unitId, string? note = null, bool notStockChecked = false) =>
        new()
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductId = productId, Amount = amount,
            QuantityUnitId = unitId, Note = note, NotCheckStockFulfillment = notStockChecked
        };
}
