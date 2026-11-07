using System.Collections.Concurrent;
using Famick.HomeManagement.Core.DTOs.Sharing;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Infrastructure.Sharing;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Sharing;

/// <summary>
/// Copying a household's recipe, product, meal or contact into another household (#195): the read runs
/// as the sharer in the Smiths' household, the write as the recipient in the Joneses', and neither
/// touches the calling request's context.
/// </summary>
public class SharedItemCopierTests : IDisposable
{
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private readonly Guid _smiths = Guid.NewGuid();
    private readonly Guid _jones = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _carol = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly InMemoryDatabaseRoot _root = new();
    private readonly string _database = Guid.NewGuid().ToString();
    private readonly FileStore _files = new();
    private readonly HttpContextAccessor _http = new();
    private readonly ServiceProvider _services;

    private readonly Dictionary<(Guid Tenant, string Name), Guid> _units = new();
    private readonly Dictionary<Guid, Guid> _pantries = new();

    public SharedItemCopierTests()
    {
        _services = Services(new MultiTenancyOptions { IsMultiTenantEnabled = true });

        using var db = Seeding();
        db.Tenants.AddRange(new Tenant { Id = _smiths, Name = "The Smiths" }, new Tenant { Id = _jones, Name = "The Joneses" });
        db.Users.AddRange(
            new User { Id = _alice, TenantId = _smiths, Email = "alice@example.com", Username = "alice" },
            new User { Id = _carol, TenantId = _smiths, Email = "carol@example.com", Username = "carol" },
            new User { Id = _bob, TenantId = _jones, Email = "bob@example.com", Username = "bob" });
        foreach (var tenant in new[] { _smiths, _jones })
        {
            var pantry = new Location { Id = Guid.NewGuid(), TenantId = tenant, Name = "Pantry", IsActive = true };
            db.Locations.Add(pantry);
            _pantries[tenant] = pantry.Id;
            AddUnit(db, tenant, "Piece", "Pieces", null);
            AddUnit(db, tenant, "Cup", "Cups", StandardUnit.Cup);
        }
        db.SaveChanges();

        // The request the copy is made from: Bob's, in his household.
        _http.HttpContext = new DefaultHttpContext();
        _http.HttpContext.Items["TenantId"] = _jones;
        _http.HttpContext.Items["UserId"] = _bob;
    }

    public void Dispose() => _services.Dispose();

    private ISharedItemCopier Copier => _services.CreateScope().ServiceProvider.GetRequiredService<ISharedItemCopier>();

    private SharedItemRef Shared(ShareableKind kind, Guid itemId, Guid? by = null) => new(kind, itemId, _smiths, by ?? _alice);

    private Task<SharedItemCopyResult?> CopyToJonesAsync(ShareableKind kind, Guid itemId) =>
        Copier.CopyAsync(new ShareCopyRequest(Shared(kind, itemId), _jones, _bob));

    // --- recipes ---

    [Fact]
    public async Task A_recipe_comes_across_with_its_steps_lines_and_picture()
    {
        var butter = await AddProductAsync(_smiths, "Butter");
        await AddProductAsync(_jones, "Butter");
        var roux = await AddRecipeAsync(_smiths, "Roux", withImage: true, lines: [(butter, 2m, "2 cups butter"), (null, 0m, "a pinch of patience")]);

        var copy = await CopyToJonesAsync(ShareableKind.Recipe, roux);

        copy!.AlreadyHad.Should().BeFalse();
        using var db = Seeding();
        var recipe = await db.Recipes.Include(r => r.Steps).ThenInclude(s => s.Ingredients).ThenInclude(i => i.Product)
            .Include(r => r.Images).SingleAsync(r => r.Id == copy.ItemId);
        recipe.TenantId.Should().Be(_jones);
        recipe.Name.Should().Be("Roux");
        recipe.SharedByHouseholdName.Should().Be("The Smiths");
        recipe.OriginKey.Should().NotBeNullOrEmpty();

        var lines = recipe.Steps.Single().Ingredients.OrderBy(i => i.SortOrder).ToList();
        lines[0].Product!.TenantId.Should().Be(_jones, "the line links the Joneses' own butter");
        lines[0].Amount.Should().Be(2m);
        lines[0].QuantityUnitId.Should().Be(_units[(_jones, "Cup")]);
        lines[1].ProductId.Should().BeNull();
        lines[1].OriginalText.Should().Be("a pinch of patience");

        var image = recipe.Images.Single();
        image.IsPrimary.Should().BeTrue();
        _files.Has(_jones, $"recipe/{recipe.Id}/{image.FileName}").Should().BeTrue();

        (await db.Recipes.CountAsync(r => r.TenantId == _smiths)).Should().Be(1, "the original stays as it was");
    }

