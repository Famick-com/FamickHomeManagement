using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.ProductOnboarding;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class ProductOnboardingServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly ProductOnboardingService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly IMemoryCache _cache;

    public ProductOnboardingServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options, null);
        _cache = new MemoryCache(new MemoryCacheOptions());

        var logger = new Mock<ILogger<ProductOnboardingService>>();

        _service = new ProductOnboardingService(_context, _cache, logger.Object);
    }

    #region Helpers

    private MasterProduct CreateMasterProduct(
        string name,
        string category,
        string? lifestyleTags = null,
        string? dietaryConflictFlags = null,
        string? allergenFlags = null,
        string? cookingStyleTags = null,
        string? defaultLocationHint = null,
        string? defaultQuantityUnitHint = null,
        int popularity = 3,
        bool isStaple = false,
        string? description = null,
        int defaultBestBeforeDays = 0,
        bool tracksBestBeforeDate = true)
    {
        return new MasterProduct
        {
            Id = Guid.NewGuid(),
            Name = name,
            Category = category,
            Description = description,
            LifestyleTags = lifestyleTags ?? "[]",
            DietaryConflictFlags = dietaryConflictFlags ?? "[]",
            AllergenFlags = allergenFlags ?? "[]",
            CookingStyleTags = cookingStyleTags ?? "[]",
            DefaultLocationHint = defaultLocationHint,
            DefaultQuantityUnitHint = defaultQuantityUnitHint,
            Popularity = popularity,
            IsStaple = isStaple,
            DefaultBestBeforeDays = defaultBestBeforeDays,
            TracksBestBeforeDate = tracksBestBeforeDate
        };
    }

    private static string TagsJson(params string[] tags) =>
        JsonSerializer.Serialize(tags);

    private async Task<(Location location, QuantityUnit quantityUnit)> SeedLocationAndQuantityUnit()
    {
        var location = new Location
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Pantry",
            IsActive = true
        };
        var quantityUnit = new QuantityUnit
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Piece",
            NamePlural = "Pieces",
            IsActive = true
        };
        _context.Locations.Add(location);
        _context.QuantityUnits.Add(quantityUnit);
        await _context.SaveChangesAsync();
        return (location, quantityUnit);
    }

    private async Task SeedMultipleLocations()
    {
        _context.Locations.AddRange(
            new Location { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Pantry", IsActive = true },
            new Location { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Refrigerator", IsActive = true },
            new Location { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Freezer", IsActive = true }
        );
        _context.QuantityUnits.AddRange(
            new QuantityUnit { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Piece", NamePlural = "Pieces", IsActive = true },
            new QuantityUnit { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Pound", NamePlural = "Pounds", IsActive = true },
            new QuantityUnit { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Gallon", NamePlural = "Gallons", IsActive = true }
        );
        await _context.SaveChangesAsync();
    }

    #endregion

    #region GetStateAsync

    [Fact]
    public async Task GetStateAsync_NoStateExists_ReturnsDefaults()
    {
        var result = await _service.GetStateAsync(_tenantId);

        result.Should().NotBeNull();
        result.HasCompletedOnboarding.Should().BeFalse();
        result.ProductsCreatedCount.Should().Be(0);
        result.CompletedAt.Should().BeNull();
        result.SavedAnswers.Should().BeNull();
    }

    [Fact]
    public async Task GetStateAsync_StateExists_ReturnsSavedState()
    {
        var answers = new ProductOnboardingAnswersDto
        {
            HasBaby = true,
            HasPets = false,
            DietaryPreferences = new List<string> { "Vegan" }
        };
        var completedAt = DateTime.UtcNow.AddHours(-1);

        _context.TenantProductOnboardingStates.Add(new TenantProductOnboardingState
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            HasCompletedOnboarding = true,
            CompletedAt = completedAt,
            ProductsCreatedCount = 42,
            QuestionnaireAnswersJson = JsonSerializer.Serialize(answers,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetStateAsync(_tenantId);

        result.HasCompletedOnboarding.Should().BeTrue();
        result.CompletedAt.Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));
        result.ProductsCreatedCount.Should().Be(42);
        result.SavedAnswers.Should().NotBeNull();
        result.SavedAnswers!.HasBaby.Should().BeTrue();
        result.SavedAnswers.HasPets.Should().BeFalse();
        result.SavedAnswers.DietaryPreferences.Should().Contain("Vegan");
    }

    [Fact]
    public async Task GetStateAsync_InvalidJson_ReturnsNullAnswers()
    {
        _context.TenantProductOnboardingStates.Add(new TenantProductOnboardingState
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            HasCompletedOnboarding = true,
            ProductsCreatedCount = 5,
            QuestionnaireAnswersJson = "{invalid json!!"
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetStateAsync(_tenantId);

        result.HasCompletedOnboarding.Should().BeTrue();
        result.SavedAnswers.Should().BeNull();
    }

    #endregion

    #region PreviewAsync - Lifestyle Filtering

    [Fact]
    public async Task PreviewAsync_BabyItemsIncluded_WhenHasBabyTrue()
    {
        var babyProduct = CreateMasterProduct("Baby Formula", "Baby",
            lifestyleTags: TagsJson("baby"));
        var genericProduct = CreateMasterProduct("Rice", "Grains");
        _context.MasterProducts.AddRange(babyProduct, genericProduct);
        await _context.SaveChangesAsync();

        var answers = new ProductOnboardingAnswersDto { HasBaby = true };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(2);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().Contain("Baby Formula")
            .And.Contain("Rice");
    }

    [Fact]
    public async Task PreviewAsync_BabyItemsExcluded_WhenHasBabyFalse()
    {
        var babyProduct = CreateMasterProduct("Baby Formula", "Baby",
            lifestyleTags: TagsJson("baby"));
        var genericProduct = CreateMasterProduct("Rice", "Grains");
        _context.MasterProducts.AddRange(babyProduct, genericProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto { HasBaby = false };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(1);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().NotContain("Baby Formula")
            .And.Contain("Rice");
    }

    [Fact]
    public async Task PreviewAsync_PetItemsIncluded_WhenHasPetsTrue()
    {
        var petProduct = CreateMasterProduct("Dog Food", "Pet Supplies",
            lifestyleTags: TagsJson("pet"));
        var genericProduct = CreateMasterProduct("Bread", "Bakery");
        _context.MasterProducts.AddRange(petProduct, genericProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto { HasPets = true };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(2);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().Contain("Dog Food");
    }

    [Fact]
    public async Task PreviewAsync_PetItemsExcluded_WhenHasPetsFalse()
    {
        var petProduct = CreateMasterProduct("Dog Food", "Pet Supplies",
            lifestyleTags: TagsJson("pet"));
        var genericProduct = CreateMasterProduct("Bread", "Bakery");
        _context.MasterProducts.AddRange(petProduct, genericProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto { HasPets = false };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(1);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().NotContain("Dog Food");
    }

    [Fact]
    public async Task PreviewAsync_HouseholdItemsIncluded_WhenTrackHouseholdSuppliesTrue()
    {
        var householdProduct = CreateMasterProduct("Paper Towels", "Household",
            lifestyleTags: TagsJson("household"));
        var genericProduct = CreateMasterProduct("Milk", "Dairy");
        _context.MasterProducts.AddRange(householdProduct, genericProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto { TrackHouseholdSupplies = true };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(2);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().Contain("Paper Towels");
    }

    [Fact]
    public async Task PreviewAsync_HouseholdItemsExcluded_WhenTrackHouseholdSuppliesFalse()
    {
        var householdProduct = CreateMasterProduct("Paper Towels", "Household",
            lifestyleTags: TagsJson("household"));
        var genericProduct = CreateMasterProduct("Milk", "Dairy");
        _context.MasterProducts.AddRange(householdProduct, genericProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto { TrackHouseholdSupplies = false };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(1);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().NotContain("Paper Towels");
    }

    [Fact]
    public async Task PreviewAsync_NoLifestyleTags_AlwaysIncluded()
    {
        var genericProduct = CreateMasterProduct("Rice", "Grains");
        _context.MasterProducts.Add(genericProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto
        {
            HasBaby = false,
            HasPets = false,
            TrackHouseholdSupplies = false,
            TrackPersonalCare = false,
            TrackPharmacy = false
        };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(1);
    }

    #endregion

    #region PreviewAsync - Dietary Filtering

    [Fact]
    public async Task PreviewAsync_VeganDiet_ExcludesMeatProducts()
    {
        var meatProduct = CreateMasterProduct("Ground Beef", "Meat",
            dietaryConflictFlags: TagsJson("Vegan", "Vegetarian"));
        var veganProduct = CreateMasterProduct("Tofu", "Protein");
        _context.MasterProducts.AddRange(meatProduct, veganProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto
        {
            DietaryPreferences = new List<string> { "Vegan" }
        };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(1);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().NotContain("Ground Beef")
            .And.Contain("Tofu");
    }

    [Fact]
    public async Task PreviewAsync_VegetarianDiet_ExcludesMeatProducts()
    {
        var meatProduct = CreateMasterProduct("Chicken Breast", "Meat",
            dietaryConflictFlags: TagsJson("Vegan", "Vegetarian"));
        var dairyProduct = CreateMasterProduct("Cheddar Cheese", "Dairy",
            dietaryConflictFlags: TagsJson("Vegan"));
        _context.MasterProducts.AddRange(meatProduct, dairyProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto
        {
            DietaryPreferences = new List<string> { "Vegetarian" }
        };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(1);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().NotContain("Chicken Breast")
            .And.Contain("Cheddar Cheese");
    }

    [Fact]
    public async Task PreviewAsync_NoDietaryPreferences_IncludesAll()
    {
        var meatProduct = CreateMasterProduct("Ground Beef", "Meat",
            dietaryConflictFlags: TagsJson("Vegan", "Vegetarian"));
        var veganProduct = CreateMasterProduct("Tofu", "Protein");
        _context.MasterProducts.AddRange(meatProduct, veganProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto();

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(2);
    }

    #endregion

    #region PreviewAsync - Allergen Filtering

    [Fact]
    public async Task PreviewAsync_MilkAllergen_ExcludesDairyProducts()
    {
        var dairyProduct = CreateMasterProduct("Whole Milk", "Dairy",
            allergenFlags: TagsJson("Milk"));
        var nonDairyProduct = CreateMasterProduct("Oat Milk", "Beverages");
        _context.MasterProducts.AddRange(dairyProduct, nonDairyProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto
        {
            Allergens = new List<string> { "Milk" }
        };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(1);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().NotContain("Whole Milk")
            .And.Contain("Oat Milk");
    }

    [Fact]
    public async Task PreviewAsync_MultipleAllergens_ExcludesAllMatching()
    {
        var dairyProduct = CreateMasterProduct("Cheese", "Dairy",
            allergenFlags: TagsJson("Milk"));
        var wheatProduct = CreateMasterProduct("Bread", "Bakery",
            allergenFlags: TagsJson("Wheat", "Gluten"));
        var safeProduct = CreateMasterProduct("Rice", "Grains");
        _context.MasterProducts.AddRange(dairyProduct, wheatProduct, safeProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto
        {
            Allergens = new List<string> { "Milk", "Wheat" }
        };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(1);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().Contain("Rice")
            .And.NotContain("Cheese")
            .And.NotContain("Bread");
    }

    [Fact]
    public async Task PreviewAsync_NoAllergens_IncludesAll()
    {
        var dairyProduct = CreateMasterProduct("Cheese", "Dairy",
            allergenFlags: TagsJson("Milk"));
        var safeProduct = CreateMasterProduct("Rice", "Grains");
        _context.MasterProducts.AddRange(dairyProduct, safeProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto();

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(2);
    }

    #endregion

    #region PreviewAsync - Cooking Style Filtering

    [Fact]
    public async Task PreviewAsync_SelectedCookingStyles_OnlyIncludesMatchingItems()
    {
        var bakingProduct = CreateMasterProduct("Flour", "Baking Supplies",
            cookingStyleTags: TagsJson("Baking"));
        var meatProduct = CreateMasterProduct("Ground Beef", "Meat",
            cookingStyleTags: TagsJson("MeatAndSeafood"));
        _context.MasterProducts.AddRange(bakingProduct, meatProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto
        {
            CookingStyles = new List<string> { "Baking" }
        };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(1);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().Contain("Flour")
            .And.NotContain("Ground Beef");
    }

    [Fact]
    public async Task PreviewAsync_ItemsWithNoCookingTags_AlwaysPassCookingFilter()
    {
        var noTagProduct = CreateMasterProduct("Salt", "Pantry Staples");
        var bakingProduct = CreateMasterProduct("Flour", "Baking Supplies",
            cookingStyleTags: TagsJson("Baking"));
        _context.MasterProducts.AddRange(noTagProduct, bakingProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto
        {
            CookingStyles = new List<string> { "Baking" }
        };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(2);
        result.Categories.SelectMany(c => c.Items).Select(i => i.Name)
            .Should().Contain("Salt")
            .And.Contain("Flour");
    }

    [Fact]
    public async Task PreviewAsync_NoCookingStylesSelected_IncludesAll()
    {
        var bakingProduct = CreateMasterProduct("Flour", "Baking Supplies",
            cookingStyleTags: TagsJson("Baking"));
        var meatProduct = CreateMasterProduct("Ground Beef", "Meat",
            cookingStyleTags: TagsJson("MeatAndSeafood"));
        _context.MasterProducts.AddRange(bakingProduct, meatProduct);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto();

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(2);
    }

    #endregion

    #region PreviewAsync - Combined Filtering

    [Fact]
    public async Task PreviewAsync_CombinedFilters_ApplyAllTogether()
    {
        // Baby product - should be excluded (HasBaby = false)
        var babyProduct = CreateMasterProduct("Baby Wipes", "Baby",
            lifestyleTags: TagsJson("baby"));
        // Meat product with dairy allergen - should be excluded (Vegan diet + Milk allergen)
        var meatProduct = CreateMasterProduct("Chicken Parmesan", "Meat",
            dietaryConflictFlags: TagsJson("Vegan", "Vegetarian"),
            allergenFlags: TagsJson("Milk"));
        // Baking product - should be included (matches cooking style)
        var bakingProduct = CreateMasterProduct("Vanilla Extract", "Baking",
            cookingStyleTags: TagsJson("Baking"));
        // Generic staple - should be included (no tags)
        var staple = CreateMasterProduct("Salt", "Pantry");

        _context.MasterProducts.AddRange(babyProduct, meatProduct, bakingProduct, staple);
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto
        {
            HasBaby = false,
            DietaryPreferences = new List<string> { "Vegan" },
            Allergens = new List<string> { "Milk" },
            CookingStyles = new List<string> { "Baking" }
        };

        var result = await _service.PreviewAsync(answers);

        result.FilteredCount.Should().Be(2);
        var names = result.Categories.SelectMany(c => c.Items).Select(i => i.Name).ToList();
        names.Should().Contain("Vanilla Extract");
        names.Should().Contain("Salt");
        names.Should().NotContain("Baby Wipes");
        names.Should().NotContain("Chicken Parmesan");
    }

    [Fact]
    public async Task PreviewAsync_ReturnsCorrectTotalMasterProductsCount()
    {
        _context.MasterProducts.AddRange(
            CreateMasterProduct("Rice", "Grains"),
            CreateMasterProduct("Flour", "Baking",
                cookingStyleTags: TagsJson("Baking")),
            CreateMasterProduct("Baby Cereal", "Baby",
                lifestyleTags: TagsJson("baby"))
        );
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto();

        var result = await _service.PreviewAsync(answers);

        result.TotalMasterProducts.Should().Be(3);
    }

    [Fact]
    public async Task PreviewAsync_CategoriesGroupedCorrectly()
    {
        _context.MasterProducts.AddRange(
            CreateMasterProduct("Rice", "Grains", popularity: 5),
            CreateMasterProduct("Quinoa", "Grains", popularity: 3),
            CreateMasterProduct("Milk", "Dairy", popularity: 5)
        );
        await _context.SaveChangesAsync();
        _cache.Remove("master-products-all");

        var answers = new ProductOnboardingAnswersDto();

        var result = await _service.PreviewAsync(answers);

        result.Categories.Should().HaveCount(2);
        var grainsCategory = result.Categories.First(c => c.Category == "Grains");
        grainsCategory.ItemCount.Should().Be(2);
        // Should be ordered by popularity desc then name
        grainsCategory.Items[0].Name.Should().Be("Rice");
        grainsCategory.Items[1].Name.Should().Be("Quinoa");
    }

    #endregion

    #region CompleteAsync

    [Fact]
    public async Task CompleteAsync_CreatesProductsFromSelectedMasterProducts()
    {
        await SeedLocationAndQuantityUnit();
        var mp = CreateMasterProduct("Flour", "Baking", description: "All-purpose flour",
            defaultBestBeforeDays: 365);
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        var result = await _service.CompleteAsync(_tenantId, request);

        result.ProductsCreated.Should().Be(1);
        result.ProductsSkipped.Should().Be(0);

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Name == "Flour");
        product.Should().NotBeNull();
        product!.MasterProductId.Should().Be(mp.Id);
        product.Description.Should().Be("All-purpose flour");
        product.DefaultBestBeforeDays.Should().Be(365);
        product.OverriddenFields.Should().Be("[]");
        product.TenantId.Should().Be(_tenantId);
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_CopiesShareableFieldsFromMaster()
    {
        await SeedLocationAndQuantityUnit();
        var mp = CreateMasterProduct("Milk", "Dairy",
            description: "Whole milk",
            defaultBestBeforeDays: 14,
            tracksBestBeforeDate: true);
        mp.ServingSize = 240m;
        mp.ServingUnit = "mL";
        mp.ServingsPerContainer = 8m;
        mp.DataSourceAttribution = "USDA FoodData Central";
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        await _service.CompleteAsync(_tenantId, request);

        var product = await _context.Products.FirstAsync(p => p.Name == "Milk");
        product.DefaultBestBeforeDays.Should().Be(14);
        product.TracksBestBeforeDate.Should().BeTrue();
        product.ServingSize.Should().Be(240m);
        product.ServingUnit.Should().Be("mL");
        product.ServingsPerContainer.Should().Be(8m);
        product.DataSourceAttribution.Should().Be("USDA FoodData Central");
    }

    [Fact]
    public async Task CompleteAsync_SkipsDuplicateNames_CaseInsensitive()
    {
        var (location, quantityUnit) = await SeedLocationAndQuantityUnit();
        // Existing product with same name (different case)
        _context.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "flour",
            LocationId = location.Id,
            QuantityUnitIdPurchase = quantityUnit.Id,
            QuantityUnitIdStock = quantityUnit.Id,
            IsActive = true
        });
        var mp = CreateMasterProduct("Flour", "Baking");
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        var result = await _service.CompleteAsync(_tenantId, request);

        result.ProductsCreated.Should().Be(0);
        result.ProductsSkipped.Should().Be(1);
    }

    [Fact]
    public async Task CompleteAsync_CreatesProductGroupsForNewCategories()
    {
        await SeedLocationAndQuantityUnit();
        var mp = CreateMasterProduct("Flour", "Baking Supplies");
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        await _service.CompleteAsync(_tenantId, request);

        var group = await _context.ProductGroups.FirstOrDefaultAsync(g => g.Name == "Baking Supplies");
        group.Should().NotBeNull();
        group!.TenantId.Should().Be(_tenantId);

        var product = await _context.Products.FirstAsync(p => p.Name == "Flour");
        product.ProductGroupId.Should().Be(group.Id);
    }

    [Fact]
    public async Task CompleteAsync_ReusesExistingProductGroup()
    {
        await SeedLocationAndQuantityUnit();
        var existingGroup = new ProductGroup
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Dairy"
        };
        _context.ProductGroups.Add(existingGroup);
        var mp = CreateMasterProduct("Milk", "Dairy");
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        await _service.CompleteAsync(_tenantId, request);

        var groups = await _context.ProductGroups.Where(g => g.Name == "Dairy").ToListAsync();
        groups.Should().HaveCount(1);

        var product = await _context.Products.FirstAsync(p => p.Name == "Milk");
        product.ProductGroupId.Should().Be(existingGroup.Id);
    }

    [Fact]
    public async Task CompleteAsync_ResolvesLocationHint()
    {
        await SeedMultipleLocations();
        var mp = CreateMasterProduct("Ice Cream", "Frozen",
            defaultLocationHint: "Freezer");
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        await _service.CompleteAsync(_tenantId, request);

        var freezer = await _context.Locations.FirstAsync(l => l.Name == "Freezer");
        var product = await _context.Products.FirstAsync(p => p.Name == "Ice Cream");
        product.LocationId.Should().Be(freezer.Id);
    }

    [Fact]
    public async Task CompleteAsync_ResolvesQuantityUnitHint()
    {
        await SeedMultipleLocations();
        var mp = CreateMasterProduct("Milk", "Dairy",
            defaultQuantityUnitHint: "Gallon");
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        await _service.CompleteAsync(_tenantId, request);

        var gallon = await _context.QuantityUnits.FirstAsync(qu => qu.Name == "Gallon");
        var product = await _context.Products.FirstAsync(p => p.Name == "Milk");
        product.QuantityUnitIdPurchase.Should().Be(gallon.Id);
        product.QuantityUnitIdStock.Should().Be(gallon.Id);
    }

    [Fact]
    public async Task CompleteAsync_FallsBackToDefaultLocation_WhenHintNotFound()
    {
        await SeedLocationAndQuantityUnit();
        var mp = CreateMasterProduct("Widget", "Other",
            defaultLocationHint: "Garage");
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        await _service.CompleteAsync(_tenantId, request);

        var pantry = await _context.Locations.FirstAsync(l => l.Name == "Pantry");
        var product = await _context.Products.FirstAsync(p => p.Name == "Widget");
        product.LocationId.Should().Be(pantry.Id);
    }

    [Fact]
    public async Task CompleteAsync_UpdatesOnboardingState()
    {
        await SeedLocationAndQuantityUnit();
        var mp = CreateMasterProduct("Rice", "Grains");
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto { HasBaby = true }
        };

        await _service.CompleteAsync(_tenantId, request);

        var state = await _context.TenantProductOnboardingStates
            .FirstOrDefaultAsync(s => s.TenantId == _tenantId);
        state.Should().NotBeNull();
        state!.HasCompletedOnboarding.Should().BeTrue();
        state.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        state.ProductsCreatedCount.Should().Be(1);
        state.QuestionnaireAnswersJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteAsync_AccumulatesProductsCreatedCount()
    {
        await SeedLocationAndQuantityUnit();
        // Pre-existing state with previous count
        _context.TenantProductOnboardingStates.Add(new TenantProductOnboardingState
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            HasCompletedOnboarding = true,
            ProductsCreatedCount = 10
        });
        var mp = CreateMasterProduct("Flour", "Baking");
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        await _service.CompleteAsync(_tenantId, request);

        var state = await _context.TenantProductOnboardingStates
            .FirstAsync(s => s.TenantId == _tenantId);
        state.ProductsCreatedCount.Should().Be(11);
    }

    [Fact]
    public async Task CompleteAsync_ThrowsWhenNoLocationsExist()
    {
        // Only add quantity units, no locations
        _context.QuantityUnits.Add(new QuantityUnit
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Piece",
            NamePlural = "Pieces",
            IsActive = true
        });
        var mp = CreateMasterProduct("Rice", "Grains");
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        var act = () => _service.CompleteAsync(_tenantId, request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no locations or quantity units*");
    }

    [Fact]
    public async Task CompleteAsync_ThrowsWhenNoQuantityUnitsExist()
    {
        // Only add locations, no quantity units
        _context.Locations.Add(new Location
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Pantry",
            IsActive = true
        });
        var mp = CreateMasterProduct("Rice", "Grains");
        _context.MasterProducts.Add(mp);
        await _context.SaveChangesAsync();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { mp.Id },
            Answers = new ProductOnboardingAnswersDto()
        };

        var act = () => _service.CompleteAsync(_tenantId, request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no locations or quantity units*");
    }

    [Fact]
    public async Task CompleteAsync_NoMatchingMasterProducts_ReturnsZero()
    {
        await SeedLocationAndQuantityUnit();

        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { Guid.NewGuid() },
            Answers = new ProductOnboardingAnswersDto()
        };

        var result = await _service.CompleteAsync(_tenantId, request);

        result.ProductsCreated.Should().Be(0);
        result.ProductsSkipped.Should().Be(0);
    }

    #endregion

    #region ResetAsync

    [Fact]
    public async Task ResetAsync_ExistingState_ResetsCompletionFlags()
    {
        _context.TenantProductOnboardingStates.Add(new TenantProductOnboardingState
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            HasCompletedOnboarding = true,
            CompletedAt = DateTime.UtcNow,
            ProductsCreatedCount = 25
        });
        await _context.SaveChangesAsync();

        await _service.ResetAsync(_tenantId);

        var state = await _context.TenantProductOnboardingStates
            .FirstAsync(s => s.TenantId == _tenantId);
        state.HasCompletedOnboarding.Should().BeFalse();
        state.CompletedAt.Should().BeNull();
        // ProductsCreatedCount is preserved (not reset)
        state.ProductsCreatedCount.Should().Be(25);
    }

    [Fact]
    public async Task ResetAsync_NoExistingState_DoesNotThrow()
    {
        var act = () => _service.ResetAsync(_tenantId);

        await act.Should().NotThrowAsync();
    }

    #endregion

    public void Dispose()
    {
        _cache.Dispose();
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
