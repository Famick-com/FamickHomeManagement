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

public class DietaryProfileServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly DietaryProfileService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DietaryProfileServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        _context = new HomeManagementDbContext(options, tenantProvider.Object);

        var logger = new Mock<ILogger<DietaryProfileService>>();

        _service = new DietaryProfileService(_context, logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetAsync_ExistingContact_ReturnsProfile()
    {
        var contactId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = contactId,
            TenantId = _tenantId,
            FirstName = "John",
            LastName = "Doe",
            DietaryNotes = "Avoids spicy food",
            Allergens = new List<ContactAllergen>
            {
                new() { Id = Guid.NewGuid(), ContactId = contactId, AllergenType = AllergenType.Milk, Severity = AllergenSeverity.Allergy }
            },
            DietaryPreferences = new List<ContactDietaryPreference>
            {
                new() { Id = Guid.NewGuid(), ContactId = contactId, DietaryPreference = DietaryPreference.Vegetarian }
            }
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetAsync(contactId);

        result.Should().NotBeNull();
        result.ContactId.Should().Be(contactId);
        result.DietaryNotes.Should().Be("Avoids spicy food");
        result.Allergens.Should().HaveCount(1);
        result.Allergens[0].AllergenType.Should().Be(AllergenType.Milk);
        result.DietaryPreferences.Should().HaveCount(1);
        result.DietaryPreferences[0].DietaryPreference.Should().Be(DietaryPreference.Vegetarian);
    }

    [Fact]
    public async Task GetAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var act = () => _service.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesProfile()
    {
        var contactId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = contactId,
            TenantId = _tenantId,
            FirstName = "Jane",
            LastName = "Doe",
            Allergens = new List<ContactAllergen>
            {
                new() { Id = Guid.NewGuid(), ContactId = contactId, AllergenType = AllergenType.Milk, Severity = AllergenSeverity.Sensitivity }
            },
            DietaryPreferences = new List<ContactDietaryPreference>()
        });
        await _context.SaveChangesAsync();

        var request = new UpdateDietaryProfileRequest
        {
            DietaryNotes = "Updated notes",
            Allergens = new List<UpdateContactAllergenRequest>
            {
                new() { AllergenType = AllergenType.Peanuts, Severity = AllergenSeverity.Allergy },
                new() { AllergenType = AllergenType.Eggs, Severity = AllergenSeverity.Sensitivity }
            },
            DietaryPreferences = new List<DietaryPreference>
            {
                DietaryPreference.GlutenFree
            }
        };

        var result = await _service.UpdateAsync(contactId, request);

        result.DietaryNotes.Should().Be("Updated notes");
        result.Allergens.Should().HaveCount(2);
        result.Allergens.Should().Contain(a => a.AllergenType == AllergenType.Peanuts);
        result.Allergens.Should().Contain(a => a.AllergenType == AllergenType.Eggs);
        result.DietaryPreferences.Should().HaveCount(1);
        result.DietaryPreferences[0].DietaryPreference.Should().Be(DietaryPreference.GlutenFree);
    }

    [Fact]
    public async Task UpdateAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var request = new UpdateDietaryProfileRequest
        {
            Allergens = new List<UpdateContactAllergenRequest>(),
            DietaryPreferences = new List<DietaryPreference>()
        };

        var act = () => _service.UpdateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ClearsExistingAllergens_ReplacesWithNew()
    {
        var contactId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = contactId,
            TenantId = _tenantId,
            FirstName = "Test",
            LastName = "User",
            Allergens = new List<ContactAllergen>
            {
                new() { Id = Guid.NewGuid(), ContactId = contactId, AllergenType = AllergenType.Milk, Severity = AllergenSeverity.Allergy },
                new() { Id = Guid.NewGuid(), ContactId = contactId, AllergenType = AllergenType.Eggs, Severity = AllergenSeverity.Sensitivity }
            },
            DietaryPreferences = new List<ContactDietaryPreference>()
        });
        await _context.SaveChangesAsync();

        var request = new UpdateDietaryProfileRequest
        {
            Allergens = new List<UpdateContactAllergenRequest>
            {
                new() { AllergenType = AllergenType.Wheat, Severity = AllergenSeverity.Allergy }
            },
            DietaryPreferences = new List<DietaryPreference>()
        };

        var result = await _service.UpdateAsync(contactId, request);

        result.Allergens.Should().HaveCount(1);
        result.Allergens[0].AllergenType.Should().Be(AllergenType.Wheat);
    }
}
