using AutoMapper;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class MealTypeServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly MealTypeService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public MealTypeServiceTests()
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

        var logger = new Mock<ILogger<MealTypeService>>();

        _service = new MealTypeService(_context, mapper, logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task ListAsync_NoTypes_ReturnsEmptyList()
    {
        var result = await _service.ListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_WithTypes_ReturnsOrderedBySortOrder()
    {
        _context.MealTypes.AddRange(
            new MealType { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Dinner", SortOrder = 2, Color = "#42A5F5" },
            new MealType { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Breakfast", SortOrder = 0, Color = "#FFA726" },
            new MealType { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Lunch", SortOrder = 1, Color = "#66BB6A" }
        );
        await _context.SaveChangesAsync();

        var result = await _service.ListAsync();

        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Breakfast");
        result[1].Name.Should().Be("Lunch");
        result[2].Name.Should().Be("Dinner");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesMealType()
    {
        var request = new CreateMealTypeRequest
        {
            Name = "Brunch",
            SortOrder = 5,
            Color = "#FF5722"
        };

        var result = await _service.CreateAsync(request);

        result.Should().NotBeNull();
        result.Name.Should().Be("Brunch");
        result.SortOrder.Should().Be(5);
        result.Color.Should().Be("#FF5722");
        result.IsDefault.Should().BeFalse();
        result.Id.Should().NotBeEmpty();

        var saved = await _context.MealTypes.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsInvalidOperationException()
    {
        _context.MealTypes.Add(new MealType
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Breakfast", SortOrder = 0
        });
        await _context.SaveChangesAsync();

        var request = new CreateMealTypeRequest { Name = "Breakfast", SortOrder = 5 };

        var act = () => _service.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameCaseInsensitive_ThrowsInvalidOperationException()
    {
        _context.MealTypes.Add(new MealType
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Breakfast", SortOrder = 0
        });
        await _context.SaveChangesAsync();

        var request = new CreateMealTypeRequest { Name = "breakfast", SortOrder = 5 };

        var act = () => _service.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_ExceedsMaxTen_ThrowsInvalidOperationException()
    {
        for (var i = 0; i < 10; i++)
        {
            _context.MealTypes.Add(new MealType
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, Name = $"Type{i}", SortOrder = i
            });
        }
        await _context.SaveChangesAsync();

        var request = new CreateMealTypeRequest { Name = "Eleventh", SortOrder = 10 };

        var act = () => _service.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesMealType()
    {
        var id = Guid.NewGuid();
        _context.MealTypes.Add(new MealType
        {
            Id = id, TenantId = _tenantId, Name = "Old Name", SortOrder = 0
        });
        await _context.SaveChangesAsync();

        var request = new UpdateMealTypeRequest { Name = "New Name", SortOrder = 5, Color = "#000000" };

        var result = await _service.UpdateAsync(id, request);

        result.Name.Should().Be("New Name");
        result.SortOrder.Should().Be(5);
        result.Color.Should().Be("#000000");
    }

    [Fact]
    public async Task UpdateAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var request = new UpdateMealTypeRequest { Name = "Test", SortOrder = 0 };

        var act = () => _service.UpdateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_DuplicateName_ThrowsInvalidOperationException()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _context.MealTypes.AddRange(
            new MealType { Id = id1, TenantId = _tenantId, Name = "Breakfast", SortOrder = 0 },
            new MealType { Id = id2, TenantId = _tenantId, Name = "Lunch", SortOrder = 1 }
        );
        await _context.SaveChangesAsync();

        var request = new UpdateMealTypeRequest { Name = "Breakfast", SortOrder = 1 };

        var act = () => _service.UpdateAsync(id2, request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateAsync_SameNameSameEntity_Succeeds()
    {
        var id = Guid.NewGuid();
        _context.MealTypes.Add(new MealType
        {
            Id = id, TenantId = _tenantId, Name = "Breakfast", SortOrder = 0
        });
        await _context.SaveChangesAsync();

        var request = new UpdateMealTypeRequest { Name = "Breakfast", SortOrder = 5 };

        var result = await _service.UpdateAsync(id, request);

        result.Name.Should().Be("Breakfast");
        result.SortOrder.Should().Be(5);
    }

    [Fact]
    public async Task DeleteAsync_NonDefaultType_DeletesSuccessfully()
    {
        var id = Guid.NewGuid();
        _context.MealTypes.Add(new MealType
        {
            Id = id, TenantId = _tenantId, Name = "Custom", SortOrder = 0, IsDefault = false
        });
        await _context.SaveChangesAsync();

        await _service.DeleteAsync(id);

        var deleted = await _context.MealTypes.FindAsync(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_DefaultType_ThrowsInvalidOperationException()
    {
        var id = Guid.NewGuid();
        _context.MealTypes.Add(new MealType
        {
            Id = id, TenantId = _tenantId, Name = "Breakfast", SortOrder = 0, IsDefault = true
        });
        await _context.SaveChangesAsync();

        var act = () => _service.DeleteAsync(id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var act = () => _service.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SeedDefaultsForTenantAsync_NoExistingTypes_SeedsFourDefaults()
    {
        await _service.SeedDefaultsForTenantAsync(_tenantId);

        var types = await _context.MealTypes.Where(t => t.TenantId == _tenantId).ToListAsync();
        types.Should().HaveCount(4);
        types.Should().Contain(t => t.Name == "Breakfast");
        types.Should().Contain(t => t.Name == "Lunch");
        types.Should().Contain(t => t.Name == "Dinner");
        types.Should().Contain(t => t.Name == "Snack");
        types.Should().OnlyContain(t => t.IsDefault);
    }

    [Fact]
    public async Task SeedDefaultsForTenantAsync_ExistingTypes_DoesNothing()
    {
        _context.MealTypes.Add(new MealType
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Custom", SortOrder = 0
        });
        await _context.SaveChangesAsync();

        await _service.SeedDefaultsForTenantAsync(_tenantId);

        var types = await _context.MealTypes.Where(t => t.TenantId == _tenantId).ToListAsync();
        types.Should().HaveCount(1);
        types[0].Name.Should().Be("Custom");
    }

    [Fact]
    public async Task CreateFromOnboardingAsync_CreatesSelectedTypes()
    {
        var selections = new List<OnboardingMealTypeSelection>
        {
            new() { Name = "Breakfast", Color = "#FFA726" },
            new() { Name = "Dinner", Color = "#42A5F5" },
            new() { Name = "Supper", Color = "#5C6BC0" }
        };

        await _service.CreateFromOnboardingAsync(_tenantId, selections);

        var types = await _context.MealTypes.Where(t => t.TenantId == _tenantId).OrderBy(t => t.SortOrder).ToListAsync();
        types.Should().HaveCount(3);
        types[0].Name.Should().Be("Breakfast");
        types[0].Color.Should().Be("#FFA726");
        types[0].SortOrder.Should().Be(0);
        types[0].IsDefault.Should().BeTrue();
        types[1].Name.Should().Be("Dinner");
        types[1].SortOrder.Should().Be(1);
        types[2].Name.Should().Be("Supper");
        types[2].SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task CreateFromOnboardingAsync_SkipsIfTypesExist()
    {
        _context.MealTypes.Add(new MealType
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Existing", SortOrder = 0
        });
        await _context.SaveChangesAsync();

        var selections = new List<OnboardingMealTypeSelection>
        {
            new() { Name = "Breakfast", Color = "#FFA726" }
        };

        await _service.CreateFromOnboardingAsync(_tenantId, selections);

        var types = await _context.MealTypes.Where(t => t.TenantId == _tenantId).ToListAsync();
        types.Should().HaveCount(1);
        types[0].Name.Should().Be("Existing");
    }
}