    [Fact]
    public async Task Adding_the_same_recipe_again_returns_the_copy_already_made()
    {
        var roux = await AddRecipeAsync(_smiths, "Roux");

        var first = await CopyToJonesAsync(ShareableKind.Recipe, roux);
        var second = await CopyToJonesAsync(ShareableKind.Recipe, roux);

        second!.AlreadyHad.Should().BeTrue();
        second.ItemId.Should().Be(first!.ItemId);
        using var db = Seeding();
        (await db.Recipes.CountAsync(r => r.TenantId == _jones)).Should().Be(1);
    }

    [Fact]
    public async Task Nested_recipes_come_along_and_a_meal_reuses_a_recipe_already_copied()
    {
        var stock = await AddRecipeAsync(_smiths, "Stock");
        var soup = await AddRecipeAsync(_smiths, "Soup", nests: [stock]);
        var meal = await AddMealAsync(_smiths, "Soup night", recipes: [soup], freetext: "Crusty bread");

        var soupCopy = await CopyToJonesAsync(ShareableKind.Recipe, soup);
        var mealCopy = await CopyToJonesAsync(ShareableKind.Meal, meal);

        using var db = Seeding();
        var copiedMeal = await db.Meals.Include(m => m.Items).SingleAsync(m => m.Id == mealCopy!.ItemId);
        copiedMeal.Items.Single(i => i.ItemType == MealItemType.Recipe).RecipeId.Should().Be(soupCopy!.ItemId);
        copiedMeal.Items.Single(i => i.ItemType == MealItemType.Freetext).FreetextDescription.Should().Be("Crusty bread");

        var nesting = await db.Set<RecipeNesting>().SingleAsync(n => n.RecipeId == soupCopy.ItemId);
        (await db.Recipes.SingleAsync(r => r.Id == nesting.IncludesRecipeId)).Name.Should().Be("Stock");
        (await db.Recipes.CountAsync(r => r.TenantId == _jones)).Should().Be(2);
    }

    [Fact]
    public async Task A_line_whose_product_follows_a_published_catalog_product_adopts_it()
    {
        var master = await AddMasterAsync("All-Purpose Flour", MasterProductReviewStatus.Approved, contributedBy: null);
        var flour = await AddProductAsync(_smiths, "Our flour", masterProductId: master);
        var bread = await AddRecipeAsync(_smiths, "Bread", lines: [(flour, 3m, "3 cups flour")]);

        var copy = await CopyToJonesAsync(ShareableKind.Recipe, bread);

        using var db = Seeding();
        var line = await db.Set<RecipePosition>().Include(p => p.Product).SingleAsync(p => p.RecipeStep.RecipeId == copy!.ItemId);
        line.Product!.TenantId.Should().Be(_jones);
        line.Product.MasterProductId.Should().Be(master);
    }

