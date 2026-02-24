using System.Text.Json;
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

public class ShoppingListServiceChildProductTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly ShoppingListService _service;
    private readonly Mock<IStoreIntegrationService> _storeIntegrationMock;
    private readonly Mock<IPluginLoader> _pluginLoaderMock;
    private readonly Mock<IStockService> _stockServiceMock;
    private readonly Mock<ITodoItemService> _todoItemServiceMock;
    private readonly Mock<IProductsService> _productsServiceMock;
    private readonly Mock<IFileAccessTokenService> _tokenServiceMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;

    private readonly Guid _tenantId = Guid.NewGuid();

    public ShoppingListServiceChildProductTests()
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

        _storeIntegrationMock = new Mock<IStoreIntegrationService>();
        _pluginLoaderMock = new Mock<IPluginLoader>();
        _stockServiceMock = new Mock<IStockService>();
        _todoItemServiceMock = new Mock<ITodoItemService>();
        _productsServiceMock = new Mock<IProductsService>();
        _tokenServiceMock = new Mock<IFileAccessTokenService>();
        _fileStorageMock = new Mock<IFileStorageService>();

        var logger = new Mock<ILogger<ShoppingListService>>();

        _service = new ShoppingListService(
            _context,
            _mapper,
            logger.Object,
            _storeIntegrationMock.Object,
            _pluginLoaderMock.Object,
            _stockServiceMock.Object,
            _todoItemServiceMock.Object,
            _productsServiceMock.Object,
            _tokenServiceMock.Object,
            _fileStorageMock.Object);
    }

    private async Task<(ShoppingList list, ShoppingListItem item, Product parent, List<Product> children, ShoppingLocation store)>
        SetupParentChildScenarioAsync()
    {
        var location = new Location
        {
            Id = Guid.NewGuid(),
            Name = "Kitchen",
            TenantId = _tenantId
        };
        _context.Locations.Add(location);

        var unit = new QuantityUnit
        {
            Id = Guid.NewGuid(),
            Name = "Each",
            TenantId = _tenantId
        };
        _context.QuantityUnits.Add(unit);

        var store = new ShoppingLocation
        {
            Id = Guid.NewGuid(),
            Name = "Test Store",
            IntegrationType = "TestPlugin",
            TenantId = _tenantId
        };
        _context.ShoppingLocations.Add(store);

        var parentProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Milk",
            LocationId = location.Id,
            QuantityUnitIdPurchase = unit.Id,
            QuantityUnitIdStock = unit.Id,
            TenantId = _tenantId
        };
        _context.Products.Add(parentProduct);

        var children = new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Whole Milk 1 Gallon",
                ParentProductId = parentProduct.Id,
                LocationId = location.Id,
                QuantityUnitIdPurchase = unit.Id,
                QuantityUnitIdStock = unit.Id,
                TenantId = _tenantId
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "2% Milk 1 Gallon",
                ParentProductId = parentProduct.Id,
                LocationId = location.Id,
                QuantityUnitIdPurchase = unit.Id,
                QuantityUnitIdStock = unit.Id,
                TenantId = _tenantId
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Skim Milk 1 Gallon",
                ParentProductId = parentProduct.Id,
                LocationId = location.Id,
                QuantityUnitIdPurchase = unit.Id,
                QuantityUnitIdStock = unit.Id,
                TenantId = _tenantId
            }
        };
        _context.Products.AddRange(children);

        // Add store metadata for first two children
        _context.Set<ProductStoreMetadata>().AddRange(new[]
        {
            new ProductStoreMetadata
            {
                Id = Guid.NewGuid(),
                ProductId = children[0].Id,
                ShoppingLocationId = store.Id,
                ExternalProductId = "EXT001",
                LastKnownPrice = 4.99m,
                Aisle = "1",
                TenantId = _tenantId
            },
            new ProductStoreMetadata
            {
                Id = Guid.NewGuid(),
                ProductId = children[1].Id,
                ShoppingLocationId = store.Id,
                ExternalProductId = "EXT002",
                LastKnownPrice = 4.79m,
                Aisle = "1",
                TenantId = _tenantId
            }
        });

        var list = new ShoppingList
        {
            Id = Guid.NewGuid(),
            Name = "Weekly Groceries",
            ShoppingLocationId = store.Id,
            TenantId = _tenantId
        };
        _context.ShoppingLists.Add(list);

        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = list.Id,
            ProductId = parentProduct.Id,
            Amount = 2,
            TenantId = _tenantId
        };
        _context.ShoppingListItems.Add(item);

        await _context.SaveChangesAsync();

        return (list, item, parentProduct, children, store);
    }

    #region GetChildProductsForItemAsync Tests

    [Fact]
    public async Task GetChildProductsForItemAsync_ReturnsOnlyChildrenWithStoreMetadata()
    {
        var (_, item, _, children, _) = await SetupParentChildScenarioAsync();

        var result = await _service.GetChildProductsForItemAsync(item.Id);

        result.Should().HaveCount(2); // Only first two have store metadata
        result.Select(c => c.ProductId).Should().BeEquivalentTo(new[] { children[0].Id, children[1].Id });
    }

    [Fact]
    public async Task GetChildProductsForItemAsync_ReturnsEmptyWhenNoChildrenHaveMetadata()
    {
        var (list, _, parent, children, store) = await SetupParentChildScenarioAsync();

        // Remove all store metadata
        var metadata = await _context.Set<ProductStoreMetadata>().ToListAsync();
        _context.Set<ProductStoreMetadata>().RemoveRange(metadata);
        await _context.SaveChangesAsync();

        var item = await _context.ShoppingListItems.FirstAsync(i => i.ProductId == parent.Id);
        var result = await _service.GetChildProductsForItemAsync(item.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChildProductsForItemAsync_IncludesPurchasedQuantities()
    {
        var (_, item, _, children, _) = await SetupParentChildScenarioAsync();

        // Add some purchases
        var purchases = new List<ChildPurchaseEntry>
        {
            new() { ChildProductId = children[0].Id, ChildProductName = "Whole Milk", Quantity = 1 }
        };
        item.ChildPurchasesJson = JsonSerializer.Serialize(purchases);
        await _context.SaveChangesAsync();

        var result = await _service.GetChildProductsForItemAsync(item.Id);

        var wholeMilk = result.First(c => c.ProductId == children[0].Id);
        wholeMilk.PurchasedQuantity.Should().Be(1);

        var twoPercent = result.First(c => c.ProductId == children[1].Id);
        twoPercent.PurchasedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task GetChildProductsForItemAsync_ItemNotFound_ThrowsException()
    {
        var act = () => _service.GetChildProductsForItemAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region CheckOffChildAsync Tests

    [Fact]
    public async Task CheckOffChildAsync_AddsChildPurchaseEntry()
    {
        var (_, item, _, children, _) = await SetupParentChildScenarioAsync();

        var request = new CheckOffChildRequest
        {
            ChildProductId = children[0].Id,
            Quantity = 1
        };

        var result = await _service.CheckOffChildAsync(item.Id, request);

        result.ChildPurchases.Should().HaveCount(1);
        result.ChildPurchases![0].ChildProductId.Should().Be(children[0].Id);
        result.ChildPurchases[0].Quantity.Should().Be(1);
        result.ChildPurchasedQuantity.Should().Be(1);
    }

    [Fact]
    public async Task CheckOffChildAsync_UpdatesExistingEntry()
    {
        var (_, item, _, children, _) = await SetupParentChildScenarioAsync();

        // First check-off
        await _service.CheckOffChildAsync(item.Id, new CheckOffChildRequest
        {
            ChildProductId = children[0].Id,
            Quantity = 1
        });

        // Second check-off of same child
        var result = await _service.CheckOffChildAsync(item.Id, new CheckOffChildRequest
        {
            ChildProductId = children[0].Id,
            Quantity = 1
        });

        result.ChildPurchases.Should().HaveCount(1);
        result.ChildPurchases![0].Quantity.Should().Be(2);
        result.ChildPurchasedQuantity.Should().Be(2);
    }

    [Fact]
    public async Task CheckOffChildAsync_AllowsQuantityExceedingParent()
    {
        var (_, item, _, children, _) = await SetupParentChildScenarioAsync();

        // Parent amount is 2, but we can buy more
        var request = new CheckOffChildRequest
        {
            ChildProductId = children[0].Id,
            Quantity = 5
        };

        var result = await _service.CheckOffChildAsync(item.Id, request);

        result.ChildPurchasedQuantity.Should().Be(5);
        result.RemainingQuantity.Should().Be(-3); // Negative means exceeded
        result.IsPurchased.Should().BeTrue(); // Should be marked purchased
    }

    [Fact]
    public async Task CheckOffChildAsync_ThrowsWhenChildNotValidForParent()
    {
        var (_, item, _, _, _) = await SetupParentChildScenarioAsync();

        var act = () => _service.CheckOffChildAsync(item.Id, new CheckOffChildRequest
        {
            ChildProductId = Guid.NewGuid(), // Invalid child
            Quantity = 1
        });

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*not a child of product*");
    }

    [Fact]
    public async Task CheckOffChildAsync_KeepsParentUnpurchasedWhenPartiallyComplete()
    {
        var (_, item, _, children, _) = await SetupParentChildScenarioAsync();

        // Parent amount is 2, check off 1
        var result = await _service.CheckOffChildAsync(item.Id, new CheckOffChildRequest
        {
            ChildProductId = children[0].Id,
            Quantity = 1
        });

        result.IsPurchased.Should().BeFalse();
        result.ChildPurchasedQuantity.Should().Be(1);
        result.RemainingQuantity.Should().Be(1);
    }

    [Fact]
    public async Task CheckOffChildAsync_MarksParentPurchasedWhenQuantityMet()
    {
        var (_, item, _, children, _) = await SetupParentChildScenarioAsync();

        // Check off exact amount
        var result = await _service.CheckOffChildAsync(item.Id, new CheckOffChildRequest
        {
            ChildProductId = children[0].Id,
            Quantity = 2
        });

        result.IsPurchased.Should().BeTrue();
        result.ChildPurchasedQuantity.Should().Be(2);
        result.RemainingQuantity.Should().Be(0);
    }

    [Fact]
    public async Task CheckOffChildAsync_MarksParentPurchasedWhenQuantityExceeded()
    {
        var (_, item, _, children, _) = await SetupParentChildScenarioAsync();

        var result = await _service.CheckOffChildAsync(item.Id, new CheckOffChildRequest
        {
            ChildProductId = children[0].Id,
            Quantity = 3
        });

        result.IsPurchased.Should().BeTrue();
        result.ChildPurchasedQuantity.Should().Be(3);
        result.RemainingQuantity.Should().Be(-1);
    }

    #endregion

    #region UncheckChildAsync Tests

    [Fact]
    public async Task UncheckChildAsync_RemovesChildPurchaseEntry()
    {
        var (_, item, _, children, _) = await SetupParentChildScenarioAsync();

        // First check off
        await _service.CheckOffChildAsync(item.Id, new CheckOffChildRequest
        {
            ChildProductId = children[0].Id,
            Quantity = 2
        });

        // Then uncheck
        var result = await _service.UncheckChildAsync(item.Id, children[0].Id);

        result.ChildPurchases.Should().BeEmpty();
        result.ChildPurchasedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task UncheckChildAsync_UnmarksParentIfWasPurchased()
    {
        var (_, item, _, children, _) = await SetupParentChildScenarioAsync();

        // Check off enough to mark purchased
        await _service.CheckOffChildAsync(item.Id, new CheckOffChildRequest
        {
            ChildProductId = children[0].Id,
            Quantity = 2
        });

        // Verify parent is purchased
        var updated = await _context.ShoppingListItems.FindAsync(item.Id);
        updated!.IsPurchased.Should().BeTrue();

        // Uncheck
        var result = await _service.UncheckChildAsync(item.Id, children[0].Id);

        result.IsPurchased.Should().BeFalse();
        result.ChildPurchasedQuantity.Should().Be(0);
    }

    #endregion

    #region AddChildToParentAsync Tests

    [Fact]
    public async Task AddChildToParentAsync_AddsExistingProductAsChild()
    {
        var (_, item, parent, children, _) = await SetupParentChildScenarioAsync();

        // Create a new product not yet a child
        var newProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Organic Milk",
            LocationId = parent.LocationId,
            QuantityUnitIdPurchase = parent.QuantityUnitIdPurchase,
            QuantityUnitIdStock = parent.QuantityUnitIdStock,
            TenantId = _tenantId
        };
        _context.Products.Add(newProduct);
        await _context.SaveChangesAsync();

        var result = await _service.AddChildToParentAsync(item.Id, new AddChildToParentRequest
        {
            ProductId = newProduct.Id,
            Quantity = 1
        });

        result.ChildPurchases.Should().HaveCount(1);
        result.ChildPurchases![0].ChildProductId.Should().Be(newProduct.Id);

        // Verify product is now a child
        var updatedProduct = await _context.Products.FindAsync(newProduct.Id);
        updatedProduct!.ParentProductId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task AddChildToParentAsync_ThrowsWhenItemNotParent()
    {
        // Use the standard setup which creates items with a parent product
        var (_, item, parent, _, _) = await SetupParentChildScenarioAsync();

        // Create a new item linked to the list but without a ProductId (ad-hoc item)
        var adHocItem = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = item.ShoppingListId,
            ProductName = "Ad-hoc item without product",
            Amount = 1,
            TenantId = _tenantId
        };
        _context.ShoppingListItems.Add(adHocItem);
        await _context.SaveChangesAsync();

        var act = () => _service.AddChildToParentAsync(adHocItem.Id, new AddChildToParentRequest
        {
            ProductName = "Test",
            Quantity = 1
        });

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*does not have a linked parent product*");
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
