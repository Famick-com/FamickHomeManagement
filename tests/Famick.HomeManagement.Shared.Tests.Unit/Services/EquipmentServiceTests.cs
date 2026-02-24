using Famick.HomeManagement.Core.DTOs.Equipment;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Unit tests for EquipmentService focusing on tree structure functionality
/// </summary>
public class EquipmentServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly EquipmentService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public EquipmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        var mockFileStorage = new Mock<IFileStorageService>();
        var mockTokenService = new Mock<IFileAccessTokenService>();
        var logger = new Mock<ILogger<EquipmentService>>();

        _service = new EquipmentService(
            _context,
            mockFileStorage.Object,
            mockTokenService.Object,
            logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Equipment Tree Tests

    [Fact]
    public async Task GetEquipmentTreeAsync_ReturnsHierarchicalStructure()
    {
        // Arrange - Create parent equipment
        var parent = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "HVAC System",
            ParentEquipmentId = null,
            CreatedAt = DateTime.UtcNow
        };
        _context.Equipment.Add(parent);

        // Create child equipment
        var child = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Air Conditioner",
            ParentEquipmentId = parent.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Equipment.Add(child);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetEquipmentTreeAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1); // Only root items at top level
        result[0].Name.Should().Be("HVAC System");
        result[0].Children.Should().HaveCount(1);
        result[0].Children[0].Name.Should().Be("Air Conditioner");
    }

    [Fact]
    public async Task GetEquipmentTreeAsync_ChildrenNestedUnderParent()
    {
        // Arrange - Create a parent with multiple children
        var parent = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Kitchen Appliances",
            ParentEquipmentId = null,
            CreatedAt = DateTime.UtcNow
        };
        _context.Equipment.Add(parent);

        var child1 = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Refrigerator",
            ParentEquipmentId = parent.Id,
            CreatedAt = DateTime.UtcNow
        };
        var child2 = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Dishwasher",
            ParentEquipmentId = parent.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Equipment.AddRange(child1, child2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetEquipmentTreeAsync();

        // Assert
        result.Should().HaveCount(1);
        var parentNode = result[0];
        parentNode.Children.Should().HaveCount(2);
        parentNode.Children.Should().Contain(c => c.Name == "Refrigerator");
        parentNode.Children.Should().Contain(c => c.Name == "Dishwasher");
    }

    [Fact]
    public async Task GetEquipmentTreeAsync_OrphanedEquipment_AtTopLevel()
    {
        // Arrange - Create equipment without parents (orphaned/root items)
        var item1 = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Lawn Mower",
            ParentEquipmentId = null,
            CreatedAt = DateTime.UtcNow
        };
        var item2 = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Snow Blower",
            ParentEquipmentId = null,
            CreatedAt = DateTime.UtcNow
        };
        _context.Equipment.AddRange(item1, item2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetEquipmentTreeAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Name == "Lawn Mower");
        result.Should().Contain(e => e.Name == "Snow Blower");
        result.All(e => e.Children.Count == 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetEquipmentTreeAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetEquipmentTreeAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEquipmentTreeAsync_MultiLevelHierarchy_NestedCorrectly()
    {
        // Arrange - Create 3-level hierarchy
        var grandparent = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Building Systems",
            ParentEquipmentId = null,
            CreatedAt = DateTime.UtcNow
        };
        _context.Equipment.Add(grandparent);

        var parent = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "HVAC",
            ParentEquipmentId = grandparent.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Equipment.Add(parent);

        var child = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Thermostat",
            ParentEquipmentId = parent.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Equipment.Add(child);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetEquipmentTreeAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Building Systems");
        result[0].Children.Should().HaveCount(1);
        result[0].Children[0].Name.Should().Be("HVAC");
        result[0].Children[0].Children.Should().HaveCount(1);
        result[0].Children[0].Children[0].Name.Should().Be("Thermostat");
    }

    #endregion
}
