using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// A catalog child barcode's whole journey against real Postgres, where the barcode matching and the
/// tenant and catalog query filters run as SQL: a shopping scan with no household copy of the generic
/// product, the move to inventory that copies it in and creates the child, then an inventory scan of
/// another of the catalog's child barcodes. The cases are covered one by one in <c>CatalogChildScanTests</c>.
/// </summary>
public class CatalogChildScanPostgresTests(PostgresContainerFixture fixture)
    : IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task A_catalog_child_barcode_goes_from_shopping_scan_to_a_stocked_child_product()
    {
        var tenantId = Guid.NewGuid();
        var cansUpc = NewUpc();
        var caseUpc = NewUpc();
        Guid masterId, listId;

        await using (var seed = ContextFor(null))
        {
            seed.Tenants.Add(new Tenant { Id = tenantId, Name = "Household" });
            var master = new MasterProduct { Id = Guid.NewGuid(), Name = $"Cola Soda {Guid.NewGuid():N}", Category = "Beverages" };
            masterId = master.Id;
            seed.MasterProducts.Add(master);
            seed.MasterProductBarcodes.AddRange(
                new MasterProductBarcode { Id = Guid.NewGuid(), MasterProductId = master.Id, Barcode = cansUpc, Note = "Cola, 12 cans", IsChildCandidate = true },
                new MasterProductBarcode { Id = Guid.NewGuid(), MasterProductId = master.Id, Barcode = caseUpc, Note = "Cola, 24 cans", IsChildCandidate = true });
            await seed.SaveChangesAsync();
        }

        await using (var db = ContextFor(tenantId))
        {
            var store = new ShoppingLocation { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Store" };
            var list = new ShoppingList { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Groceries", ShoppingLocationId = store.Id };
            listId = list.Id;
            db.ShoppingLocations.Add(store);
            db.ShoppingLists.Add(list);
            db.Locations.Add(new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pantry" });
            db.QuantityUnits.Add(new QuantityUnit { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Piece", NamePlural = "Pieces" });
            await db.SaveChangesAsync();
        }

        // Shopping: the household has no copy of the generic product, so the item stands for the catalog's.
        Guid pendingChildId, itemId;
        await using (var db = ContextFor(tenantId))
        {
            var (_, _, shopping) = Services(db, tenantId);
            var scan = await shopping.ScanToListAsync(listId, cansUpc);

            scan.ListUpdated.Should().BeTrue();
            pendingChildId = scan.ChildProductId!.Value;
            itemId = scan.ItemId!.Value;
            (await db.ShoppingListItems.SingleAsync(i => i.Id == itemId)).MasterProductId.Should().Be(masterId);
            (await db.Products.CountAsync()).Should().Be(0);
        }

        // Move to inventory: the generic product is copied in, then the child created under it.
        Guid parentId;
        await using (var db = ContextFor(tenantId))
        {
            var (_, _, shopping) = Services(db, tenantId);
            var response = await shopping.MoveToInventoryAsync(new MoveToInventoryRequest
            {
                ShoppingListId = listId,
                Items = [new MoveToInventoryItem { ShoppingListItemId = itemId, ProductName = "Cola", Amount = 1 }]
            });

            response.Errors.Should().BeEmpty();
            var parent = await db.Products.SingleAsync(p => p.MasterProductId == masterId);
            parentId = parent.Id;
            var child = await db.Products.SingleAsync(p => p.ParentProductId == parent.Id);
            child.Id.Should().Be(pendingChildId);
            (await db.ProductBarcodes.SingleAsync(b => b.Barcode == cansUpc)).ProductId.Should().Be(child.Id);
            (await db.ProductBarcodes.SingleAsync(b => b.Barcode == caseUpc)).IsCatalogChild.Should().BeTrue();
        }

        // Inventory: the catalog's other child barcode, now on the generic product, becomes a child of its
        // own, scanned here as its 13-digit EAN.
        await using (var db = ContextFor(tenantId))
        {
            var (_, products, _) = Services(db, tenantId);
            var first = await products.GetByBarcodeAsync("0" + caseUpc);
            var second = await products.GetByBarcodeAsync(caseUpc);

            first!.ParentProductId.Should().Be(parentId);
            first.Name.Should().Be("Cola, 24 cans");
            second!.Id.Should().Be(first.Id);
            (await db.Products.CountAsync(p => p.ParentProductId == parentId)).Should().Be(2);
        }
    }

    private HomeManagementDbContext ContextFor(Guid? tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(tenantId);
        return new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseNpgsql(fixture.ConnectionString).Options,
            tenantProvider.Object);
    }

    private static (CatalogChildProductService Catalog, ProductsService Products, ShoppingListService Shopping) Services(
        HomeManagementDbContext db, Guid tenantId)
    {
        var tenant = Mock.Of<ITenantProvider>(t => t.TenantId == tenantId);
        var factory = new Mock<IDbContextFactory<HomeManagementDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(db);
        var search = new ProductSearchService(db, factory.Object, Mock.Of<IFileUrlService>(), Mock.Of<IMasterProductImageResolver>(),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), tenant,
            NullLogger<ProductSearchService>.Instance);
        var catalog = new CatalogChildProductService(db, Mock.Of<IProductLookupService>(), search,
            Mock.Of<ICatalogChildEnrichmentRunner>(), tenant, NullLogger<CatalogChildProductService>.Instance);
        var products = new ProductsService(db, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            Mock.Of<IOutboundHttpFetcher>(), Mock.Of<IMasterProductImageResolver>(), search, catalog, tenant,
            NullLogger<ProductsService>.Instance);

        var stock = new Mock<IStockService>();
        stock.Setup(s => s.AddStockAsync(It.IsAny<AddStockRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new StockEntryDto { Id = Guid.NewGuid() });
        var shopping = new ShoppingListService(db, NullLogger<ShoppingListService>.Instance, Mock.Of<IStoreIntegrationService>(),
            Mock.Of<IPluginLoader>(), stock.Object, Mock.Of<ITodoItemService>(), products, Mock.Of<IFileUrlService>(), catalog);

        return (catalog, products, shopping);
    }

    /// <summary>
    /// A manufacturer's 12-digit UPC-A with a valid check digit, unique to this run. It leads with 0, as
    /// catalog barcodes do: one leading with 2 is an in-store weight or price code, which never resolves
    /// through the catalog.
    /// </summary>
    private static string NewUpc()
    {
        var digits = "0" + Random.Shared.NextInt64(1_000_000_000, 9_999_999_999);
        var sum = digits.Select((c, i) => (c - '0') * (i % 2 == 0 ? 3 : 1)).Sum();
        return digits + (10 - sum % 10) % 10;
    }
}