    [Fact]
    public async Task Links_and_addresses_that_arent_http_are_dropped()
    {
        var recipe = await AddRecipeAsync(_smiths, "Roux", source: "javascript:alert(1)", videoUrl: "data:text/html,hi");
        var kept = await AddRecipeAsync(_smiths, "Gravy", source: "Grandma's cookbook", videoUrl: "https://videos.example/gravy");

        var dropped = await CopyToJonesAsync(ShareableKind.Recipe, recipe);
        var copied = await CopyToJonesAsync(ShareableKind.Recipe, kept);

        using var db = Seeding();
        var first = await db.Recipes.Include(r => r.Steps).SingleAsync(r => r.Id == dropped!.ItemId);
        first.Source.Should().BeNull();
        first.Steps.Single().VideoUrl.Should().BeNull();
        var second = await db.Recipes.Include(r => r.Steps).SingleAsync(r => r.Id == copied!.ItemId);
        second.Source.Should().Be("Grandma's cookbook");
        second.Steps.Single().VideoUrl.Should().Be("https://videos.example/gravy");
    }

    [Fact]
    public async Task A_recipe_with_too_many_steps_isnt_copied()
    {
        var recipe = await AddRecipeAsync(_smiths, "Epic", steps: ShareLimits.MaxSteps + 1);

        await FluentActions.Awaiting(() => CopyToJonesAsync(ShareableKind.Recipe, recipe))
            .Should().ThrowAsync<ShareTooLargeException>();
    }

    [Fact]
    public async Task A_copy_that_fails_leaves_no_rows_and_no_files_behind()
    {
        var recipe = await AddRecipeAsync(_smiths, "Roux", withImage: true, extraImages: 1);
        _files.FailSavesAfter(1);

        await FluentActions.Awaiting(() => CopyToJonesAsync(ShareableKind.Recipe, recipe))
            .Should().ThrowAsync<IOException>();

        _files.Count(_jones).Should().Be(0);
        using var db = Seeding();
        (await db.Recipes.CountAsync(r => r.TenantId == _jones)).Should().Be(0);
    }

    // --- products ---

    [Fact]
    public async Task A_product_comes_across_with_its_details_but_none_of_the_sharers_stock_or_places()
    {
        var granola = await AddProductAsync(_smiths, "Granola", group: "Breakfast", barcodes: ["111", "222"], withNutrition: true, withImage: true,
            allergens: [AllergenType.Milk]);
        var taken = await AddProductAsync(_jones, "Oats", barcodes: ["222"]);

        var copy = await CopyToJonesAsync(ShareableKind.Product, granola);

        using var db = Seeding();
        var product = await db.Products.Include(p => p.Barcodes).Include(p => p.Nutrition).Include(p => p.Allergens)
            .Include(p => p.Images).Include(p => p.ProductGroup).SingleAsync(p => p.Id == copy!.ItemId);
        product.TenantId.Should().Be(_jones);
        product.LocationId.Should().Be(_pantries[_jones]);
        product.QuantityUnitIdStock.Should().Be(_units[(_jones, "Piece")]);
        product.ProductGroup!.Name.Should().Be("Breakfast");
        product.ProductGroup.TenantId.Should().Be(_jones);
        product.Barcodes.Select(b => b.Barcode).Should().BeEquivalentTo(["111"], "222 is already the Joneses' oats");
        product.Nutrition!.Calories.Should().Be(120m);
        product.Nutrition.TenantId.Should().Be(_jones);
        product.Allergens.Select(a => a.AllergenType).Should().BeEquivalentTo([AllergenType.Milk]);
        _files.Has(_jones, $"product/{product.Id}/{product.Images.Single().FileName}").Should().BeTrue();
        (await db.ProductBarcodes.SingleAsync(b => b.Barcode == "222" && b.TenantId == _jones)).ProductId.Should().Be(taken);
    }

    [Fact]
    public async Task A_product_whose_name_the_household_already_uses_says_where_it_came_from()
    {
        var granola = await AddProductAsync(_smiths, "Granola");
        await AddProductAsync(_jones, "granola");

        var copy = await CopyToJonesAsync(ShareableKind.Product, granola);

        copy!.Name.Should().Be("Granola (from The Smiths)");
    }

