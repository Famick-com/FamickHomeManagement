using System.Text;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Net;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.RecipeImport;

/// <summary>
/// Importing a recipe from a page: fetched only through the outbound fetcher, matched to products
/// exactly, and saved whole or not at all.
/// </summary>
public class RecipeImportServiceTests : IDisposable
{
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];

    /// <summary>Serves pages by URL; anything else is a 404. Keeps every request.</summary>
    private sealed class FakeFetcher : IOutboundHttpFetcher
    {
        private readonly OutboundHttpFetcher _urlRules = new(
            null!, new OutboundFetchOptions(), allowPrivateNetworks: false, NullLogger<OutboundHttpFetcher>.Instance);

        public Dictionary<string, (byte[] Body, string? ContentType)> Pages { get; } = new();
        public List<(Uri Url, long MaxBytes, IReadOnlyDictionary<string, string>? Headers)> Requests { get; } = new();

        public OutboundFetchFailure? CheckUrl(Uri url) => _urlRules.CheckUrl(url);

        public Task<OutboundFetchResult> GetAsync(Uri url, long maxBytes, IReadOnlyDictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((url, maxBytes, headers));
            return Pages.TryGetValue(url.AbsoluteUri, out var page)
                ? Task.FromResult(new OutboundFetchResult(page.Body, page.ContentType, url))
                : throw new OutboundFetchException(OutboundFetchFailure.HttpError, 404);
        }
    }

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly HomeManagementDbContext _context;
    private readonly FakeFetcher _fetcher = new();
    private readonly RecipeImportService _service;

    private readonly Guid _tablespoon;
    private readonly Guid _cup;

    public RecipeImportServiceTests()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantProvider.Object);

        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.SaveRecipeImageAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Stream _, string name, CancellationToken _) => "stored-" + name);

        var recipes = new RecipeService(_context, storage.Object, Mock.Of<IFileUrlService>(), NullLogger<RecipeService>.Instance);
        var products = new ProductsService(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(), _fetcher,
            Mock.Of<IMasterProductImageResolver>(), Mock.Of<IProductSearchService>(), tenantProvider.Object,
            NullLogger<ProductsService>.Instance);
        _service = new RecipeImportService(_context, _fetcher, products, recipes, NullLogger<RecipeImportService>.Instance);

        _context.Locations.Add(new Location { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Pantry", IsActive = true });
        _tablespoon = AddUnit("Tbsp", "Tbsp", StandardUnit.Tablespoon);
        _cup = AddUnit("Cup", "Cups", StandardUnit.Cup);
        AddUnit("Piece", "Pieces", null);
        AddUnit("Clove", "Cloves", null);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task A_recipe_page_imports_with_its_steps_ingredients_photo_and_credit()
    {
        Serve("https://kitchen.example/lemon-chicken/", RecipePageFixtures.WordPressGraph);
        ServeImage("https://kitchen.example/wp-content/uploads/lemon-chicken-1200x1200.jpg");

        var result = await _service.ImportAsync("https://kitchen.example/lemon-chicken/", false, null);

        var recipe = result.Recipe;
        recipe.Name.Should().Be("Weeknight Lemon Chicken & Rice");
        recipe.Servings.Should().Be(4);
        recipe.Notes.Should().Be("A bright, one-pan dinner.");
        recipe.Source.Should().Be("https://kitchen.example/lemon-chicken/");
        recipe.Attribution.Should().Be("Recipe by Jordan Example from Example Kitchen Blog");
        recipe.Steps.Select(s => (s.StepOrder, s.Title)).Should().Equal((1, "Prepare"), (2, null), (3, "Cook"), (4, null));
        recipe.Steps[0].Ingredients.Select(i => i.OriginalText).Should().Equal(
            "2 lbs boneless skinless chicken thighs", "1 cup long grain rice", "2 tablespoons olive oil",
            "1 lemon, juiced", "3 cloves garlic, minced", "salt to taste");
        recipe.Steps.Skip(1).Should().OnlyContain(s => s.Ingredients.Count == 0);

        var image = await _context.Set<RecipeImage>().SingleAsync();
        image.IsPrimary.Should().BeTrue();
        image.ContentType.Should().Be("image/png");
        image.ExternalSource.Should().Be(RecipeImportService.ImageExternalSource);
        image.ExternalUrl.Should().BeNull("the stored copy is served, not the recipe site's");
    }

    [Fact]
    public async Task A_line_matching_a_household_product_is_linked_with_its_amount_and_unit()
    {
        var oil = AddProduct("Olive Oil");
        await _context.SaveChangesAsync();
        Serve("https://example.test/r", Page("2 tablespoons olive oil, divided"));

        var result = await _service.ImportAsync("https://example.test/r", false, null);

        var line = result.Recipe.Steps[0].Ingredients.Single();
        line.ProductId.Should().Be(oil);
        line.Amount.Should().Be(2m);
        line.QuantityUnitId.Should().Be(_tablespoon);
        line.Note.Should().Be("divided");
        line.OriginalText.Should().Be("2 tablespoons olive oil, divided");
        result.Ingredients.Single().Outcome.Should().Be(ImportedIngredientOutcome.LinkedFromHousehold);
    }

    [Fact]
    public async Task A_generic_household_product_is_chosen_over_a_branded_one()
    {
        AddProduct("Butter", brand: "Acme");
        var generic = AddProduct("Butter");
        await _context.SaveChangesAsync();
        Serve("https://example.test/r", Page("1 cup unsalted butter"));

        var result = await _service.ImportAsync("https://example.test/r", false, null);

        result.Recipe.Steps[0].Ingredients.Single().ProductId.Should().Be(generic);
    }

    [Fact]
    public async Task A_line_matching_a_generic_catalog_product_adds_it_once_to_the_household()
    {
        var flour = AddMaster("Flour");
        await _context.SaveChangesAsync();
        Serve("https://example.test/bread", Page("2 cups all-purpose flour, sifted"));
        Serve("https://example.test/cake", Page("3 cups flour"));

        var first = await _service.ImportAsync("https://example.test/bread", false, null);
        var second = await _service.ImportAsync("https://example.test/cake", false, null);

        var product = await _context.Products.SingleAsync();
        product.MasterProductId.Should().Be(flour);
        first.Ingredients.Single().Outcome.Should().Be(ImportedIngredientOutcome.AddedFromCatalog);
        first.Recipe.Steps[0].Ingredients.Single().Should().Match<RecipeIngredientDto>(i =>
            i.ProductId == product.Id && i.Amount == 2m && i.QuantityUnitId == _cup);
        second.Ingredients.Single().Outcome.Should().Be(ImportedIngredientOutcome.LinkedFromHousehold);
        second.Recipe.Steps[0].Ingredients.Single().ProductId.Should().Be(product.Id);
    }

    [Fact]
    public async Task A_catalog_product_named_with_its_description_or_a_qualifier_is_matched()
    {
        var flour = AddMaster("All-Purpose Flour");
        var eggs = AddMaster("Eggs (Large)");
        AddMaster("Flour (Rice)");
        await _context.SaveChangesAsync();
        Serve("https://example.test/r", Page("2 cups all-purpose flour, sifted", "3 large eggs"));

        var result = await _service.ImportAsync("https://example.test/r", false, null);

        result.Ingredients.Should().OnlyContain(i => i.Outcome == ImportedIngredientOutcome.AddedFromCatalog);
        result.Recipe.Steps[0].Ingredients.Select(i => i.Note).Should().Equal("sifted", "large");
        (await _context.Products.Select(p => p.MasterProductId).ToListAsync()).Should().BeEquivalentTo(new Guid?[] { flour, eggs });
    }

    [Fact]
    public async Task A_generic_household_product_wins_over_a_branded_one_with_the_described_name()
    {
        AddProduct("All-Purpose Flour", brand: "Acme");
        var generic = AddProduct("Flour");
        await _context.SaveChangesAsync();
        Serve("https://example.test/r", Page("2 cups all-purpose flour"));

        var result = await _service.ImportAsync("https://example.test/r", false, null);

        result.Recipe.Steps[0].Ingredients.Single().ProductId.Should().Be(generic);
    }

    [Fact]
    public async Task Among_generic_household_products_the_described_name_wins()
    {
        AddProduct("Flour");
        var allPurpose = AddProduct("All-Purpose Flour");
        await _context.SaveChangesAsync();
        Serve("https://example.test/r", Page("2 cups all-purpose flour"));

        var result = await _service.ImportAsync("https://example.test/r", false, null);

        result.Recipe.Steps[0].Ingredients.Single().ProductId.Should().Be(allPurpose);
    }

    [Fact]
    public async Task A_household_product_by_the_plain_name_wins_over_the_catalog_by_the_described_name()
    {
        AddMaster("All-Purpose Flour");
        var householdFlour = AddProduct("Flour");
        await _context.SaveChangesAsync();
        Serve("https://example.test/r", Page("2 cups all-purpose flour"));

        var result = await _service.ImportAsync("https://example.test/r", false, null);

        result.Recipe.Steps[0].Ingredients.Single().ProductId.Should().Be(householdFlour);
        (await _context.Products.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Branded_child_and_pending_catalog_products_are_never_picked()
    {
        var milk = AddMaster("Milk");
        AddMaster("Whole Milk", parent: milk);
        AddMaster("Chicken Stock", brand: "Acme");
        AddMaster("Cardamom", status: MasterProductReviewStatus.Pending, contributedBy: _tenantId);
        await _context.SaveChangesAsync();
        Serve("https://example.test/r", Page("1 cup whole milk", "2 cups chicken stock", "1 tsp cardamom"));

        var result = await _service.ImportAsync("https://example.test/r", false, null);

        result.Ingredients.Should().OnlyContain(i => i.Outcome == ImportedIngredientOutcome.Unlinked);
        (await _context.Products.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_line_matching_nothing_is_kept_as_its_text()
    {
        AddProduct("Salt");
        await _context.SaveChangesAsync();
        Serve("https://example.test/r", Page("1 pinch saffron threads", "salt to taste"));

        var result = await _service.ImportAsync("https://example.test/r", false, null);

        // "salt to taste" names a product but has no amount, so it stays text too.
        result.Recipe.Steps[0].Ingredients.Should().OnlyContain(i => i.ProductId == null && i.Amount == 0m);
        result.Recipe.Steps[0].Ingredients.Select(i => i.OriginalText).Should().Equal("1 pinch saffron threads", "salt to taste");
        result.Ingredients.Should().OnlyContain(i => i.Outcome == ImportedIngredientOutcome.Unlinked);
    }

    [Fact]
    public async Task A_page_without_a_recipe_is_refused_and_nothing_is_saved()
    {
        Serve("https://example.test/tips", RecipePageFixtures.NoRecipe);

        var import = () => _service.ImportAsync("https://example.test/tips", false, null);

        (await import.Should().ThrowAsync<RecipeImportException>()).Which.Code.Should().Be(RecipeImportException.NoRecipeFound);
        (await _context.Recipes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_page_that_is_not_html_is_refused()
    {
        _fetcher.Pages["https://example.test/feed.json"] = (Encoding.UTF8.GetBytes("{}"), "application/json");

        var import = () => _service.ImportAsync("https://example.test/feed.json", false, null);

        (await import.Should().ThrowAsync<RecipeImportException>()).Which.Code.Should().Be(RecipeImportException.NotAWebPage);
    }

    [Fact]
    public async Task A_page_that_cannot_be_fetched_is_reported_as_unreachable()
    {
        var import = () => _service.ImportAsync("https://example.test/missing", false, null);

        (await import.Should().ThrowAsync<RecipeImportException>()).Which.Code.Should().Be(RecipeImportException.PageUnreachable);
    }

    [Fact]
    public async Task An_address_the_fetcher_refuses_is_not_requested()
    {
        var import = () => _service.ImportAsync("http://example.test:6379/", false, null);

        (await import.Should().ThrowAsync<RecipeImportException>()).Which.Code.Should().Be(RecipeImportException.UrlNotAllowed);
        _fetcher.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Importing_the_same_page_twice_is_refused_unless_asked()
    {
        Serve("https://example.test/r", Page("1 egg"));
        var first = await _service.ImportAsync("https://example.test/r", false, null);
        var requestsAfterFirst = _fetcher.Requests.Count;

        var again = () => _service.ImportAsync("https://example.test/r", false, null);
        (await again.Should().ThrowAsync<RecipeAlreadyImportedException>())
            .Which.ExistingRecipeId.Should().Be(first.Recipe.Id);
        _fetcher.Requests.Should().HaveCount(requestsAfterFirst, "the pasted link matches before anything is fetched");

        var duplicate = await _service.ImportAsync("https://example.test/r", true, null);
        duplicate.Recipe.Id.Should().NotBe(first.Recipe.Id);
        (await _context.Recipes.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_page_already_imported_under_its_canonical_address_is_a_duplicate()
    {
        Serve("https://kitchen.example/lemon-chicken/", RecipePageFixtures.WordPressGraph);
        Serve("https://kitchen.example/lemon-chicken/?utm_source=share", RecipePageFixtures.WordPressGraph);
        var first = await _service.ImportAsync("https://kitchen.example/lemon-chicken/", false, null);

        var again = () => _service.ImportAsync("https://kitchen.example/lemon-chicken/?utm_source=share", false, null);

        (await again.Should().ThrowAsync<RecipeAlreadyImportedException>())
            .Which.ExistingRecipeId.Should().Be(first.Recipe.Id);
    }

    [Fact]
    public async Task A_photo_that_cannot_be_fetched_leaves_the_recipe_without_one()
    {
        Serve("https://kitchen.example/lemon-chicken/", RecipePageFixtures.WordPressGraph);

        var result = await _service.ImportAsync("https://kitchen.example/lemon-chicken/", false, null);

        result.Recipe.Name.Should().Be("Weeknight Lemon Chicken & Rice");
        (await _context.Set<RecipeImage>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task The_page_and_photo_are_fetched_through_the_fetcher_with_caps_and_browser_headers()
    {
        Serve("https://kitchen.example/lemon-chicken/", RecipePageFixtures.WordPressGraph);
        ServeImage("https://kitchen.example/wp-content/uploads/lemon-chicken-1200x1200.jpg");

        await _service.ImportAsync("https://kitchen.example/lemon-chicken/", false, null);

        _fetcher.Requests.Select(r => (r.Url.AbsoluteUri, r.MaxBytes)).Should().Equal(
            ("https://kitchen.example/lemon-chicken/", RecipeImportService.MaxPageBytes),
            ("https://kitchen.example/wp-content/uploads/lemon-chicken-1200x1200.jpg", RecipeImportService.MaxImageBytes));
        _fetcher.Requests.Select(r => r.Headers).Should().Equal(RecipeImportService.PageHeaders, RecipeImportService.ImageHeaders);
        RecipeImportService.PageHeaders["User-Agent"].Should().StartWith("Mozilla/5.0");
        RecipeImportService.ImageHeaders["Accept"].Should().NotContain("avif");
    }

    private void Serve(string url, string html) => _fetcher.Pages[url] = (Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8");

    private void ServeImage(string url) => _fetcher.Pages[url] = (PngBytes, "image/jpeg");

    private static string Page(params string[] ingredients) =>
        $$"""
        <html><head><script type="application/ld+json">
        { "@type": "Recipe", "name": "Test Recipe", "recipeIngredient": {{JsonSerializer.Serialize(ingredients)}},
          "recipeInstructions": "Mix everything." }
        </script></head></html>
        """;

    private Guid AddUnit(string name, string plural, StandardUnit? standard)
    {
        var unit = new QuantityUnit
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = name, NamePlural = plural, IsActive = true,
            StandardUnit = standard
        };
        _context.QuantityUnits.Add(unit);
        return unit.Id;
    }

    private Guid AddProduct(string name, string? brand = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = name, Brand = brand, IsActive = true,
            QuantityUnitIdPurchase = _cup, QuantityUnitIdStock = _cup
        };
        _context.Products.Add(product);
        return product.Id;
    }

    private Guid AddMaster(string name, string? brand = null, Guid? parent = null,
        MasterProductReviewStatus status = MasterProductReviewStatus.Approved, Guid? contributedBy = null)
    {
        var master = new MasterProduct
        {
            Id = Guid.NewGuid(), Name = name, Brand = brand, Category = "Baking", ParentMasterProductId = parent,
            ReviewStatus = status, ContributedByTenantId = contributedBy
        };
        _context.MasterProducts.Add(master);
        return master.Id;
    }
}
