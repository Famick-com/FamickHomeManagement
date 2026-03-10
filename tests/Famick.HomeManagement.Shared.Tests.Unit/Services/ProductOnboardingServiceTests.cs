using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.ProductOnboarding;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class ProductOnboardingServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly ProductOnboardingService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ProductOnboardingServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options, null);

        var logger = new Mock<ILogger<ProductOnboardingService>>();

        _service = new ProductOnboardingService(_context, logger.Object);
    }

    #region Helpers

    private MasterProduct CreateMasterProduct(
        string name,
        string category,
        string? lifestyleTags = null,
        string? dietaryConflictFlags = null,
        string? allergenFlags = null,
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
            OrganicScore = 3,
            ConvenienceScore = 3,
            HealthScore = 3,
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
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
