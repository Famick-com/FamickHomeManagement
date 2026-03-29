using AutoMapper;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class MealPlanServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly MealPlanService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000099");

    public MealPlanServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        _context = new HomeManagementDbContext(options, tenantProvider.Object);

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MealPlannerMappingProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var mapper = config.CreateMapper();

        var allergenService = new Mock<IAllergenWarningService>();
        allergenService
            .Setup(s => s.CheckMealPlanAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MealPlanAllergenWarningsDto
            {
                MealPlanId = Guid.Empty,
                HasWarnings = false,
                Warnings = new List<AllergenWarningDto>()
            });

        var logger = new Mock<ILogger<MealPlanService>>();

        _service = new MealPlanService(_context, mapper, allergenService.Object, logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<(MealPlan Plan, MealType MealType, Meal Meal)> SeedPlanWithMealTypeAndMeal()
    {
        var mealType = new MealType
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Breakfast", SortOrder = 0
        };
        var meal = new Meal
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Pancakes",
            Items = new List<MealItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ItemType = MealItemType.Freetext,
                    FreetextDescription = "Pancake batter",
                    SortOrder = 0
                }
            }
        };
        var plan = new MealPlan
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(int)DateTime.UtcNow.DayOfWeek + 1),
            Version = 1
        };

        _context.MealTypes.Add(mealType);
        _context.Meals.Add(meal);
        _context.MealPlans.Add(plan);
        await _context.SaveChangesAsync();

        return (plan, mealType, meal);
    }

    [Fact]
    public async Task GetOrCreateForWeekAsync_NoPlanExists_CreatesNewPlan()
    {
        var weekStart = new DateOnly(2026, 3, 2); // A Monday

        var result = await _service.GetOrCreateForWeekAsync(weekStart);

        result.Should().NotBeNull();
        result.WeekStartDate.Should().Be(weekStart);
        result.Entries.Should().BeEmpty();

        var saved = await _context.MealPlans.FirstOrDefaultAsync(p => p.WeekStartDate == weekStart);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrCreateForWeekAsync_PlanExists_ReturnsExisting()
    {
        var weekStart = new DateOnly(2026, 3, 2);
        var planId = Guid.NewGuid();
        _context.MealPlans.Add(new MealPlan
        {
            Id = planId, TenantId = _tenantId, WeekStartDate = weekStart
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetOrCreateForWeekAsync(weekStart);

        result.Should().NotBeNull();
        result.Id.Should().Be(planId);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingPlan_ReturnsPlan()
    {
        var planId = Guid.NewGuid();
        _context.MealPlans.Add(new MealPlan
        {
            Id = planId, TenantId = _tenantId, WeekStartDate = new DateOnly(2026, 3, 2)
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetByIdAsync(planId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(planId);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsAllPlans()
    {
        _context.MealPlans.AddRange(
            new MealPlan { Id = Guid.NewGuid(), TenantId = _tenantId, WeekStartDate = new DateOnly(2026, 3, 2) },
            new MealPlan { Id = Guid.NewGuid(), TenantId = _tenantId, WeekStartDate = new DateOnly(2026, 3, 9) }
        );
        await _context.SaveChangesAsync();

        var result = await _service.ListAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_ExistingPlan_DeletesSuccessfully()
    {
        var planId = Guid.NewGuid();
        _context.MealPlans.Add(new MealPlan
        {
            Id = planId, TenantId = _tenantId, WeekStartDate = new DateOnly(2026, 3, 2)
        });
        await _context.SaveChangesAsync();

        await _service.DeleteAsync(planId);

        var deleted = await _context.MealPlans.FindAsync(planId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var act = () => _service.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AddEntryAsync_ValidMealEntry_CreatesEntry()
    {
        var (plan, mealType, meal) = await SeedPlanWithMealTypeAndMeal();

        var request = new CreateMealPlanEntryRequest
        {
            MealId = meal.Id,
            MealTypeId = mealType.Id,
            DayOfWeek = 0,
            SortOrder = 0
        };

        var result = await _service.AddEntryAsync(plan.Id, request, plan.Version, _userId);

        result.Should().NotBeNull();
        result.MealId.Should().Be(meal.Id);
        result.MealTypeId.Should().Be(mealType.Id);
        result.DayOfWeek.Should().Be(0);
    }

    [Fact]
    public async Task AddEntryAsync_InlineNote_CreatesEntry()
    {
        var (plan, mealType, _) = await SeedPlanWithMealTypeAndMeal();

        var request = new CreateMealPlanEntryRequest
        {
            InlineNote = "Leftovers from yesterday",
            MealTypeId = mealType.Id,
            DayOfWeek = 1,
            SortOrder = 0
        };

        var result = await _service.AddEntryAsync(plan.Id, request, plan.Version, _userId);

        result.Should().NotBeNull();
        result.InlineNote.Should().Be("Leftovers from yesterday");
        result.MealId.Should().BeNull();
    }

    [Fact]
    public async Task AddEntryAsync_NonExistentPlan_ThrowsKeyNotFoundException()
    {
        var request = new CreateMealPlanEntryRequest
        {
            InlineNote = "Test",
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 0,
            SortOrder = 0
        };

        var act = () => _service.AddEntryAsync(Guid.NewGuid(), request, 0, _userId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AddEntryAsync_WrongVersion_ThrowsMealPlanConcurrencyException()
    {
        var (plan, mealType, meal) = await SeedPlanWithMealTypeAndMeal();

        var request = new CreateMealPlanEntryRequest
        {
            MealId = meal.Id,
            MealTypeId = mealType.Id,
            DayOfWeek = 0,
            SortOrder = 0
        };

        var act = () => _service.AddEntryAsync(plan.Id, request, 999, _userId);

        await act.Should().ThrowAsync<MealPlanConcurrencyException>();
    }

    [Fact]
    public async Task DeleteEntryAsync_ExistingEntry_DeletesSuccessfully()
    {
        var (plan, mealType, meal) = await SeedPlanWithMealTypeAndMeal();
        var entryId = Guid.NewGuid();
        _context.Set<MealPlanEntry>().Add(new MealPlanEntry
        {
            Id = entryId,
            MealPlanId = plan.Id,
            MealId = meal.Id,
            MealTypeId = mealType.Id,
            DayOfWeek = 0,
            SortOrder = 0
        });
        await _context.SaveChangesAsync();

        await _service.DeleteEntryAsync(plan.Id, entryId, plan.Version, _userId);

        var deleted = await _context.Set<MealPlanEntry>().FindAsync(entryId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task GetTodaysMealsAsync_NoPlan_ReturnsEmptyResult()
    {
        var result = await _service.GetTodaysMealsAsync();

        result.Should().NotBeNull();
        result.MealGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNutritionAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var act = () => _service.GetNutritionAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GenerateShoppingListAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var request = new GenerateShoppingListRequest { ShoppingListId = Guid.NewGuid() };

        var act = () => _service.GenerateShoppingListAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
