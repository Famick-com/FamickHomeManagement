using AutoMapper;
using Famick.HomeManagement.Core.DTOs.ProductGroups;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit;

/// <summary>
/// Basic smoke tests for Phase 3 services to verify core functionality
/// </summary>
public class BasicServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public BasicServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ProductGroupMappingProfile>();
            cfg.AddProfile<ShoppingLocationMappingProfile>();
        });
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void ProductGroupMappingProfile_ShouldBeValid()
    {
        // Arrange
        var config = new MapperConfiguration(cfg => cfg.AddProfile<ProductGroupMappingProfile>());

        // Act & Assert
        config.AssertConfigurationIsValid();
    }

    [Fact]
    public async Task ProductGroupService_CreateAsync_ShouldWork()
    {
        // Arrange
        var logger = new Mock<ILogger<ProductGroupService>>();
        var service = new ProductGroupService(_context, _mapper, logger.Object);
        var request = new CreateProductGroupRequest
        {
            Name = "Test Group",
            Description = "Test"
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Group");
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ProductGroupService_GetByIdAsync_ShouldWork()
    {
        // Arrange
        var logger = new Mock<ILogger<ProductGroupService>>();
        var service = new ProductGroupService(_context, _mapper, logger.Object);
        var created = await service.CreateAsync(new CreateProductGroupRequest { Name = "Test" });

        // Act
        var result = await service.GetByIdAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task ProductGroupService_ListAsync_ShouldReturnAll()
    {
        // Arrange
        var logger = new Mock<ILogger<ProductGroupService>>();
        var service = new ProductGroupService(_context, _mapper, logger.Object);
        await service.CreateAsync(new CreateProductGroupRequest { Name = "Group 1" });
        await service.CreateAsync(new CreateProductGroupRequest { Name = "Group 2" });

        // Act
        var result = await service.ListAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ProductGroupService_UpdateAsync_ShouldWork()
    {
        // Arrange
        var logger = new Mock<ILogger<ProductGroupService>>();
        var service = new ProductGroupService(_context, _mapper, logger.Object);
        var created = await service.CreateAsync(new CreateProductGroupRequest { Name = "Original" });

        // Act
        var result = await service.UpdateAsync(created.Id, new UpdateProductGroupRequest { Name = "Updated" });

        // Assert
        result.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task ProductGroupService_DeleteAsync_ShouldWork()
    {
        // Arrange
        var logger = new Mock<ILogger<ProductGroupService>>();
        var service = new ProductGroupService(_context, _mapper, logger.Object);
        var created = await service.CreateAsync(new CreateProductGroupRequest { Name = "ToDelete" });

        // Act
        await service.DeleteAsync(created.Id);

        // Assert
        var result = await service.GetByIdAsync(created.Id);
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
