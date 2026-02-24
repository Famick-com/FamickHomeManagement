using AutoMapper;
using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Plugins;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class ShoppingListServiceScanPurchaseTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly ShoppingListService _service;

    private readonly Guid _tenantId = Guid.NewGuid();

    public ShoppingListServiceScanPurchaseTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ShoppingListMappingProfile>();
        });
        _mapper = config.CreateMapper();

        _service = new ShoppingListService(
            _context,
            _mapper,
            new Mock<ILogger<ShoppingListService>>().Object,
            new Mock<IStoreIntegrationService>().Object,
            new Mock<IPluginLoader>().Object,
            new Mock<IStockService>().Object,
            new Mock<ITodoItemService>().Object,
            new Mock<IProductsService>().Object,
            new Mock<IFileAccessTokenService>().Object,
            new Mock<IFileStorageService>().Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<ShoppingListItem> CreateItemAsync(decimal amount, decimal purchasedQuantity = 0)
    {
        var list = new ShoppingList
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Test List"
        };
        _context.Set<ShoppingList>().Add(list);

        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ShoppingListId = list.Id,
            ProductName = "Test Product",
            Amount = amount,
            PurchasedQuantity = purchasedQuantity
        };
        _context.ShoppingListItems.Add(item);

        await _context.SaveChangesAsync();
        return item;
    }

    [Fact]
    public async Task ScanPurchaseAsync_IncrementsPurchasedQuantityByOne()
    {
        // Arrange
        var item = await CreateItemAsync(amount: 3);

        // Act
        var result = await _service.ScanPurchaseAsync(item.Id, new ScanPurchaseRequest());

        // Assert
        result.Item.PurchasedQuantity.Should().Be(1);
        result.IsCompleted.Should().BeFalse();
        result.RemainingQuantity.Should().Be(2);
    }

    [Fact]
    public async Task ScanPurchaseAsync_AutoCompletesWhenQuantityMet()
    {
        // Arrange - already scanned 2 of 3
        var item = await CreateItemAsync(amount: 3, purchasedQuantity: 2);

        // Act
        var result = await _service.ScanPurchaseAsync(item.Id, new ScanPurchaseRequest());

        // Assert
        result.Item.PurchasedQuantity.Should().Be(3);
        result.IsCompleted.Should().BeTrue();
        result.Item.IsPurchased.Should().BeTrue();
        result.Item.PurchasedAt.Should().NotBeNull();
        result.RemainingQuantity.Should().Be(0);
    }

    [Fact]
    public async Task ScanPurchaseAsync_Amount1_CompletesOnFirstScan()
    {
        // Arrange
        var item = await CreateItemAsync(amount: 1);

        // Act
        var result = await _service.ScanPurchaseAsync(item.Id, new ScanPurchaseRequest());

        // Assert
        result.Item.PurchasedQuantity.Should().Be(1);
        result.IsCompleted.Should().BeTrue();
        result.Item.IsPurchased.Should().BeTrue();
        result.RemainingQuantity.Should().Be(0);
    }

    [Fact]
    public async Task ScanPurchaseAsync_Amount0_TreatedAs1_CompletesOnFirstScan()
    {
        // Arrange
        var item = await CreateItemAsync(amount: 0);

        // Act
        var result = await _service.ScanPurchaseAsync(item.Id, new ScanPurchaseRequest());

        // Assert
        result.Item.PurchasedQuantity.Should().Be(1);
        result.IsCompleted.Should().BeTrue();
        result.Item.IsPurchased.Should().BeTrue();
        result.RemainingQuantity.Should().Be(0);
    }

    [Fact]
    public async Task ScanPurchaseAsync_AllowsScansBeondAmount()
    {
        // Arrange - already at Amount, still purchased
        var item = await CreateItemAsync(amount: 2, purchasedQuantity: 2);
        item.IsPurchased = true;
        item.PurchasedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ScanPurchaseAsync(item.Id, new ScanPurchaseRequest());

        // Assert
        result.Item.PurchasedQuantity.Should().Be(3);
        result.IsCompleted.Should().BeTrue();
        result.RemainingQuantity.Should().Be(-1);
    }

    [Fact]
    public async Task ScanPurchaseAsync_ThrowsWhenItemNotFound()
    {
        // Act & Assert
        var act = () => _service.ScanPurchaseAsync(Guid.NewGuid(), new ScanPurchaseRequest());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task TogglePurchasedAsync_OnSyncsPurchasedQuantity()
    {
        // Arrange
        var item = await CreateItemAsync(amount: 3);

        // Act - toggle ON
        var result = await _service.TogglePurchasedAsync(item.Id);

        // Assert
        result.IsPurchased.Should().BeTrue();
        result.PurchasedQuantity.Should().Be(3); // Synced to Amount
    }

    [Fact]
    public async Task TogglePurchasedAsync_OffResetsPurchasedQuantity()
    {
        // Arrange - item is already purchased with some scans
        var item = await CreateItemAsync(amount: 3, purchasedQuantity: 3);
        item.IsPurchased = true;
        item.PurchasedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Act - toggle OFF
        var result = await _service.TogglePurchasedAsync(item.Id);

        // Assert
        result.IsPurchased.Should().BeFalse();
        result.PurchasedQuantity.Should().Be(0); // Reset
    }
}