    [Fact]
    public async Task A_product_following_a_catalog_product_the_household_already_has_is_that_product()
    {
        var master = await AddMasterAsync("Rolled Oats", MasterProductReviewStatus.Approved, contributedBy: null);
        var smithsOats = await AddProductAsync(_smiths, "Oats", masterProductId: master);
        var jonesOats = await AddProductAsync(_jones, "Rolled oats", masterProductId: master);

        var copy = await CopyToJonesAsync(ShareableKind.Product, smithsOats);

        copy!.ItemId.Should().Be(jonesOats);
        copy.AlreadyHad.Should().BeTrue();
    }

    [Fact]
    public async Task A_catalog_contribution_still_in_review_stays_the_sharers()
    {
        var pending = await AddMasterAsync("Smith Family Granola", MasterProductReviewStatus.Pending, contributedBy: _smiths);
        var granola = await AddProductAsync(_smiths, "Granola", masterProductId: pending);

        var copy = await CopyToJonesAsync(ShareableKind.Product, granola);

        using var db = Seeding();
        var product = await db.Products.SingleAsync(p => p.Id == copy!.ItemId);
        product.MasterProductId.Should().BeNull();
        product.OverriddenFields.Should().Be("[]");
    }

    // --- contacts ---

    [Fact]
    public async Task A_contact_comes_across_with_their_card_and_the_same_address()
    {
        var (contact, address) = await AddContactAsync(_smiths, "Dana", "Doctor", ContactVisibilityLevel.TenantShared, createdBy: _alice);

        var copy = await CopyToJonesAsync(ShareableKind.Contact, contact);

        using var db = Seeding();
        var copied = await db.Contacts.Include(c => c.EmailAddresses).Include(c => c.PhoneNumbers).Include(c => c.Addresses)
            .SingleAsync(c => c.Id == copy!.ItemId);
        copied.TenantId.Should().Be(_jones);
        copied.FirstName.Should().Be("Dana");
        copied.Notes.Should().BeNull("the sharer's notes stay theirs");
        copied.Visibility.Should().Be(ContactVisibilityLevel.TenantShared);
        copied.EmailAddresses.Single().Email.Should().Be("dana@example.com");
        copied.PhoneNumbers.Single().PhoneNumber.Should().Be("555-0100");
        copied.Addresses.Single().AddressId.Should().Be(address);
        copied.ProfileImageFileName.Should().NotBeNull();
        _files.Has(_jones, $"contact/{copied.Id}/{copied.ProfileImageFileName}").Should().BeTrue();

        var created = await db.Set<ContactAuditLog>().FirstAsync(a => a.ContactId == copied.Id && a.Action == ContactAuditAction.Created);
        created.UserId.Should().Be(_bob);
        created.TenantId.Should().Be(_jones);
    }

    [Fact]
    public async Task A_contact_the_sharer_cant_see_isnt_shared()
    {
        var (secret, _) = await AddContactAsync(_smiths, "Sam", "Secret", ContactVisibilityLevel.UserPrivate, createdBy: _carol);

        (await Copier.PreviewAsync(Shared(ShareableKind.Contact, secret))).Should().BeNull();
        (await CopyToJonesAsync(ShareableKind.Contact, secret)).Should().BeNull();
    }

    [Fact]
    public async Task A_contacts_preview_is_their_name_and_photo_only()
    {
        var (contact, _) = await AddContactAsync(_smiths, "Dana", "Doctor", ContactVisibilityLevel.TenantShared, createdBy: _alice);

        var preview = await Copier.PreviewAsync(Shared(ShareableKind.Contact, contact));
        var photo = await Copier.ReadPreviewImageAsync(Shared(ShareableKind.Contact, contact));

        preview!.Name.Should().Be("Dana Doctor");
        preview.SharedByHouseholdName.Should().Be("The Smiths");
        preview.HasImage.Should().BeTrue();
        photo!.ContentType.Should().Be("image/png");
        photo.Bytes.Should().Equal(Png);
    }

