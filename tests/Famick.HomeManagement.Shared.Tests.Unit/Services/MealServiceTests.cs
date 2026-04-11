using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class MealServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly MealService _service;
    private readonly Mock<IAllergenWarningService> _allergenWarningService;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public MealServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        _context = new HomeManagementDbContext(options, tenantProvider.Object);

        _allergenWarningService = new Mock<IAllergenWarningService>();
        var logger = new Mock<ILogger<MealService>>();

        _service = new MealService(_context, _allergenWarningService.Object, logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesMeal()
    {
        var request = new CreateMealRequest
        {
            Name = "Pasta Night",
            Notes = "Family favorite",
            IsFavorite = true,
            Items = new List<CreateMealItemRequest>
            {
                new()
                {
                    ItemType = MealItemType.Freetext,
                    FreetextDescription = "Spaghetti with sauce",
                    SortOrder = 0
                }
            }
        };

        var result = await _service.CreateAsync(request);

        result.Should().NotBeNull();
        result.Name.Should().Be("Pasta Night");
        result.Notes.Should().Be("Family favorite");
        result.IsFavorite.Should().BeTrue();
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingMeal_ReturnsMeal()
    {
        var mealId = Guid.NewGuid();
        _context.Meals.Add(new Meal
        {
            Id = mealId,
            TenantId = _tenantId,
            Name = "Test Meal",
            IsFavorite = false,
            Items = new List<MealItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    MealId = mealId,
                    ItemType = MealItemType.Freetext,
                    FreetextDescription = "Side salad",
                    SortOrder = 0
                }
            }
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetByIdAsync(mealId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Meal");
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_NoFilter_ReturnsAllMeals()
    {
        _context.Meals.AddRange(
            new Meal { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Meal A" },
            new Meal { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Meal B" }
        );
        await _context.SaveChangesAsync();

        var result = await _service.ListAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_WithSearchTerm_FiltersResults()
    {
        _context.Meals.AddRange(
            new Meal { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Chicken Pasta" },
            new Meal { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Beef Stew" }
        );
        await _context.SaveChangesAsync();

        var filter = new MealFilterRequest { SearchTerm = "pasta" };

        var result = await _service.ListAsync(filter);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Chicken Pasta");
    }

    [Fact]
    public async Task ListAsync_FilterByFavorite_ReturnsOnlyFavorites()
    {
        _context.Meals.AddRange(
            new Meal { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Fav Meal", IsFavorite = true },
            new Meal { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Regular Meal", IsFavorite = false }
        );
        await _context.SaveChangesAsync();

        var filter = new MealFilterRequest { IsFavorite = true };

        var result = await _service.ListAsync(filter);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Fav Meal");
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesMeal()
    {
        var mealId = Guid.NewGuid();
        _context.Meals.Add(new Meal
        {
            Id = mealId,
            TenantId = _tenantId,
            Name = "Old Name",
            Items = new List<MealItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    MealId = mealId,
                    ItemType = MealItemType.Freetext,
                    FreetextDescription = "Old item",
                    SortOrder = 0
                }
            }
        });
        await _context.SaveChangesAsync();

        var request = new UpdateMealRequest
        {
            Name = "New Name",
            Notes = "Updated notes",
            IsFavorite = true,
            Items = new List<CreateMealItemRequest>
            {
                new()
                {
                    ItemType = MealItemType.Freetext,
                    FreetextDescription = "New item",
                    SortOrder = 0
                }
            }
        };

        var result = await _service.UpdateAsync(mealId, request);

        result.Name.Should().Be("New Name");
        result.Notes.Should().Be("Updated notes");
        result.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var request = new UpdateMealRequest
        {
            Name = "Test",
            Items = new List<CreateMealItemRequest>
            {
                new() { ItemType = MealItemType.Freetext, FreetextDescription = "Item", SortOrder = 0 }
            }
        };

        var act = () => _service.UpdateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_UnreferencedMeal_DeletesSuccessfully()
    {
        var mealId = Guid.NewGuid();
        _context.Meals.Add(new Meal
        {
            Id = mealId, TenantId = _tenantId, Name = "To Delete"
        });
        await _context.SaveChangesAsync();

        await _service.DeleteAsync(mealId);

        var deleted = await _context.Meals.FindAsync(mealId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var act = () => _service.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetNutritionAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var act = () => _service.GetNutritionAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CheckAllergensAsync_DelegatesToAllergenWarningService()
    {
        var mealId = Guid.NewGuid();
        var expected = new AllergenCheckResultDto
        {
            MealId = mealId,
            HasWarnings = false,
            Warnings = new List<AllergenWarningDto>()
        };
        _allergenWarningService
            .Setup(s => s.CheckMealAsync(mealId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.CheckAllergensAsync(mealId);

        result.Should().BeSameAs(expected);
        _allergenWarningService.Verify(s => s.CheckMealAsync(mealId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ReturnsStructuredSuggestions()
    {
        _context.Meals.Add(new Meal
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Favorite Meal", IsFavorite = true
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetSuggestionsAsync();

        result.Should().NotBeNull();
        result.Favorites.Should().NotBeEmpty();
        result.Favorites[0].Name.Should().Be("Favorite Meal");
    }
}
