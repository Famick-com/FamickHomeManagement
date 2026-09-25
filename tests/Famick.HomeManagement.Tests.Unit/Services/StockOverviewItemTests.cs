using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Covers the stock-overview row aggregation: the whole-list <see cref="StockService.GetOverviewAsync"/>
/// and the single-row path used to patch one row after a mutation.
///
/// The whole-list assertions are deliberately characterization tests — they pin the aggregation's
/// existing output so that factoring the row builders out of GetOverviewAsync cannot change it.
/// GetOverviewAsync aggregates in LINQ-to-objects rather than in SQL, so the in-memory provider
/// exercises the real logic here.
/// </summary>
public class StockOverviewItemTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ServiceProvider _serviceProvider;

    // Fixture ids, so assertions can name what they mean.
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly Guid _pantryId = Guid.NewGuid();
    private readonly Guid _fridgeId = Guid.NewGuid();
    private readonly Guid _dairyGroupId = Guid.NewGuid();

    private readonly Guid _breadId = Guid.NewGuid();
    private readonly Guid _milkId = Guid.NewGuid();
    private readonly Guid _sodaParentId = Guid.NewGuid();
    private readonly Guid _sodaColaId = Guid.NewGuid();
    private readonly Guid _sodaLimeId = Guid.NewGuid();
    private readonly Guid _ghostId = Guid.NewGuid();
    private readonly Guid _emptyParentId = Guid.NewGuid();
    private readonly Guid _emptyChildId = Guid.NewGuid();

    private readonly DateTime _today = DateTime.UtcNow.Date;

    public StockOverviewItemTests()
    {
        // The name is generated once, outside the options lambda — EF invokes that lambda per
        // DbContext instance, so a Guid created inside it would give every scope its own database.
        var databaseName = $"stock-overview-{Guid.NewGuid()}";

        var services = new ServiceCollection();
        services.AddDbContext<HomeManagementDbContext>(opt => opt.UseInMemoryDatabase(databaseName));
        _serviceProvider = services.BuildServiceProvider();

        Seed();
    }

    public void Dispose() => _serviceProvider.Dispose();

    #region Fixture

    private void Seed()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();

        db.Tenants.Add(new Tenant { Id = TenantId, Name = "Test Home", TimeZoneId = "UTC" });
        db.QuantityUnits.Add(new QuantityUnit { Id = _unitId, TenantId = TenantId, Name = "Piece", NamePlural = "Pieces" });
        db.Locations.Add(new Location { Id = _pantryId, TenantId = TenantId, Name = "Pantry" });
        db.Locations.Add(new Location { Id = _fridgeId, TenantId = TenantId, Name = "Fridge" });
        db.ProductGroups.Add(new ProductGroup { Id = _dairyGroupId, TenantId = TenantId, Name = "Dairy" });

        // Bread — one expired entry in the pantry. No product group.
        db.Products.Add(NewProduct(_breadId, "Bread", locationId: _pantryId));
        db.Stock.Add(NewEntry(_breadId, amount: 1, bestBefore: _today.AddDays(-1), locationId: _pantryId, price: 3m));

        // Milk — below its min stock, earliest entry due soon. In the Dairy group, in the fridge.
        db.Products.Add(NewProduct(_milkId, "Milk", locationId: _fridgeId, minStock: 2m, groupId: _dairyGroupId));
        db.Stock.Add(NewEntry(_milkId, amount: 1, bestBefore: _today.AddDays(2), locationId: _fridgeId, price: 4m));

        // Soda — a parent product whose stock lives entirely in two child variants.
        db.Products.Add(NewProduct(_sodaParentId, "Soda", locationId: _pantryId, minStock: 5m));
        db.Products.Add(NewProduct(_sodaColaId, "Soda Cola", locationId: _pantryId, parentId: _sodaParentId));
        db.Products.Add(NewProduct(_sodaLimeId, "Soda Lime", locationId: _fridgeId, parentId: _sodaParentId));
        db.Stock.Add(NewEntry(_sodaColaId, amount: 2, bestBefore: _today.AddDays(3), locationId: _pantryId, price: 1m));
        db.Stock.Add(NewEntry(_sodaLimeId, amount: 1, bestBefore: _today.AddDays(8), locationId: _fridgeId, price: 2m));

        // Ghost — a product with no stock at all. Must never produce a row.
        db.Products.Add(NewProduct(_ghostId, "Ghost", locationId: _pantryId));

        // A parent whose children have no stock. Must never produce a row either.
        db.Products.Add(NewProduct(_emptyParentId, "Empty Parent", locationId: _pantryId));
        db.Products.Add(NewProduct(_emptyChildId, "Empty Child", locationId: _pantryId, parentId: _emptyParentId));

        db.SaveChanges();
    }

    private Product NewProduct(
        Guid id, string name, Guid locationId,
        decimal minStock = 0m, Guid? groupId = null, Guid? parentId = null) => new()
        {
            Id = id,
            TenantId = TenantId,
            Name = name,
            LocationId = locationId,
            QuantityUnitIdPurchase = _unitId,
            QuantityUnitIdStock = _unitId,
            MinStockAmount = minStock,
            ProductGroupId = groupId,
            ParentProductId = parentId,
            TracksBestBeforeDate = true,
            DefaultBestBeforeDays = 7,
            IsActive = true
        };

    private StockEntry NewEntry(
        Guid productId, decimal amount, DateTime? bestBefore, Guid locationId, decimal? price = null) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ProductId = productId,
            Amount = amount,
            BestBeforeDate = bestBefore,
            PurchasedDate = _today.AddDays(-30),
            LocationId = locationId,
            Price = price,
            StockId = Guid.NewGuid().ToString()
        };

    private (StockService Service, IServiceScope Scope) CreateService()
    {
        var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(TenantId);
        tenantProvider.SetupGet(t => t.UserId).Returns(Guid.NewGuid());

        // The overview only ever asks for an image URL; a stable fake keeps assertions readable.
        var fileUrls = new Mock<IFileUrlService>();
        fileUrls
            .Setup(f => f.GetProductImageUrl(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((Guid productId, Guid imageId, Guid tenantId, string? extThumb, string? ext, string? file)
                => $"/files/{productId}/{imageId}");

        return (new StockService(db, tenantProvider.Object, fileUrls.Object), scope);
    }

    #endregion

    #region GetOverviewAsync — characterization

    [Fact]
    public async Task GetOverview_ReturnsOneRowPerStockedProduct_SortedByDueDateThenName()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var rows = await service.GetOverviewAsync();

            rows.Select(r => r.ProductName).Should().Equal("Bread", "Milk", "Soda");
        }
    }

    [Fact]
    public async Task GetOverview_OmitsProductsWithNoStock()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var rows = await service.GetOverviewAsync();

            rows.Should().NotContain(r => r.ProductId == _ghostId, "a product with no stock entries has no row");
            rows.Should().NotContain(r => r.ProductId == _emptyParentId, "a parent whose children hold no stock has no row");
            rows.Should().NotContain(r => r.ProductId == _emptyChildId);
        }
    }

    [Fact]
    public async Task GetOverview_StandaloneRow_CarriesAggregatesAndFlags()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var rows = await service.GetOverviewAsync();

            var milk = rows.Single(r => r.ProductId == _milkId);
            milk.TotalAmount.Should().Be(1m);
            milk.QuantityUnitName.Should().Be("Piece");
            milk.ProductGroupName.Should().Be("Dairy");
            milk.MinStockAmount.Should().Be(2m);
            milk.IsBelowMinStock.Should().BeTrue("1 in stock is under the minimum of 2");
            milk.IsDueSoon.Should().BeTrue("its only entry expires in 2 days");
            milk.IsExpired.Should().BeFalse();
            milk.NextDueDate.Should().Be(_today.AddDays(2));
            milk.DaysUntilDue.Should().Be(2);
            milk.TotalValue.Should().Be(4m);
            milk.StockEntryCount.Should().Be(1);
            milk.IsParentProduct.Should().BeFalse();
            milk.ChildProducts.Should().BeNull();
            milk.TracksBestBeforeDate.Should().BeTrue();
            milk.DefaultBestBeforeDays.Should().Be(7);
        }
    }

    [Fact]
    public async Task GetOverview_ExpiredRow_IsExpiredAndNotDueSoon()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var rows = await service.GetOverviewAsync();

            var bread = rows.Single(r => r.ProductId == _breadId);
            bread.IsExpired.Should().BeTrue();
            bread.IsDueSoon.Should().BeFalse("an expired row is never also reported as due soon");
            bread.DaysUntilDue.Should().Be(-1);
            bread.ProductGroupName.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetOverview_ParentRow_AggregatesChildrenAndListsThemByDueDate()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var rows = await service.GetOverviewAsync();

            var soda = rows.Single(r => r.ProductId == _sodaParentId);
            soda.IsParentProduct.Should().BeTrue();
            soda.TotalAmount.Should().Be(3m, "2 cola + 1 lime");
            soda.TotalValue.Should().Be(4m, "2x1 + 1x2");
            soda.StockEntryCount.Should().Be(2);
            soda.MinStockAmount.Should().Be(5m);
            soda.IsBelowMinStock.Should().BeTrue();
            soda.NextDueDate.Should().Be(_today.AddDays(3), "the earliest child entry wins");
            soda.ChildProductCount.Should().Be(2);

            soda.ChildProducts.Should().NotBeNull();
            soda.ChildProducts!.Select(c => c.ProductName).Should().Equal("Soda Cola", "Soda Lime");
            soda.ChildProducts.Select(c => c.TotalAmount).Should().Equal(2m, 1m);

            // The children themselves are folded in and never appear as rows of their own.
            rows.Should().NotContain(r => r.ProductId == _sodaColaId);
            rows.Should().NotContain(r => r.ProductId == _sodaLimeId);
        }
    }

    // Soda is due soon as well as Milk — its earliest child entry is 3 days out.
    [Theory]
    [InlineData("expired", "Bread")]
    [InlineData("duesoon", "Milk,Soda")]
    [InlineData("belowminstock", "Milk,Soda")]
    public async Task GetOverview_StatusFilter_KeepsOnlyMatchingRows(string status, string expected)
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var rows = await service.GetOverviewAsync(new StockOverviewFilterRequest { Status = status });

            rows.Select(r => r.ProductName).Should().Equal(expected.Split(','));
        }
    }

    [Fact]
    public async Task GetOverview_LocationFilter_AggregatesOnlyThatLocationsEntries()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var rows = await service.GetOverviewAsync(new StockOverviewFilterRequest { LocationId = _fridgeId });

            // Bread is pantry-only, so it drops out entirely. Soda keeps a row, but only its
            // lime child is in the fridge — which is what makes the filter observable on a
            // parent row.
            rows.Select(r => r.ProductName).Should().Equal("Milk", "Soda");

            var soda = rows.Single(r => r.ProductId == _sodaParentId);
            soda.TotalAmount.Should().Be(1m, "only the lime child is in the fridge");
            soda.ChildProducts!.Select(c => c.ProductName).Should().Equal("Soda Lime");
        }
    }

    [Fact]
    public async Task GetOverview_SortByAmountDescending_OrdersByTotalAmount()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var rows = await service.GetOverviewAsync(
                new StockOverviewFilterRequest { SortBy = "amount", Descending = true });

            rows.Select(r => r.ProductName).Should().Equal("Soda", "Bread", "Milk");
        }
    }

    #endregion

    #region GetOverviewItemAsync — the single row used to patch after a mutation

    [Fact]
    public async Task GetOverviewItem_ForStandaloneProduct_MatchesTheRowFromTheFullList()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            // The real invariant: patching a row must leave the client with what a reload shows.
            var fromList = (await service.GetOverviewAsync()).Single(r => r.ProductId == _milkId);
            var single = await service.GetOverviewItemAsync(_milkId);

            single.Should().NotBeNull();
            single!.ProductId.Should().Be(fromList.ProductId);
            single.ProductName.Should().Be(fromList.ProductName);
            single.TotalAmount.Should().Be(fromList.TotalAmount);
            single.QuantityUnitName.Should().Be(fromList.QuantityUnitName);
            single.ProductGroupName.Should().Be(fromList.ProductGroupName);
            single.NextDueDate.Should().Be(fromList.NextDueDate);
            single.DaysUntilDue.Should().Be(fromList.DaysUntilDue);
            single.TotalValue.Should().Be(fromList.TotalValue);
            single.MinStockAmount.Should().Be(fromList.MinStockAmount);
            single.IsBelowMinStock.Should().Be(fromList.IsBelowMinStock);
            single.IsExpired.Should().Be(fromList.IsExpired);
            single.IsDueSoon.Should().Be(fromList.IsDueSoon);
            single.StockEntryCount.Should().Be(fromList.StockEntryCount);
            single.IsParentProduct.Should().Be(fromList.IsParentProduct);
            single.TracksBestBeforeDate.Should().Be(fromList.TracksBestBeforeDate);
            single.DefaultBestBeforeDays.Should().Be(fromList.DefaultBestBeforeDays);
        }
    }

    [Fact]
    public async Task GetOverviewItem_ForParentProduct_MatchesTheRowFromTheFullList()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var fromList = (await service.GetOverviewAsync()).Single(r => r.ProductId == _sodaParentId);
            var single = await service.GetOverviewItemAsync(_sodaParentId);

            single.Should().NotBeNull();
            single!.IsParentProduct.Should().BeTrue();
            single.TotalAmount.Should().Be(fromList.TotalAmount);
            single.TotalValue.Should().Be(fromList.TotalValue);
            single.NextDueDate.Should().Be(fromList.NextDueDate);
            single.StockEntryCount.Should().Be(fromList.StockEntryCount);
            single.ChildProductCount.Should().Be(fromList.ChildProductCount);
            single.ChildProducts!.Select(c => c.ProductName)
                .Should().Equal(fromList.ChildProducts!.Select(c => c.ProductName));
        }
    }

    [Fact]
    public async Task GetOverviewItem_ForAChildProduct_ReturnsTheParentsRow()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            // A child variant has no row of its own — acting on one must patch the parent's row.
            var single = await service.GetOverviewItemAsync(_sodaColaId);

            single.Should().NotBeNull();
            single!.ProductId.Should().Be(_sodaParentId, "the row belongs to the parent, not the child");
            single.ProductName.Should().Be("Soda");
            single.IsParentProduct.Should().BeTrue();
            single.TotalAmount.Should().Be(3m, "the parent row still aggregates both children");
        }
    }

    [Fact]
    public async Task GetOverviewItem_ForProductWithNoStock_ReturnsNull()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            (await service.GetOverviewItemAsync(_ghostId)).Should().BeNull();
        }
    }

    [Fact]
    public async Task GetOverviewItem_ForParentWhoseChildrenHoldNoStock_ReturnsNull()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            (await service.GetOverviewItemAsync(_emptyParentId)).Should().BeNull();
            (await service.GetOverviewItemAsync(_emptyChildId)).Should().BeNull();
        }
    }

    [Fact]
    public async Task GetOverviewItem_ForUnknownProduct_ReturnsNull()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            (await service.GetOverviewItemAsync(Guid.NewGuid())).Should().BeNull();
        }
    }

    [Fact]
    public async Task GetOverviewItem_HonoursTheLocationFilter()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            // Only the lime child sits in the fridge, so a fridge-filtered row must not count cola.
            var single = await service.GetOverviewItemAsync(
                _sodaParentId, new StockOverviewFilterRequest { LocationId = _fridgeId });

            single.Should().NotBeNull();
            single!.TotalAmount.Should().Be(1m);
            single.ChildProducts!.Select(c => c.ProductName).Should().Equal("Soda Lime");
        }
    }

    [Fact]
    public async Task GetOverviewItem_WhenTheFilterExcludesEveryEntry_ReturnsNull()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            // Bread is pantry-only, so under a fridge filter it has no row — same as the list.
            (await service.GetOverviewItemAsync(
                _breadId, new StockOverviewFilterRequest { LocationId = _fridgeId })).Should().BeNull();
        }
    }

    #endregion

    #region Mutations answer with the row to patch

    [Fact]
    public async Task QuickConsume_PartialAmount_ReturnsRowWithTheReducedTotal()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var row = await service.QuickConsumeAsync(new QuickConsumeRequest
            {
                ProductId = _sodaColaId,
                Amount = 1m
            });

            // Acting on a child answers with the parent's row, down from 3 to 2.
            row.Should().NotBeNull();
            row!.ProductId.Should().Be(_sodaParentId);
            row.TotalAmount.Should().Be(2m);
        }
    }

    [Fact]
    public async Task QuickConsume_LastUnitOfAStandaloneProduct_ReturnsNullSoTheRowIsDropped()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var row = await service.QuickConsumeAsync(new QuickConsumeRequest
            {
                ProductId = _milkId,
                ConsumeAll = true
            });

            row.Should().BeNull("the product holds no stock, so it has no overview row");
        }
    }

    [Fact]
    public async Task QuickConsume_EmptyingOneChild_KeepsTheParentRowWithoutThatChild()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var row = await service.QuickConsumeAsync(new QuickConsumeRequest
            {
                ProductId = _sodaColaId,
                ConsumeAll = true
            });

            row.Should().NotBeNull("the lime child still holds stock, so the parent row survives");
            row!.ProductId.Should().Be(_sodaParentId);
            row.TotalAmount.Should().Be(1m);
            row.ChildProductCount.Should().Be(1);
            row.ChildProducts!.Select(c => c.ProductName).Should().Equal("Soda Lime");
        }
    }

    [Fact]
    public async Task QuickAdd_ReturnsRowWithTheIncreasedTotal()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var row = await service.QuickAddAsync(_milkId, amount: 2m);

            row.Should().NotBeNull();
            row!.ProductId.Should().Be(_milkId);
            row.TotalAmount.Should().Be(3m);
            row.IsBelowMinStock.Should().BeFalse("3 in stock clears the minimum of 2");
        }
    }

    [Fact]
    public async Task ConsumeStock_ReturnsRowForTheEntrysProduct()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var entries = await service.GetByProductAsync(_sodaColaId);
            var entryId = entries.Single().Id;

            var row = await service.ConsumeStockAsync(entryId, new ConsumeStockRequest { Amount = 1m });

            row.Should().NotBeNull();
            row!.ProductId.Should().Be(_sodaParentId, "the entry's product is a child, so the parent row comes back");
            row.TotalAmount.Should().Be(2m);
        }
    }

    [Fact]
    public async Task Delete_OfTheOnlyEntry_ReturnsNullSoTheRowIsDropped()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            var entries = await service.GetByProductAsync(_breadId);
            var entryId = entries.Single().Id;

            var row = await service.DeleteAsync(entryId);

            row.Should().BeNull();
        }
    }

    #endregion

    #region The statistics delta agrees with a server recount

    [Fact]
    public async Task PatchingStatisticsLocally_LandsOnTheSameNumbersAsARecount()
    {
        var (service, scope) = CreateService();
        using (scope)
        {
            // Start from the server's numbers, then do what the page does: for each mutation, keep
            // the row's before/after and move the statistics by the difference. If this drifts, the
            // header silently disagrees with the list and no server-side test would notice.
            var patched = await service.GetStatisticsAsync();

            await MutateAndPatch(service, patched, _milkId,
                () => service.QuickConsumeAsync(new QuickConsumeRequest { ProductId = _milkId, ConsumeAll = true }));

            await MutateAndPatch(service, patched, _sodaColaId,
                () => service.QuickConsumeAsync(new QuickConsumeRequest { ProductId = _sodaColaId, ConsumeAll = true }));

            await MutateAndPatch(service, patched, _breadId,
                () => service.QuickAddAsync(_breadId, amount: 2m));

            await MutateAndPatch(service, patched, _ghostId,
                () => service.QuickAddAsync(_ghostId, amount: 1m));

            var recounted = await service.GetStatisticsAsync();

            patched.TotalProductCount.Should().Be(recounted.TotalProductCount);
            patched.TotalStockValue.Should().Be(recounted.TotalStockValue);
            patched.ExpiredCount.Should().Be(recounted.ExpiredCount);
            patched.DueSoonCount.Should().Be(recounted.DueSoonCount);
            patched.BelowMinStockCount.Should().Be(recounted.BelowMinStockCount);
        }
    }

    /// <summary>
    /// Captures the owning row before a mutation, runs it, and folds the difference into
    /// <paramref name="statistics"/> exactly as the stock overview page does.
    /// </summary>
    private static async Task MutateAndPatch(
        StockService service,
        StockStatisticsDto statistics,
        Guid productId,
        Func<Task<StockOverviewItemDto?>> mutate)
    {
        var before = await service.GetOverviewItemAsync(productId);
        var after = await mutate();

        StockOverviewPatching.ApplyRowDelta(statistics, before, after);
    }

    #endregion
}