    // --- the sharer, the households and the request ---

    [Fact]
    public async Task Nothing_is_shared_once_the_sharer_has_left_the_household()
    {
        var roux = await AddRecipeAsync(_smiths, "Roux");
        using (var db = Seeding())
        {
            (await db.Users.SingleAsync(u => u.Id == _alice)).IsActive = false;
            await db.SaveChangesAsync();
        }

        (await Copier.PreviewAsync(Shared(ShareableKind.Recipe, roux))).Should().BeNull();
        (await CopyToJonesAsync(ShareableKind.Recipe, roux)).Should().BeNull();
    }

    [Fact]
    public async Task The_calling_requests_household_and_member_are_left_as_they_were()
    {
        var roux = await AddRecipeAsync(_smiths, "Roux");

        await Copier.PreviewAsync(Shared(ShareableKind.Recipe, roux));
        await CopyToJonesAsync(ShareableKind.Recipe, roux);

        _http.HttpContext!.Items["TenantId"].Should().Be(_jones);
        _http.HttpContext.Items["UserId"].Should().Be(_bob);
    }

    [Fact]
    public async Task A_household_name_that_reads_like_a_link_or_message_isnt_repeated()
    {
        using (var db = Seeding())
        {
            (await db.Tenants.SingleAsync(t => t.Id == _smiths)).Name = "Famick Support: reset at http://evil.example";
            await db.SaveChangesAsync();
        }
        var roux = await AddRecipeAsync(_smiths, "Roux");

        var preview = await Copier.PreviewAsync(Shared(ShareableKind.Recipe, roux));

        preview!.SharedByHouseholdName.Should().Be(ShareText.UnnamedHousehold);
    }

    [Fact]
    public void Hidden_direction_characters_are_taken_out_of_copied_text()
    {
        ShareText.Clean("Smith‮evil⁦ family", 100).Should().Be("Smithevil family");
        ShareText.Clean("👨‍👩‍👧 night", 100).Should().Be("👨‍👩‍👧 night", "the joiner holds the emoji together");
    }

