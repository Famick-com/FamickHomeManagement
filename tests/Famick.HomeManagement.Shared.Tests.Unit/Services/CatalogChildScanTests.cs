using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Plugin.Abstractions;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Scans of master catalog child barcodes: the barcode of a specific product the catalog lists under one
/// of its generic products. Inventory scans (products/by-barcode) create the specific product as a child
/// of the household's copy of the generic product straight away; shopping scans (shoppinglists/{id}/scan)
/// record it as a pending child and leave the creation to the move to inventory.
/// </summary>
public class CatalogChildScanTests : IDisposable
{
    private const string CansUpc = "049000028904";
    private const string CansLabel = "Coca-Cola, 12 × 12 fl oz cans";
    private const string CaseUpc = "049000012781";

    private readonly DbContextOptions<HomeManagementDbContext> _options = new DbContextOptionsBuilder<HomeManagementDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;
    private readonly HomeManagementDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _listId = Guid.NewGuid();
    private readonly Guid _pantry = Guid.NewGuid();
    private readonly Guid _garage = Guid.NewGuid();
    private readonly Guid _can = Guid.NewGuid();
    private readonly Guid _pack = Guid.NewGuid();
    private readonly MasterProduct _cola;

    private readonly Mock<IProductLookupService> _lookup = new();
    private readonly Mock<ICatalogChildEnrichmentRunner> _runner = new();
    private readonly Mock<IStockService> _stock = new();
    private readonly List<AddStockRequest> _stocked = [];

    private readonly CatalogChildProductService _catalog;
    private readonly ProductsService _products;
    private readonly ShoppingListService _shopping;

    public CatalogChildScanTests()
    {
        _context = new HomeManagementDbContext(_options);

        var tenant = Mock.Of<ITenantProvider>(t => t.TenantId == _tenantId);
        var factory = new Mock<IDbContextFactory<HomeManagementDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_context);
        var search = new ProductSearchService(_context, factory.Object, Mock.Of<IFileUrlService>(),
            Mock.Of<IMasterProductImageResolver>(), new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            tenant, NullLogger<ProductSearchService>.Instance);

        _catalog = new CatalogChildProductService(_context, _lookup.Object, search, _runner.Object, tenant,
            NullLogger<CatalogChildProductService>.Instance);
        _products = new ProductsService(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            Mock.Of<IOutboundHttpFetcher>(), Mock.Of<IMasterProductImageResolver>(), search, _catalog, tenant,
            NullLogger<ProductsService>.Instance);

        _stock.Setup(s => s.AddStockAsync(It.IsAny<AddStockRequest>(), It.IsAny<CancellationToken>()))
            .Callback((AddStockRequest r, CancellationToken _) => _stocked.Add(r))
            .ReturnsAsync(() => new StockEntryDto { Id = Guid.NewGuid() });
        _shopping = new ShoppingListService(_context, NullLogger<ShoppingListService>.Instance,
            Mock.Of<IStoreIntegrationService>(), Mock.Of<IPluginLoader>(), _stock.Object, Mock.Of<ITodoItemService>(),
            _products, Mock.Of<IFileUrlService>(), _catalog);

        _context.Locations.AddRange(
            new Location { Id = _pantry, TenantId = _tenantId, Name = "Pantry" },
            new Location { Id = _garage, TenantId = _tenantId, Name = "Garage" });
        _context.QuantityUnits.AddRange(
            new QuantityUnit { Id = _can, TenantId = _tenantId, Name = "Piece", NamePlural = "Pieces" },
            new QuantityUnit { Id = _pack, TenantId = _tenantId, Name = "Pack", NamePlural = "Packs" });
        // A list's store is a required navigation, so a list whose store row is missing doesn't load.
        var storeId = Guid.NewGuid();
        _context.ShoppingLocations.Add(new ShoppingLocation { Id = storeId, TenantId = _tenantId, Name = "Corner Store" });
        _context.ShoppingLists.Add(new ShoppingList { Id = _listId, TenantId = _tenantId, Name = "Groceries", ShoppingLocationId = storeId });

