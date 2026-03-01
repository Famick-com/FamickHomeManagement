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

public class AllergenWarningServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly AllergenWarningService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public AllergenWarningServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        _context = new HomeManagementDbContext(options, tenantProvider.Object);

        var logger = new Mock<ILogger<AllergenWarningService>>();

        _service = new AllergenWarningService(_context, logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<(Contact Household, Contact Member, Product Product, Meal Meal)> SeedHouseholdWithAllergenScenario()
    {
        var householdId = Guid.NewGuid();
        var household = new Contact
        {
            Id = householdId,
            TenantId = _tenantId,
            FirstName = "Smith",
            LastName = "Family",
            IsTenantHousehold = true,
            Allergens = new List<ContactAllergen>(),
            DietaryPreferences = new List<ContactDietaryPreference>()
        };

        var memberId = Guid.NewGuid();
        var member = new Contact
        {
            Id = memberId,
            TenantId = _tenantId,
            FirstName = "Alice",
            LastName = "Smith",
            ParentContactId = householdId,
            Allergens = new List<ContactAllergen>
            {
                new() { Id = Guid.NewGuid(), ContactId = memberId, AllergenType = AllergenType.Peanuts, Severity = AllergenSeverity.Allergy }
            },
            DietaryPreferences = new List<ContactDietaryPreference>()
        };

        var productId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            TenantId = _tenantId,
            Name = "Peanut Butter",
            Allergens = new List<ProductAllergen>
            {
                new() { Id = Guid.NewGuid(), ProductId = productId, AllergenType = AllergenType.Peanuts }
            },
            DietaryConflicts = new List<ProductDietaryConflict>()
        };

        var mealId = Guid.NewGuid();
        var meal = new Meal
        {
            Id = mealId,
            TenantId = _tenantId,
            Name = "PB&J Sandwich",
            Items = new List<MealItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    MealId = mealId,
                    ItemType = MealItemType.Product,
                    ProductId = productId,
                    SortOrder = 0
                }
            }
        };

        _context.Contacts.AddRange(household, member);
        _context.Products.Add(product);
        _context.Meals.Add(meal);
        await _context.SaveChangesAsync();

        return (household, member, product, meal);
    }

    [Fact]
    public async Task CheckMealAsync_AllergenMatch_ReturnsWarnings()
    {
        var (_, member, _, meal) = await SeedHouseholdWithAllergenScenario();

        var result = await _service.CheckMealAsync(meal.Id);

        result.Should().NotBeNull();
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().NotBeEmpty();
        result.Warnings.Should().Contain(w =>
            w.ContactId == member.Id &&
            w.AllergenType == AllergenType.Peanuts &&
            w.Severity == AllergenSeverity.Allergy);
    }

    [Fact]
    public async Task CheckMealAsync_NoAllergenMatch_ReturnsNoWarnings()
    {
        var householdId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = householdId,
            TenantId = _tenantId,
            FirstName = "Test",
            LastName = "Family",
            IsTenantHousehold = true,
            Allergens = new List<ContactAllergen>(),
            DietaryPreferences = new List<ContactDietaryPreference>()
        });

        var memberId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = memberId,
            TenantId = _tenantId,
            FirstName = "Bob",
            LastName = "Test",
            ParentContactId = householdId,
            Allergens = new List<ContactAllergen>
            {
                new() { Id = Guid.NewGuid(), ContactId = memberId, AllergenType = AllergenType.Shellfish, Severity = AllergenSeverity.Allergy }
            },
            DietaryPreferences = new List<ContactDietaryPreference>()
        });

        var productId = Guid.NewGuid();
        _context.Products.Add(new Product
        {
            Id = productId,
            TenantId = _tenantId,
            Name = "Bread",
            Allergens = new List<ProductAllergen>
            {
                new() { Id = Guid.NewGuid(), ProductId = productId, AllergenType = AllergenType.Wheat }
            },
            DietaryConflicts = new List<ProductDietaryConflict>()
        });

        var mealId = Guid.NewGuid();
        _context.Meals.Add(new Meal
        {
            Id = mealId,
            TenantId = _tenantId,
            Name = "Toast",
            Items = new List<MealItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    MealId = mealId,
                    ItemType = MealItemType.Product,
                    ProductId = productId,
                    SortOrder = 0
                }
            }
        });
        await _context.SaveChangesAsync();

        var result = await _service.CheckMealAsync(mealId);

        result.HasWarnings.Should().BeFalse();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckMealAsync_NoHousehold_ReturnsNoWarnings()
    {
        var mealId = Guid.NewGuid();
        _context.Meals.Add(new Meal
        {
            Id = mealId,
            TenantId = _tenantId,
            Name = "Lonely Meal",
            Items = new List<MealItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    MealId = mealId,
                    ItemType = MealItemType.Freetext,
                    FreetextDescription = "Something",
                    SortOrder = 0
                }
            }
        });
        await _context.SaveChangesAsync();

        var result = await _service.CheckMealAsync(mealId);

        result.HasWarnings.Should().BeFalse();
    }

    [Fact]
    public async Task CheckMealAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var act = () => _service.CheckMealAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CheckMealAsync_FreetextItemsIgnored_NoWarnings()
    {
        var householdId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = householdId,
            TenantId = _tenantId,
            FirstName = "Test",
            LastName = "Family",
            IsTenantHousehold = true,
            Allergens = new List<ContactAllergen>(),
            DietaryPreferences = new List<ContactDietaryPreference>()
        });

        var memberId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = memberId,
            TenantId = _tenantId,
            FirstName = "Alice",
            LastName = "Test",
            ParentContactId = householdId,
            Allergens = new List<ContactAllergen>
            {
                new() { Id = Guid.NewGuid(), ContactId = memberId, AllergenType = AllergenType.Peanuts, Severity = AllergenSeverity.Allergy }
            },
            DietaryPreferences = new List<ContactDietaryPreference>()
        });

        var mealId = Guid.NewGuid();
        _context.Meals.Add(new Meal
        {
            Id = mealId,
            TenantId = _tenantId,
            Name = "Freetext Only Meal",
            Items = new List<MealItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    MealId = mealId,
                    ItemType = MealItemType.Freetext,
                    FreetextDescription = "Peanut butter sandwich",
                    SortOrder = 0
                }
            }
        });
        await _context.SaveChangesAsync();

        var result = await _service.CheckMealAsync(mealId);

        result.HasWarnings.Should().BeFalse();
    }
}