    [Fact]
    public async Task A_server_with_one_household_shares_nothing()
    {
        using var single = Services(new MultiTenancyOptions { IsMultiTenantEnabled = false, FixedTenantId = _smiths });
        var copier = single.CreateScope().ServiceProvider.GetRequiredService<ISharedItemCopier>();

        await FluentActions.Awaiting(() => copier.CopyAsync(new ShareCopyRequest(Shared(ShareableKind.Recipe, Guid.NewGuid()), _jones, _bob)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Origin_keys_are_stable_for_one_household_and_differ_between_households()
    {
        var item = Guid.NewGuid();
        var forJones = new ShareOrigin(_smiths, _jones, "x").KeyFor(ShareableKind.Recipe, item);

        new ShareOrigin(_smiths, _jones, "y").KeyFor(ShareableKind.Recipe, item).Should().Be(forJones);
        new ShareOrigin(_smiths, Guid.NewGuid(), "x").KeyFor(ShareableKind.Recipe, item).Should().NotBe(forJones);
        forJones.Length.Should().BeLessThanOrEqualTo(64);
    }

    // --- harness ---

    private ServiceProvider Services(MultiTenancyOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMultiTenancyOptions>(options);
        services.AddSingleton<IHttpContextAccessor>(_http);
        services.AddScoped<ITenantProvider>(sp => new RequestTenantProvider(sp.GetRequiredService<IHttpContextAccessor>()));
        services.AddScoped(sp => new HomeManagementDbContext(Options(), sp.GetRequiredService<ITenantProvider>(), options));
        services.AddScoped(sp => _files.For(sp.GetRequiredService<ITenantProvider>()));
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
        services.AddScoped<ContactShareHandler>();
        services.AddScoped<IShareableItemHandler>(sp => sp.GetRequiredService<RecipeShareHandler>());
        services.AddScoped<IShareableItemHandler>(sp => sp.GetRequiredService<ProductShareHandler>());
        services.AddScoped<IShareableItemHandler>(sp => sp.GetRequiredService<MealShareHandler>());
        services.AddScoped<IShareableItemHandler>(sp => sp.GetRequiredService<ContactShareHandler>());
        services.AddScoped<ISharedItemCopier, SharedItemCopier>();
        return services.BuildServiceProvider();
    }

    private DbContextOptions<HomeManagementDbContext> Options() => new DbContextOptionsBuilder<HomeManagementDbContext>()
        .UseInMemoryDatabase(_database, _root)
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    /// <summary>A context that sees every household, for seeding and checking.</summary>
    private HomeManagementDbContext Seeding() => new(Options());

    private void AddUnit(HomeManagementDbContext db, Guid tenant, string name, string plural, StandardUnit? standard)
    {
        var unit = new QuantityUnit { Id = Guid.NewGuid(), TenantId = tenant, Name = name, NamePlural = plural, StandardUnit = standard, IsActive = true };
        db.QuantityUnits.Add(unit);
        _units[(tenant, name)] = unit.Id;
    }

    private async Task<Guid> AddProductAsync(Guid tenant, string name, Guid? masterProductId = null, string? group = null,
        string[]? barcodes = null, bool withNutrition = false, bool withImage = false, AllergenType[]? allergens = null)
    {
        using var db = Seeding();
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenant, Name = name, IsActive = true, MasterProductId = masterProductId,
            OverriddenFields = masterProductId != null ? "[\"Name\"]" : "[]",
            LocationId = _pantries[tenant], QuantityUnitIdPurchase = _units[(tenant, "Piece")], QuantityUnitIdStock = _units[(tenant, "Piece")]
        };
        if (group != null)
            product.ProductGroup = new ProductGroup { Id = Guid.NewGuid(), TenantId = tenant, Name = group };
        foreach (var code in barcodes ?? [])
            product.Barcodes.Add(new ProductBarcode { Id = Guid.NewGuid(), TenantId = tenant, Barcode = code });
        if (withNutrition)
            product.Nutrition = new ProductNutrition { Id = Guid.NewGuid(), TenantId = tenant, DataSource = "label", Calories = 120m };
        foreach (var allergen in allergens ?? [])
            product.Allergens.Add(new ProductAllergen { Id = Guid.NewGuid(), AllergenType = allergen });
        if (withImage)
            product.Images.Add(new ProductImage
            {
                Id = Guid.NewGuid(), TenantId = tenant, FileName = _files.Put(tenant, $"product/{product.Id}", Png),
                OriginalFileName = "granola.png", ContentType = "image/png", IsPrimary = true
            });
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private async Task<Guid> AddMasterAsync(string name, MasterProductReviewStatus status, Guid? contributedBy)
    {
        using var db = Seeding();
        var master = new MasterProduct
        {
            Id = Guid.NewGuid(), Name = name, Category = "Pantry", ReviewStatus = status, ContributedByTenantId = contributedBy
        };
        db.MasterProducts.Add(master);
        await db.SaveChangesAsync();
        return master.Id;
    }

    private async Task<Guid> AddRecipeAsync(Guid tenant, string name, bool withImage = false, int extraImages = 0,
        (Guid? Product, decimal Amount, string Text)[]? lines = null, Guid[]? nests = null, int steps = 1,
        string? source = null, string? videoUrl = null)
    {
        using var db = Seeding();
        var recipe = new Recipe { Id = Guid.NewGuid(), TenantId = tenant, Name = name, Servings = 2, Source = source };
        for (var s = 0; s < steps; s++)
            recipe.Steps.Add(new RecipeStep
            {
                Id = Guid.NewGuid(), TenantId = tenant, RecipeId = recipe.Id, StepOrder = s + 1, Instructions = $"Step {s + 1}",
                VideoUrl = videoUrl
            });

        var first = recipe.Steps.First();
        var order = 0;
        foreach (var (product, amount, text) in lines ?? [])
            first.Ingredients.Add(new RecipePosition
            {
                Id = Guid.NewGuid(), TenantId = tenant, RecipeStepId = first.Id, ProductId = product, Amount = amount,
                QuantityUnitId = product != null ? _units[(tenant, "Cup")] : null, OriginalText = text, SortOrder = order++
            });

        for (var i = 0; i < (withImage ? 1 + extraImages : 0); i++)
            recipe.Images.Add(new RecipeImage
            {
                Id = Guid.NewGuid(), TenantId = tenant, RecipeId = recipe.Id, FileName = _files.Put(tenant, $"recipe/{recipe.Id}", Png),
                OriginalFileName = "roux.png", ContentType = "image/png", SortOrder = i, IsPrimary = i == 0
            });

        foreach (var nested in nests ?? [])
            recipe.NestedRecipes.Add(new RecipeNesting { Id = Guid.NewGuid(), TenantId = tenant, RecipeId = recipe.Id, IncludesRecipeId = nested });

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe.Id;
    }

    private async Task<Guid> AddMealAsync(Guid tenant, string name, Guid[] recipes, string? freetext = null)
    {
        using var db = Seeding();
        var meal = new Meal { Id = Guid.NewGuid(), TenantId = tenant, Name = name };
        foreach (var recipe in recipes)
            meal.Items.Add(new MealItem { Id = Guid.NewGuid(), MealId = meal.Id, ItemType = MealItemType.Recipe, RecipeId = recipe, SortOrder = meal.Items.Count });
        if (freetext != null)
            meal.Items.Add(new MealItem { Id = Guid.NewGuid(), MealId = meal.Id, ItemType = MealItemType.Freetext, FreetextDescription = freetext, SortOrder = meal.Items.Count });
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        return meal.Id;
    }

    private async Task<(Guid Contact, Guid Address)> AddContactAsync(
        Guid tenant, string first, string last, ContactVisibilityLevel visibility, Guid createdBy)
    {
        using var db = Seeding();
        var address = new Address { Id = Guid.NewGuid(), AddressLine1 = "1 Main St", City = "Springfield" };
        var contact = new Contact
        {
            Id = Guid.NewGuid(), TenantId = tenant, FirstName = first, LastName = last, Visibility = visibility,
            CreatedByUserId = createdBy, IsActive = true, Notes = "Owes us a casserole dish"
        };
        contact.ProfileImageFileName = _files.Put(tenant, $"contact/{contact.Id}", Png);
        contact.EmailAddresses.Add(new ContactEmailAddress { Id = Guid.NewGuid(), TenantId = tenant, ContactId = contact.Id, Email = "dana@example.com" });
        contact.PhoneNumbers.Add(new ContactPhoneNumber { Id = Guid.NewGuid(), TenantId = tenant, ContactId = contact.Id, PhoneNumber = "555-0100" });
        contact.Addresses.Add(new ContactAddress { Id = Guid.NewGuid(), TenantId = tenant, ContactId = contact.Id, AddressId = address.Id });
        db.Addresses.Add(address);
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return (contact.Id, address.Id);
    }

    /// <summary>
    /// Resolves the household as the cloud's provider does: its own setting first, else the request's
    /// items, which setting it also writes. A copy that ran in the request's context would change them.
    /// </summary>
    private sealed class RequestTenantProvider(IHttpContextAccessor http) : ITenantProvider
    {
        private Guid? _tenantId;
        private Guid? _userId;

        public Guid? TenantId => _tenantId ?? http.HttpContext?.Items["TenantId"] as Guid?;
        public Guid? UserId => _userId ?? http.HttpContext?.Items["UserId"] as Guid?;

        public void SetTenantId(Guid tenantId)
        {
            _tenantId = tenantId;
            if (http.HttpContext is { } context)
                context.Items["TenantId"] = tenantId;
        }

        public void SetUserId(Guid userId)
        {
            _userId = userId;
            if (http.HttpContext is { } context)
                context.Items["UserId"] = userId;
        }

        public void ClearTenantId() => _tenantId = null;
        public void ClearUserId() => _userId = null;
    }

    /// <summary>
    /// Files by household, as cloud storage keeps them: each scope's storage reads and writes under the
    /// household its provider holds when it is called, and has none without one.
    /// </summary>
    private sealed class FileStore
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();
        private int _savesLeft = int.MaxValue;

        public void FailSavesAfter(int saves) => _savesLeft = saves;

        public string Put(Guid tenant, string folder, byte[] bytes)
        {
            var name = $"{Guid.NewGuid():N}.png";
            _files[$"{tenant:N}/{folder}/{name}"] = bytes;
            return name;
        }

        public bool Has(Guid tenant, string path) => _files.ContainsKey($"{tenant:N}/{path}");

        public int Count(Guid tenant) => _files.Keys.Count(k => k.StartsWith($"{tenant:N}/"));

        public IFileStorageService For(ITenantProvider tenantProvider)
        {
            string Folder(string kind, params Guid[] ids) =>
                $"{tenantProvider.TenantId?.ToString("N") ?? throw new InvalidOperationException("No tenant context available")}/{kind}/{string.Join('/', ids)}";

            string Save(string folder, Stream stream, string fileName)
            {
                if (_savesLeft-- <= 0)
                    throw new IOException("Storage is unavailable");
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
                _files[$"{folder}/{name}"] = buffer.ToArray();
                return name;
            }

            Stream? Open(string path) => _files.TryGetValue(path, out var bytes) ? new MemoryStream(bytes) : null;

            var storage = new Mock<IFileStorageService>();
            storage.Setup(s => s.SaveRecipeImageAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, Stream s, string f, CancellationToken _) => Save(Folder("recipe", id), s, f));
            storage.Setup(s => s.GetRecipeImageStreamAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, string f, CancellationToken _) => Open($"{Folder("recipe", id)}/{f}"));
            storage.Setup(s => s.DeleteRecipeImageAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, string f, CancellationToken _) => { _files.TryRemove($"{Folder("recipe", id)}/{f}", out var _); return Task.CompletedTask; });
            storage.Setup(s => s.SaveRecipeStepImageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, Guid step, Stream s, string f, CancellationToken _) => Save(Folder("recipe-step", id, step), s, f));
            storage.Setup(s => s.SaveProductImageAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, Stream s, string f, CancellationToken _) => Save(Folder("product", id), s, f));
            storage.Setup(s => s.GetProductImageStreamAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, string f, CancellationToken _) => Open($"{Folder("product", id)}/{f}"));
            storage.Setup(s => s.DeleteProductImageAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, string f, CancellationToken _) => { _files.TryRemove($"{Folder("product", id)}/{f}", out var _); return Task.CompletedTask; });
            storage.Setup(s => s.SaveContactProfileImageAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, Stream s, string f, CancellationToken _) => Save(Folder("contact", id), s, f));
            storage.Setup(s => s.GetContactProfileImageStreamAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, string f, CancellationToken _) => Open($"{Folder("contact", id)}/{f}"));
            storage.Setup(s => s.DeleteContactProfileImageAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, string f, CancellationToken _) => { _files.TryRemove($"{Folder("contact", id)}/{f}", out var _); return Task.CompletedTask; });
            return storage.Object;
        }
    }
}
