using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Pins the barcode contract of the shopping-list payload.
/// </summary>
/// <remarks>
/// The mobile app caches this payload into SQLite and matches scanned barcodes against it
/// on-device, so a shopper checking off something already on the list never waits on the
/// network. That match is only as good as the barcodes the payload carries — and when it
/// carries none, the scan still resolves correctly via the server, so the regression is
/// invisible in manual testing. These tests are the thing that would catch it.
/// </remarks>
public class ShoppingListServiceBarcodePayloadTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly ShoppingListService _service;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _listId = Guid.NewGuid();

    public ShoppingListServiceBarcodePayloadTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        _service = new ShoppingListService(
            _context,
            new Mock<ILogger<ShoppingListService>>().Object,
            new Mock<IStoreIntegrationService>().Object,
            new Mock<IPluginLoader>().Object,
            new Mock<IStockService>().Object,
            new Mock<ITodoItemService>().Object,
            new Mock<IProductsService>().Object,
            new Mock<IFileUrlService>().Object);

        // The list needs a real store row: ShoppingList.ShoppingLocationId is non-nullable, so
        // EF treats the navigation as required and an Include of it drops any list whose store
        // does not exist — the query comes back empty rather than merely un-populated.
        var storeId = Guid.NewGuid();
        _context.Set<ShoppingLocation>().Add(new ShoppingLocation
        {
            Id = storeId,
            TenantId = _tenantId,
            Name = "Test Store"
        });

        _context.Set<ShoppingList>().Add(new ShoppingList
        {
            Id = _listId,
            TenantId = _tenantId,
            Name = "Test List",
            ShoppingLocationId = storeId
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private Product CreateProduct(string name, params string[] barcodes)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = name
        };
        _context.Set<Product>().Add(product);

        foreach (var barcode in barcodes)
        {
            _context.Set<ProductBarcode>().Add(new ProductBarcode
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ProductId = product.Id,
                Barcode = barcode
            });
        }

        return product;
    }

    private ShoppingListItem AddToList(Product? product, string productName)
    {
        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ShoppingListId = _listId,
            ProductId = product?.Id,
            ProductName = productName,
            Amount = 1
        };
        _context.ShoppingListItems.Add(item);
        return item;
    }

    [Fact]
    public async Task GetListByIdAsync_CarriesEveryBarcodeOfTheLinkedProduct()
    {
        var product = CreateProduct("Milk", "761720051108", "0761720051108", "12345");
        AddToList(product, "Milk");
        await _context.SaveChangesAsync();

        var dto = await _service.GetListByIdAsync(_listId);

        var item = dto!.Items!.Single();
        item.Barcodes.Should().BeEquivalentTo("761720051108", "0761720051108", "12345");
    }

    [Fact]
    public async Task GetListByIdAsync_KeepsEachItemsBarcodesSeparate()
    {
        var milk = CreateProduct("Milk", "761720051108");
        var bread = CreateProduct("Bread", "012345678905", "999999999999");
        AddToList(milk, "Milk");
        AddToList(bread, "Bread");
        await _context.SaveChangesAsync();

        var dto = await _service.GetListByIdAsync(_listId);

        dto!.Items!.Single(i => i.ProductName == "Milk").Barcodes
            .Should().BeEquivalentTo("761720051108");
        dto.Items!.Single(i => i.ProductName == "Bread").Barcodes
            .Should().BeEquivalentTo("012345678905", "999999999999");
    }

    [Fact]
    public async Task GetListByIdAsync_LeavesFreeTextItemsWithNoBarcodes()
    {
        // Items the shopper typed have no linked product, so there is nothing to attach.
        AddToList(null, "Something from the deli counter");
        await _context.SaveChangesAsync();

        var dto = await _service.GetListByIdAsync(_listId);

        dto!.Items!.Single().Barcodes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetListByIdAsync_LeavesProductsWithNoBarcodesEmpty()
    {
        var product = CreateProduct("Loose carrots");
        AddToList(product, "Loose carrots");
        await _context.SaveChangesAsync();

        var dto = await _service.GetListByIdAsync(_listId);

        dto!.Items!.Single().Barcodes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetListByIdAsync_ShareBarcodesAcrossTwoItemsOfTheSameProduct()
    {
        // The same product can appear twice (different notes or units). Both need the barcodes,
        // not just whichever the grouping happened to see first.
        var product = CreateProduct("Milk", "761720051108");
        AddToList(product, "Milk");
        AddToList(product, "Milk");
        await _context.SaveChangesAsync();

        var dto = await _service.GetListByIdAsync(_listId);

        dto!.Items!.Should().HaveCount(2);
        dto.Items!.Should().OnlyContain(i => i.Barcodes.Contains("761720051108"));
    }
}