        _cola = new MasterProduct
        {
            Id = Guid.NewGuid(),
            Name = "Cola Soda",
            Category = "Beverages",
            TracksBestBeforeDate = true,
            DefaultBestBeforeDays = 180
        };
        _context.MasterProducts.Add(_cola);
        AddCatalogChildBarcode(CansUpc, CansLabel);
        AddCatalogChildBarcode(CaseUpc, null);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Inventory scans ─────────────────────────────────────────────────────

    [Fact]
    public async Task Inventory_scan_creates_the_child_under_the_households_generic_product()
    {
        var generic = AddGeneric();

        var result = await _products.GetByBarcodeAsync(CansUpc, createCatalogChild: true);

        result.Should().NotBeNull();
        var child = await _context.Products.SingleAsync(p => p.Id == result!.Id);
        child.ParentProductId.Should().Be(generic.Id);
        child.Name.Should().Be(CansLabel);
        child.LocationId.Should().Be(_garage);
        child.QuantityUnitIdPurchase.Should().Be(_pack);
        child.QuantityUnitIdStock.Should().Be(_can);
        child.QuantityUnitFactorPurchaseToStock.Should().Be(12m);
        child.TracksBestBeforeDate.Should().BeTrue();
        child.DefaultBestBeforeDays.Should().Be(90);
        (await _context.ProductBarcodes.SingleAsync(b => b.Barcode == CansUpc)).ProductId.Should().Be(child.Id);
        _runner.Verify(r => r.Start(_tenantId, child.Id, CansUpc, CansLabel, null), Times.Once);
    }

    [Fact]
    public async Task Inventory_scan_names_the_child_after_the_generic_product_when_the_catalog_has_no_label()
    {
        var generic = AddGeneric();

        var result = await _products.GetByBarcodeAsync(CaseUpc, createCatalogChild: true);

        result!.Name.Should().Be($"{generic.Name} ({CaseUpc})");
        result.ParentProductId.Should().Be(generic.Id);
    }

    [Fact]
    public async Task Inventory_scan_copies_the_generic_product_in_when_the_household_has_none()
    {
        var result = await _products.GetByBarcodeAsync(CansUpc, createCatalogChild: true);

        var parent = await _context.Products.SingleAsync(p => p.MasterProductId == _cola.Id);
        result!.ParentProductId.Should().Be(parent.Id);
        parent.Name.Should().Be("Cola Soda");

        // The catalog's other child barcode is copied onto the generic product, marked as such;
        // the scanned one moved to the child.
        var copied = await _context.ProductBarcodes.SingleAsync(b => b.Barcode == CaseUpc);
        copied.ProductId.Should().Be(parent.Id);
        copied.IsCatalogChild.Should().BeTrue();
        var scanned = await _context.ProductBarcodes.SingleAsync(b => b.Barcode == CansUpc);
        scanned.ProductId.Should().Be(result.Id);
        scanned.IsCatalogChild.Should().BeFalse();
    }

    [Fact]
    public async Task A_second_inventory_scan_matches_the_child_with_no_catalog_step()
    {
        AddGeneric();
        var first = await _products.GetByBarcodeAsync(CansUpc, createCatalogChild: true);

        var second = await _products.GetByBarcodeAsync(CansUpc, createCatalogChild: true);

        second!.Id.Should().Be(first!.Id);
        (await _context.Products.CountAsync(p => p.ParentProductId != null)).Should().Be(1);
        _runner.Verify(r => r.Start(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<ProductLookupLocation?>()), Times.Once);
    }

    [Fact]
    public async Task A_13_digit_scan_of_a_12_digit_catalog_barcode_still_resolves()
    {
        var generic = AddGeneric();

        var result = await _products.GetByBarcodeAsync("0" + CansUpc, createCatalogChild: true);

        result!.ParentProductId.Should().Be(generic.Id);
    }

    [Fact]
    public async Task An_inventory_scan_that_may_not_create_products_finds_only_what_the_household_has()
    {
        AddGeneric();
        var child = await _products.GetByBarcodeAsync(CansUpc, createCatalogChild: true);

        var found = await _products.GetByBarcodeAsync(CansUpc, createCatalogChild: false);
        var notFound = await _products.GetByBarcodeAsync(CaseUpc, createCatalogChild: false);

        found!.Id.Should().Be(child!.Id);
        notFound.Should().BeNull();
        (await _context.Products.CountAsync(p => p.ParentProductId != null)).Should().Be(1);
    }

