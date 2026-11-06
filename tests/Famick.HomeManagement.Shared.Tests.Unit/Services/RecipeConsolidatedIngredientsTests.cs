using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Recipes;
using ChildPurchaseEntry = Famick.HomeManagement.Core.DTOs.ShoppingLists.ChildPurchaseEntry;
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
/// A recipe's ingredients summed across its steps: one line per product and unit, as the recipe wrote
/// them. The units here aren't tagged with standard measures, so "2 tbsp" doesn't convert to cups; see
/// <see cref="PurchaseAndStockUnitTests"/> for units that do.
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

        var recipe = await _service.GetByIdAsync(recipeId, null);

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

        var recipe = await _service.GetByIdAsync(recipeId, null);

        recipe!.ConsolidatedIngredients.Select(i => (i.ProductName, i.TotalAmount, i.QuantityUnitName))
            .Should().Equal(("Flour", 1m, "Cup"), ("Flour", 2m, "Tablespoon"));
    }

    [Fact]
    public async Task Ingredients_that_are_not_stock_checked_are_still_listed()
    {
        var recipeId = AddRecipe("Pasta",
            [Ingredient(_salt, 1, _tablespoon, notStockChecked: true)]);
        await _context.SaveChangesAsync();

        var recipe = await _service.GetByIdAsync(recipeId, null);

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

        var recipe = await _service.GetByIdAsync(recipeId, null);

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

        var recipe = await _service.GetByIdAsync(pieId, null);

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
    public async Task A_line_that_does_not_convert_is_left_off_the_shopping_list()
    {
        var recipeId = AddRecipe("Roux",
            [Ingredient(_flour, 1, _cup)],
            [Ingredient(_flour, 2, _tablespoon)]);
        _context.Set<StockEntry>().Add(new StockEntry
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductId = _flour, Amount = 0.5m
        });
        var list = new ShoppingList { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Groceries" };
        _context.Set<ShoppingList>().Add(list);
        await _context.SaveChangesAsync();

        await _service.AddToShoppingListAsync(recipeId, new AddToShoppingListRequest { ShoppingListId = list.Id });

        // Tablespoons don't convert to cups here, so only the cup line is bought for: it is 0.5 short.
        var item = await _context.Set<ShoppingListItem>().SingleAsync();
        item.ProductId.Should().Be(_flour);
        item.Amount.Should().Be(0.5m);
    }

    [Fact]
    public async Task Adding_the_same_recipe_twice_leaves_the_list_unchanged_the_second_time()
    {
        var recipeId = AddRecipe("Bread", [Ingredient(_flour, 3, _cup)]);
        AddStock(_flour, 1m);
        var list = AddList();
        await _context.SaveChangesAsync();

        await _service.AddToShoppingListAsync(recipeId, new AddToShoppingListRequest { ShoppingListId = list.Id });
        await _service.AddToShoppingListAsync(recipeId, new AddToShoppingListRequest { ShoppingListId = list.Id });

        var item = await _context.Set<ShoppingListItem>().SingleAsync();
        item.Amount.Should().Be(2m);
    }

    [Fact]
    public async Task A_partly_purchased_item_counts_only_what_is_still_to_buy()
    {
        var recipeId = AddRecipe("Bread", [Ingredient(_flour, 3, _cup)]);
        AddStock(_flour, 1m);
        var list = AddList();
        var item = AddListItem(list, _flour, amount: 2m, purchased: 1.5m);
        await _context.SaveChangesAsync();

        await _service.AddToShoppingListAsync(recipeId, new AddToShoppingListRequest { ShoppingListId = list.Id });

        // 2 short, 0.5 of it still to buy on the list: 1.5 more, on the same item.
        (await _context.Set<ShoppingListItem>().CountAsync()).Should().Be(1);
        item.Amount.Should().Be(3.5m);
    }

    [Fact]
    public async Task A_child_product_checked_off_against_an_item_counts_as_bought()
    {
        var recipeId = AddRecipe("Bread", [Ingredient(_flour, 3, _cup)]);
        AddStock(_flour, 1m);
        var list = AddList();
        var item = AddListItem(list, _flour, amount: 2m);
        item.ChildPurchasesJson = JsonSerializer.Serialize(new List<ChildPurchaseEntry>
        {
            new() { ChildProductId = Guid.NewGuid(), ChildProductName = "Brand flour", Quantity = 1m }
        });
        await _context.SaveChangesAsync();

        await _service.AddToShoppingListAsync(recipeId, new AddToShoppingListRequest { ShoppingListId = list.Id });

        // 2 short, 1 of the listed 2 already bought as a child product: 1 more.
        item.Amount.Should().Be(3m);
    }

    [Fact]
    public async Task An_over_bought_open_item_is_topped_up_from_what_was_bought()
    {
        var recipeId = AddRecipe("Bread", [Ingredient(_flour, 3, _cup)]);
        AddStock(_flour, 1m);
        var list = AddList();
        var item = AddListItem(list, _flour, amount: 3m, purchased: 2m);
        item.ChildPurchasesJson = JsonSerializer.Serialize(new List<ChildPurchaseEntry>
        {
            new() { ChildProductId = Guid.NewGuid(), ChildProductName = "Brand flour", Quantity = 2m }
        });
        await _context.SaveChangesAsync();

        await _service.AddToShoppingListAsync(recipeId, new AddToShoppingListRequest { ShoppingListId = list.Id });

        // 4 bought against 3, so nothing is left; 2 short means 2 more to buy on top of the 4.
        item.Amount.Should().Be(6m);
    }

    [Fact]
    public async Task A_purchased_item_counts_for_nothing_and_a_new_item_is_added()
    {
        var recipeId = AddRecipe("Bread", [Ingredient(_flour, 3, _cup)]);
        AddStock(_flour, 1m);
        var list = AddList();
        var bought = AddListItem(list, _flour, amount: 2m, purchased: 2m, isPurchased: true);
        await _context.SaveChangesAsync();

        await _service.AddToShoppingListAsync(recipeId, new AddToShoppingListRequest { ShoppingListId = list.Id });

        bought.Amount.Should().Be(2m);
        var added = await _context.Set<ShoppingListItem>().SingleAsync(i => i.Id != bought.Id);
        added.Amount.Should().Be(2m);
        added.IsPurchased.Should().BeFalse();
    }

    [Fact]
    public async Task A_line_that_does_not_convert_is_compared_with_the_whole_stock()
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

        // The cup line takes its 1 from the stock; the tablespoon line can't, so it sees all 2.5, in cups.
        fulfillment.Ingredients.Select(i => (i.QuantityUnitName, i.AvailableAmount, i.AvailableQuantityUnitName, i.IsSufficient, i.CanCompare))
            .Should().Equal(("Cup", 2.5m, "Cup", true, true), ("Tablespoon", 2.5m, "Cup", true, false));
        fulfillment.CanBeMade.Should().BeTrue();
        fulfillment.HasUncomparableIngredients.Should().BeTrue();
    }

    [Fact]
    public async Task Unlinked_lines_are_listed_on_their_own_with_their_text()
    {
        var recipeId = AddRecipe("Imported",
            [Ingredient(_flour, 1, _cup), Unlinked("1 pinch saffron"), Unlinked("2 sprigs thyme")]);
        await _context.SaveChangesAsync();

        var recipe = await _service.GetByIdAsync(recipeId, null);

        recipe!.ConsolidatedIngredients.Select(i => (i.ProductId, i.OriginalText))
            .Should().Equal((_flour, null), (null, "1 pinch saffron"), (null, "2 sprigs thyme"));
        recipe.Steps[0].Ingredients.Where(i => i.ProductId == null).Select(i => i.OriginalText)
            .Should().Equal("1 pinch saffron", "2 sprigs thyme");
    }

    [Fact]
    public async Task An_unlinked_line_carries_its_text_as_its_product_name()
    {
        var recipeId = AddRecipe("Imported", [Ingredient(_flour, 1, _cup), Unlinked("1 pinch saffron")]);
        await _context.SaveChangesAsync();

        var recipe = await _service.GetByIdAsync(recipeId, null);

        recipe!.Steps[0].Ingredients.Select(i => i.ProductName).Should().Equal("Flour", "1 pinch saffron");
        recipe.ConsolidatedIngredients.Select(i => i.ProductName).Should().Equal("Flour", "1 pinch saffron");
    }

    [Fact]
    public async Task The_stock_check_skips_unlinked_lines_and_lists_them()
    {
        var recipeId = AddRecipe("Imported",
            [Ingredient(_flour, 1, _cup), Unlinked("1 pinch saffron")]);
        AddStock(_flour, 1);
        await _context.SaveChangesAsync();

        var ingredients = await _service.GetAggregatedIngredientsAsync(recipeId);
        var fulfillment = await _service.CheckFulfillmentAsync(recipeId);

        ingredients.Should().ContainSingle(i => i.ProductId == _flour);
        fulfillment.Ingredients.Should().ContainSingle(i => i.ProductId == _flour);
        fulfillment.UnlinkedIngredients.Should().Equal("1 pinch saffron");
        fulfillment.CanBeMade.Should().BeTrue();
    }

    [Fact]
    public async Task Adding_to_the_shopping_list_skips_unlinked_lines()
    {
        var recipeId = AddRecipe("Imported",
            [Ingredient(_flour, 2, _cup), Unlinked("1 pinch saffron")]);
        var list = AddList();
        await _context.SaveChangesAsync();

        await _service.AddToShoppingListAsync(recipeId, new AddToShoppingListRequest { ShoppingListId = list.Id });

        _context.Set<ShoppingListItem>().AsEnumerable().Select(i => (i.ProductId, i.Amount))
            .Should().Equal((_flour, 2m));
    }

    [Fact]
    public async Task Linking_an_unlinked_line_makes_it_count_in_the_stock_check()
    {
        var line = Unlinked("2 cups butter");
        var recipeId = AddRecipe("Imported", [line]);
        await _context.SaveChangesAsync();

        var linked = await _service.UpdateIngredientAsync(line.Id, new UpdateRecipeIngredientRequest
        {
            ProductId = _butter, Amount = 2, QuantityUnitId = _cup
        });
        var fulfillment = await _service.CheckFulfillmentAsync(recipeId);

        linked.ProductId.Should().Be(_butter);
        linked.ProductName.Should().Be("Butter");
        linked.OriginalText.Should().Be("2 cups butter");
        fulfillment.Ingredients.Should().ContainSingle(i => i.ProductId == _butter && i.RequiredAmount == 2m);
        fulfillment.UnlinkedIngredients.Should().BeEmpty();
    }

    [Fact]
    public async Task Linking_a_line_needs_an_amount()
    {
        var line = Unlinked("butter");
        AddRecipe("Imported", [line]);
        await _context.SaveChangesAsync();

        var link = () => _service.UpdateIngredientAsync(line.Id, new UpdateRecipeIngredientRequest { ProductId = _butter });

        await link.Should().ThrowAsync<Famick.HomeManagement.Core.Exceptions.BusinessRuleViolationException>();
    }

    private void AddStock(Guid productId, decimal amount) =>
        _context.Set<StockEntry>().Add(new StockEntry
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductId = productId, Amount = amount
        });

    private ShoppingList AddList()
    {
        var list = new ShoppingList { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Groceries" };
        _context.Set<ShoppingList>().Add(list);
        return list;
    }

    private ShoppingListItem AddListItem(ShoppingList list, Guid productId, decimal amount, decimal purchased = 0m, bool isPurchased = false)
    {
        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ShoppingListId = list.Id, ProductId = productId,
            Amount = amount, PurchasedQuantity = purchased, IsPurchased = isPurchased
        };
        _context.Set<ShoppingListItem>().Add(item);
        return item;
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

    private RecipePosition Unlinked(string text) =>
        new() { Id = Guid.NewGuid(), TenantId = _tenantId, OriginalText = text };
}
