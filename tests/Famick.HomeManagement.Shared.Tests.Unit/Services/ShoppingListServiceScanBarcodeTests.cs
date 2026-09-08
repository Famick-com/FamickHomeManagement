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
/// Covers <see cref="ShoppingListService.ScanBarcodeAsync"/>, and in particular the resolved-product
/// block it reports when a scanned barcode is a known product that simply isn't on the list — which
/// is what lets the mobile scan path skip a second products/by-barcode round-trip.
/// </summary>
public class ShoppingListServiceScanBarcodeTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly ShoppingListService _service;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _listId = Guid.NewGuid();

    public ShoppingListServiceScanBarcodeTests()
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

        _context.Set<ShoppingList>().Add(new ShoppingList
        {
            Id = _listId,
            TenantId = _tenantId,
            Name = "Test List"
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private Product CreateProduct(
        string name,
        string barcode,
        bool tracksBestBefore = true,
        int defaultBestBeforeDays = 7,
        Guid? parentProductId = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = name,
            TracksBestBeforeDate = tracksBestBefore,
            DefaultBestBeforeDays = defaultBestBeforeDays,
            ParentProductId = parentProductId
        };
        _context.Set<Product>().Add(product);

        _context.Set<ProductBarcode>().Add(new ProductBarcode
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProductId = product.Id,
            Barcode = barcode
        });

        return product;
    }

    private ShoppingListItem AddToList(Product product)
    {
        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ShoppingListId = _listId,
            ProductId = product.Id,
            ProductName = product.Name,
            Amount = 1
        };
        _context.ShoppingListItems.Add(item);
        return item;
    }

    [Fact]
    public async Task ScanBarcodeAsync_WhenProductIsOnTheList_ReportsFoundWithoutResolvedProduct()
    {
        // The resolved block exists only to save the caller a lookup it would otherwise make
        // after a miss. A hit already carries everything it needs, so leaving it null keeps
        // the two cases unambiguous for the client.
        var product = CreateProduct("Milk", "761720051108");
        var item = AddToList(product);
        await _context.SaveChangesAsync();

        var result = await _service.ScanBarcodeAsync(_listId, "761720051108");

        result.Found.Should().BeTrue();
        result.ItemId.Should().Be(item.Id);
        result.ResolvedProductId.Should().BeNull();
        result.ResolvedProductName.Should().BeNull();
    }

    [Fact]
    public async Task ScanBarcodeAsync_WhenProductIsKnownButNotOnTheList_ReportsTheResolvedProduct()
    {
        var product = CreateProduct("Peanut Butter", "051500255162",
            tracksBestBefore: true, defaultBestBeforeDays: 180);
        await _context.SaveChangesAsync();

        var result = await _service.ScanBarcodeAsync(_listId, "051500255162");

        result.Found.Should().BeFalse();
        result.ResolvedProductId.Should().Be(product.Id);
        result.ResolvedProductName.Should().Be("Peanut Butter");
        result.ResolvedTracksBestBeforeDate.Should().BeTrue();
        result.ResolvedDefaultBestBeforeDays.Should().Be(180);
    }

    [Fact]
    public async Task ScanBarcodeAsync_CarriesTracksBestBeforeDateFalseThrough()
    {
        // A false here has to be distinguishable from "we didn't look" — the client uses it to
        // decide whether to prompt for a date it can never recover later.
        CreateProduct("Aluminium Foil", "012345678905",
            tracksBestBefore: false, defaultBestBeforeDays: 0);
        await _context.SaveChangesAsync();

        var result = await _service.ScanBarcodeAsync(_listId, "012345678905");

        result.ResolvedProductId.Should().NotBeNull();
        result.ResolvedTracksBestBeforeDate.Should().BeFalse();
        result.ResolvedDefaultBestBeforeDays.Should().Be(0);
    }

    [Fact]
    public async Task ScanBarcodeAsync_WhenBarcodeIsUnknown_ReportsNothingResolved()
    {
        CreateProduct("Milk", "761720051108");
        await _context.SaveChangesAsync();

        var result = await _service.ScanBarcodeAsync(_listId, "999999999999");

        result.Found.Should().BeFalse();
        result.ResolvedProductId.Should().BeNull();
        result.ResolvedProductName.Should().BeNull();
    }

    [Fact]
    public async Task ScanBarcodeAsync_WhenBarcodeMatchesParentAndChild_PrefersTheParent()
    {
        // Both share a barcode and neither is on the list. The add-item prompt should offer the
        // parent, because that is what the downstream child-selection flow expects to work from.
        var parent = CreateProduct("Soda", "049000050100");
        var child = CreateProduct("Soda - 12pk", "049000050100", parentProductId: parent.Id);
        await _context.SaveChangesAsync();

        var result = await _service.ScanBarcodeAsync(_listId, "049000050100");

        result.Found.Should().BeFalse();
        result.ResolvedProductId.Should().Be(parent.Id);
        result.ResolvedProductId.Should().NotBe(child.Id);
    }

    [Fact]
    public async Task ScanBarcodeAsync_ResolvesAType2BarcodeByItsItemNumber()
    {
        // A variable-weight barcode's raw string is unique per package, so the stored barcode is
        // the 5-digit item number. The resolved block still has to come back populated.
        var product = CreateProduct("Ground Beef", "81234");
        await _context.SaveChangesAsync();

        var result = await _service.ScanBarcodeAsync(_listId, "281234001500");

        result.Found.Should().BeFalse();
        result.ResolvedProductId.Should().Be(product.Id);
        result.ResolvedProductName.Should().Be("Ground Beef");
    }

    [Fact]
    public async Task ScanBarcodeAsync_StillFindsAType2ItemThatIsOnTheList()
    {
        var product = CreateProduct("Ground Beef", "81234");
        var item = AddToList(product);
        await _context.SaveChangesAsync();

        var result = await _service.ScanBarcodeAsync(_listId, "281234001500");

        result.Found.Should().BeTrue();
        result.ItemId.Should().Be(item.Id);
        result.EmbeddedWeight.Should().Be(1.50m);
    }

    [Fact]
    public async Task ScanBarcodeAsync_DoesNotResolveAgainstAnotherListsProducts()
    {
        // Product resolution is catalogue-wide by design — the shopper can scan anything in the
        // aisle — but the Found flag must stay scoped to the list being shopped.
        var otherList = new ShoppingList { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Other" };
        _context.Set<ShoppingList>().Add(otherList);

        var product = CreateProduct("Milk", "761720051108");
        _context.ShoppingListItems.Add(new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ShoppingListId = otherList.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            Amount = 1
        });
        await _context.SaveChangesAsync();

        var result = await _service.ScanBarcodeAsync(_listId, "761720051108");

        result.Found.Should().BeFalse();
        result.ResolvedProductId.Should().Be(product.Id);
    }
}