    [Fact]
    public async Task A_barcode_the_household_already_has_is_left_alone()
    {
        AddGeneric();
        var own = AddProduct("My Cola Cans", CansUpc);

        var inventory = await _products.GetByBarcodeAsync(CansUpc, createCatalogChild: true);
        var shopping = await _shopping.ScanToListAsync(_listId, CansUpc);

        inventory!.Id.Should().Be(own.Id);
        shopping.ListUpdated.Should().BeFalse();
        shopping.ResolvedProductId.Should().Be(own.Id);
        (await _context.Products.CountAsync(p => p.ParentProductId != null)).Should().Be(0);
        (await _context.ShoppingListItems.CountAsync()).Should().Be(0);
    }

    // ── Shopping scans ──────────────────────────────────────────────────────

    [Fact]
    public async Task Shopping_scan_checks_off_the_generic_item_with_a_pending_child()
    {
        var generic = AddGeneric();
        var item = AddToList(generic);

        var result = await _shopping.ScanToListAsync(_listId, CansUpc);

        result.Found.Should().BeTrue();
        result.ListUpdated.Should().BeTrue();
        result.ItemId.Should().Be(item.Id);
        result.IsChildProduct.Should().BeTrue();
        result.ChildProductName.Should().Be(CansLabel);

        var saved = await _context.ShoppingListItems.SingleAsync();
        saved.IsPurchased.Should().BeTrue();
        var pending = Purchases(saved).Single();
        pending.IsPending.Should().BeTrue();
        pending.Barcode.Should().Be(CansUpc);
        pending.ChildProductId.Should().Be(result.ChildProductId!.Value);

        // Nothing is created while shopping.
        (await _context.Products.CountAsync()).Should().Be(1);
        _lookup.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Shopping_scan_adds_the_generic_item_when_it_is_not_on_the_list()
    {
        var generic = AddGeneric();

        var result = await _shopping.ScanToListAsync(_listId, CansUpc);

        var item = await _context.ShoppingListItems.SingleAsync();
        item.ProductId.Should().Be(generic.Id);
        item.IsPurchased.Should().BeTrue();
        result.ItemId.Should().Be(item.Id);
        result.ListUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task Shopping_scan_adds_the_catalog_product_when_the_household_has_no_copy()
    {
        var result = await _shopping.ScanToListAsync(_listId, CansUpc);

        var item = await _context.ShoppingListItems.SingleAsync();
        item.ProductId.Should().BeNull();
        item.MasterProductId.Should().Be(_cola.Id);
        item.ProductName.Should().Be("Cola Soda");
        item.IsPurchased.Should().BeTrue();
        item.BestBeforeDate.Should().Be(DateTime.UtcNow.Date.AddDays(180));
        result.ListUpdated.Should().BeTrue();
        (await _context.Products.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Scanning_the_same_product_twice_adds_to_its_pending_child()
    {
        var generic = AddGeneric();
        var item = AddToList(generic, amount: 2);

        var first = await _shopping.ScanToListAsync(_listId, CansUpc);
        var second = await _shopping.ScanToListAsync(_listId, CansUpc);

        second.ChildProductId.Should().Be(first.ChildProductId!.Value);
        var saved = await _context.ShoppingListItems.SingleAsync(i => i.Id == item.Id);
        Purchases(saved).Single().Quantity.Should().Be(2);
        saved.IsPurchased.Should().BeTrue();
    }

    [Fact]
    public async Task The_existing_scan_barcode_endpoint_is_unchanged_for_catalog_barcodes()
    {
        AddGeneric();

        var result = await _shopping.ScanBarcodeAsync(_listId, CansUpc);

        result.Found.Should().BeFalse();
        result.ListUpdated.Should().BeFalse();
        (await _context.ShoppingListItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_pending_child_shows_on_the_list_before_its_product_exists()
    {
        await _shopping.ScanToListAsync(_listId, CansUpc);

        var list = await _shopping.GetListByIdAsync(_listId);

        var item = list!.Items.Single();
        item.MasterProductId.Should().Be(_cola.Id);
        item.IsParentProduct.Should().BeTrue();
        item.ChildPurchases.Should().ContainSingle(p => p.IsPending && p.ChildProductName == CansLabel);
        item.ChildPurchasedQuantity.Should().Be(1);
    }

    [Fact]
    public async Task The_list_keeps_catalog_child_barcodes_apart_from_the_generic_products_own()
    {
        await _products.EnsureProductFromMasterAsync(_cola.Id);
        var generic = await _context.Products.SingleAsync();
        _context.ProductBarcodes.Add(new ProductBarcode { Id = Guid.NewGuid(), TenantId = _tenantId, ProductId = generic.Id, Barcode = "012345678905" });
        AddToList(generic);

        var list = await _shopping.GetListByIdAsync(_listId);

        var item = list!.Items.Single();
        item.Barcodes.Should().Equal("012345678905");
        item.CatalogChildBarcodes.Should().BeEquivalentTo([CansUpc, CaseUpc]);
    }

    [Fact]
    public async Task A_scan_after_the_household_gets_its_copy_reuses_the_catalog_item()
    {
        // Shopping first, with no household copy: the item stands for the catalog product.
        await _shopping.ScanToListAsync(_listId, CansUpc);
        // An inventory scan of another of its child barcodes then copies it in.
        var other = await _products.GetByBarcodeAsync(CaseUpc, createCatalogChild: true);

        var result = await _shopping.ScanToListAsync(_listId, CansUpc);

        var item = await _context.ShoppingListItems.SingleAsync();
        item.ProductId.Should().Be(other!.ParentProductId);
        item.MasterProductId.Should().BeNull();
        result.ItemId.Should().Be(item.Id);
        Purchases(item).Should().ContainSingle(p => p.IsPending && p.Barcode == CansUpc && p.Quantity == 2);
    }

    [Fact]
    public async Task Unchecking_a_pending_child_keeps_the_item_a_parent_while_others_remain()
    {
        var generic = AddGeneric();
        var item = AddToList(generic, amount: 2);
        var cans = await _shopping.ScanToListAsync(_listId, CansUpc);
        await _shopping.ScanToListAsync(_listId, CaseUpc);

        var dto = await _shopping.UncheckChildAsync(item.Id, cans.ChildProductId!.Value);

        dto.IsParentProduct.Should().BeTrue();
        dto.ChildPurchases.Should().ContainSingle(p => p.Barcode == CaseUpc);
    }

    // ── Move to inventory ───────────────────────────────────────────────────

    [Fact]
    public async Task Move_to_inventory_creates_the_pending_child_and_stocks_it()
    {
        var generic = AddGeneric();
        var item = AddToList(generic);
        var scan = await _shopping.ScanToListAsync(_listId, CansUpc);

        var response = await _shopping.MoveToInventoryAsync(MoveRequest(item.Id, generic.Id));

        response.Errors.Should().BeEmpty();
        var child = await _context.Products.SingleAsync(p => p.ParentProductId == generic.Id);
        child.Id.Should().Be(scan.ChildProductId!.Value);
        child.Name.Should().Be(CansLabel);
        (await _context.ProductBarcodes.SingleAsync(b => b.Barcode == CansUpc)).ProductId.Should().Be(child.Id);
        _stocked.Should().ContainSingle(s => s.ProductId == child.Id && s.Amount == 12m);
    }

    [Fact]
    public async Task Move_to_inventory_copies_in_the_catalog_product_then_creates_the_child()
    {
        var scan = await _shopping.ScanToListAsync(_listId, CansUpc);
        var item = await _context.ShoppingListItems.SingleAsync();

        var response = await _shopping.MoveToInventoryAsync(MoveRequest(item.Id, productId: null));

        response.Errors.Should().BeEmpty();
        var parent = await _context.Products.SingleAsync(p => p.MasterProductId == _cola.Id);
        var child = await _context.Products.SingleAsync(p => p.ParentProductId == parent.Id);
        child.Id.Should().Be(scan.ChildProductId!.Value);
        _stocked.Should().ContainSingle(s => s.ProductId == child.Id);
        (await _context.ProductBarcodes.SingleAsync(b => b.Barcode == CansUpc)).ProductId.Should().Be(child.Id);
        (await _context.ProductBarcodes.SingleAsync(b => b.Barcode == CaseUpc)).IsCatalogChild.Should().BeTrue();
        _runner.Verify(r => r.Start(_tenantId, child.Id, CansUpc, CansLabel, null), Times.Once);
    }

    [Fact]
    public async Task A_copied_child_barcode_is_promoted_to_its_own_child_on_a_scan()
    {
        await _products.EnsureProductFromMasterAsync(_cola.Id);
        var generic = await _context.Products.SingleAsync();

        var result = await _products.GetByBarcodeAsync(CaseUpc, createCatalogChild: true);

        result!.ParentProductId.Should().Be(generic.Id);
        var row = await _context.ProductBarcodes.SingleAsync(b => b.Barcode == CaseUpc);
        row.ProductId.Should().Be(result.Id);
        row.IsCatalogChild.Should().BeFalse();
        (await _context.ProductBarcodes.SingleAsync(b => b.Barcode == CansUpc)).ProductId.Should().Be(generic.Id);
    }

    // ── Filling in details ──────────────────────────────────────────────────

    [Fact]
    public async Task Lookup_details_replace_the_provisional_name()
    {
        var child = await CreateChildAsync();
        LookupReturns(new ProductLookupResult
        {
            Name = "Coca-Cola Original Taste Soda",
            BrandName = "Coca-Cola",
            Description = "12 x 12 fl oz cans",
            Barcodes = [Parse(CansUpc)]
        });

        await _catalog.EnrichAsync(child.Id, CansUpc, CansLabel);

        var saved = await _context.Products.SingleAsync(p => p.Id == child.Id);
        saved.Name.Should().Be("Coca-Cola Original Taste Soda");
        saved.Brand.Should().Be("Coca-Cola");
        saved.Description.Should().Be("12 x 12 fl oz cans");
        _lookup.Verify(l => l.SearchAsync(CansUpc, It.IsAny<int>(), ProductSearchMode.ExternalSourcesOnly,
            null, It.IsAny<CancellationToken>()), Times.Once);
        _lookup.Verify(l => l.ApplyLookupResultAsync(child.Id, It.IsAny<ProductLookupResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void The_result_carrying_the_scanned_barcode_wins_over_a_near_match()
    {
        var results = new List<ProductLookupResult>
        {
            new() { Name = "Coca-Cola Zero Sugar", Barcodes = [Parse("049000042849")] },
            new() { Name = "Coca-Cola Original Taste Soda", Barcodes = [Parse(CansUpc)] }
        };

        CatalogChildProductService.PickResult(results, CansUpc)!.Name.Should().Be("Coca-Cola Original Taste Soda");
        CatalogChildProductService.PickResult(results, "012345678905")!.Name.Should().Be("Coca-Cola Zero Sugar");
    }

    [Fact]
    public async Task A_failing_lookup_leaves_the_child_with_its_provisional_name()
    {
        var child = await CreateChildAsync();
        _lookup.Setup(l => l.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ProductSearchMode>(),
                It.IsAny<ProductLookupLocation?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("plugin down"));

        var enrich = () => _catalog.EnrichAsync(child.Id, CansUpc, CansLabel);

        await enrich.Should().NotThrowAsync();
        (await _context.Products.SingleAsync(p => p.Id == child.Id)).Name.Should().Be(CansLabel);
    }

    [Fact]
    public async Task A_lookup_that_finds_nothing_leaves_the_child_with_its_provisional_name()
    {
        var child = await CreateChildAsync();
        LookupReturns();

        await _catalog.EnrichAsync(child.Id, CansUpc, CansLabel);

        (await _context.Products.SingleAsync(p => p.Id == child.Id)).Name.Should().Be(CansLabel);
        _lookup.Verify(l => l.ApplyLookupResultAsync(It.IsAny<Guid>(), It.IsAny<ProductLookupResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Lookup_details_are_not_applied_over_a_name_the_household_changed()
    {
        var child = await CreateChildAsync();
        child.Name = "Big pack of Coke";
        await _context.SaveChangesAsync();
        LookupReturns(new ProductLookupResult { Name = "Coca-Cola Original Taste Soda", Barcodes = [Parse(CansUpc)] });

        await _catalog.EnrichAsync(child.Id, CansUpc, CansLabel);

        (await _context.Products.SingleAsync(p => p.Id == child.Id)).Name.Should().Be("Big pack of Coke");
        _lookup.Verify(l => l.ApplyLookupResultAsync(It.IsAny<Guid>(), It.IsAny<ProductLookupResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_provisional_name_is_shortened_to_fit_the_name_column()
    {
        var generic = AddGeneric();
        generic.Name = new string('x', 300);
        await _context.SaveChangesAsync();

        var child = await _catalog.CreateChildAsync(generic.Id, CaseUpc, label: null);

        child.Name.Should().HaveLength(300).And.EndWith($" ({CaseUpc})");
    }

    [Fact]
    public async Task A_copied_child_barcode_can_only_be_claimed_while_it_is_still_marked()
    {
        await _products.EnsureProductFromMasterAsync(_cola.Id);
        await using var other = new HomeManagementDbContext(_options);
        var stale = await other.ProductBarcodes.SingleAsync(b => b.Barcode == CansUpc);

        // This scan claims the barcode for its new child...
        await _products.GetByBarcodeAsync(CansUpc, createCatalogChild: true);

        // ...so a second claim made from what it read before that one fails rather than overwriting it.
        stale.ProductId = Guid.NewGuid();
        stale.IsCatalogChild = false;
        var claim = () => other.SaveChangesAsync();
        await claim.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task A_provisional_name_another_product_has_is_qualified_by_the_barcode()
    {
        AddProduct(CansLabel, "012345678905");
        var child = await CreateChildAsync();

        child.Name.Should().Be($"{CansLabel} ({CansUpc})");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void AddCatalogChildBarcode(string barcode, string? note) =>
        _context.MasterProductBarcodes.Add(new MasterProductBarcode
        {
            Id = Guid.NewGuid(),
            MasterProductId = _cola.Id,
            Barcode = barcode,
            Note = note,
            IsChildCandidate = true
        });

    /// <summary>The household's copy of the catalog's "Cola Soda", with settings of its own.</summary>
    private Product AddGeneric()
    {
        var generic = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Cola (cans)",
            MasterProductId = _cola.Id,
            LocationId = _garage,
            QuantityUnitIdPurchase = _pack,
            QuantityUnitIdStock = _can,
            QuantityUnitFactorPurchaseToStock = 12m,
            TracksBestBeforeDate = true,
            DefaultBestBeforeDays = 90
        };
        _context.Products.Add(generic);
        _context.SaveChanges();
        return generic;
    }

    private Product AddProduct(string name, string barcode)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = name,
            LocationId = _pantry,
            QuantityUnitIdPurchase = _can,
            QuantityUnitIdStock = _can
        };
        _context.Products.Add(product);
        _context.ProductBarcodes.Add(new ProductBarcode { Id = Guid.NewGuid(), TenantId = _tenantId, ProductId = product.Id, Barcode = barcode });
        _context.SaveChanges();
        return product;
    }

    private ShoppingListItem AddToList(Product product, decimal amount = 1)
    {
        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ShoppingListId = _listId,
            ProductId = product.Id,
            ProductName = product.Name,
            Amount = amount
        };
        _context.ShoppingListItems.Add(item);
        _context.SaveChanges();
        return item;
    }

    private async Task<Product> CreateChildAsync()
    {
        var generic = AddGeneric();
        return await _catalog.CreateChildAsync(generic.Id, CansUpc, CansLabel);
    }

    private MoveToInventoryRequest MoveRequest(Guid itemId, Guid? productId) => new()
    {
        ShoppingListId = _listId,
        Items = [new MoveToInventoryItem { ShoppingListItemId = itemId, ProductId = productId, ProductName = "Cola", Amount = 1 }]
    };

    private void LookupReturns(params ProductLookupResult[] results) =>
        _lookup.Setup(l => l.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ProductSearchMode>(),
                It.IsAny<ProductLookupLocation?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results.ToList());

    private static List<ChildPurchaseEntry> Purchases(ShoppingListItem item) =>
        JsonSerializer.Deserialize<List<ChildPurchaseEntry>>(item.ChildPurchasesJson!)!;

    private static Barcode Parse(string barcode) =>
        BarcodeParser.TryParse(barcode, out var parsed) ? parsed! : throw new ArgumentException(barcode);
}
