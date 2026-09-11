using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Pins the resolution semantics of <c>GetByBarcodeAsync</c>'s four-phase matcher.
/// </summary>
/// <remarks>
/// These exist because the lookup was reworked for performance (FHM-57): the normalized
/// phase now projects candidates to two columns and hydrates only the winner, and the
/// stock summary is scoped to the resolved product instead of aggregating the tenant's
/// whole Stock table. Neither is supposed to change which product a barcode resolves to,
/// and these tests are what says so.
/// </remarks>
public class ProductSearchServiceBarcodeLookupTests
{
    private readonly MemoryDistributedCache _cache = new(
        Options.Create(new MemoryDistributedCacheOptions()));

    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IFileUrlService> _fileUrlService = new();
    private readonly Mock<IMasterProductImageResolver> _imageResolver = new();

    public ProductSearchServiceBarcodeLookupTests()
    {
        _tenantProvider.Setup(t => t.TenantId)
            .Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    }

    [Fact]
    public async Task Resolves_an_exact_barcode_match()
    {
        var context = CreateContext();
        var product = SeedProduct(context, "Tinned Tomatoes", "012345678905");
        await context.SaveChangesAsync();

        var dto = await CreateService(context).GetByBarcodeAsync("012345678905");

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(product.Id);
        dto.Name.Should().Be("Tinned Tomatoes");
    }

    [Fact]
    public async Task Resolves_a_13_digit_ean_scan_against_a_stored_12_digit_upca()
    {
        // The normalized phase — the one whose query shape changed. A US UPC-A stored as
        // 12 digits must still resolve when scanned as its 13-digit EAN-13 rendering.
        var context = CreateContext();
        var product = SeedProduct(context, "Oat Milk", "012345678905");
        await context.SaveChangesAsync();

        var dto = await CreateService(context).GetByBarcodeAsync("0012345678905");

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task Picks_the_parse_equal_row_not_merely_a_substring_neighbour()
    {
        // The candidate query is a substring match, so an unrelated barcode that happens
        // to contain the same digits gets read. Only the parse-equal row may win.
        var context = CreateContext();
        SeedProduct(context, "Decoy — contains the digits but is a different code", "9901234567890599");
        var real = SeedProduct(context, "Oat Milk", "012345678905");
        await context.SaveChangesAsync();

        var dto = await CreateService(context).GetByBarcodeAsync("0012345678905");

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(real.Id, "the substring neighbour does not parse equal to the scanned code");
    }

    [Fact]
    public async Task Returns_null_when_nothing_matches()
    {
        var context = CreateContext();
        SeedProduct(context, "Oat Milk", "012345678905");
        await context.SaveChangesAsync();

        var dto = await CreateService(context).GetByBarcodeAsync("5000112637922");

        dto.Should().BeNull();
    }

    [Fact]
    public async Task Hydrates_the_resolved_product_not_a_bare_row()
    {
        // The winner is now fetched in a second query. Guard against that regressing into
        // a projection that leaves the caller without the product it asked for.
        var context = CreateContext();
        var product = SeedProduct(context, "Oat Milk", "012345678905");
        await context.SaveChangesAsync();

        var dto = await CreateService(context).GetByBarcodeAsync("0012345678905");

        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Oat Milk");
        dto.Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task Sums_stock_for_the_resolved_product_only()
    {
        // The stock summary used to group the tenant's entire Stock table and index into
        // the result. Now it is scoped to the resolved product — another product's stock
        // must not leak into the total.
        var context = CreateContext();
        var wanted = SeedProduct(context, "Oat Milk", "012345678905");
        var other = SeedProduct(context, "Something Else", "036000291452");

        var locationId = wanted.LocationId;

        context.Stock.AddRange(
            new StockEntry { Id = Guid.NewGuid(), ProductId = wanted.Id, LocationId = locationId, Amount = 2m },
            new StockEntry { Id = Guid.NewGuid(), ProductId = wanted.Id, LocationId = locationId, Amount = 3m },
            new StockEntry { Id = Guid.NewGuid(), ProductId = other.Id, LocationId = locationId, Amount = 99m });
        await context.SaveChangesAsync();

        var dto = await CreateService(context).GetByBarcodeAsync("012345678905");

        dto.Should().NotBeNull();
        dto!.TotalStockAmount.Should().Be(5m, "only the resolved product's entries count");
        dto.StockByLocation.Should().ContainSingle();
        dto.StockByLocation[0].Amount.Should().Be(5m);
        dto.StockByLocation[0].EntryCount.Should().Be(2);

        // The location name comes from a join in the grouping projection rather than an
        // Include; this is what catches that join being dropped.
        dto.StockByLocation[0].LocationId.Should().Be(locationId);
        dto.StockByLocation[0].LocationName.Should().Be("Pantry");
    }

    [Fact]
    public async Task Leaves_stock_empty_when_the_product_has_none()
    {
        var context = CreateContext();
        SeedProduct(context, "Oat Milk", "012345678905");
        var other = SeedProduct(context, "Something Else", "036000291452");

        context.Stock.Add(new StockEntry
        {
            Id = Guid.NewGuid(),
            ProductId = other.Id,
            LocationId = Guid.NewGuid(),
            Amount = 99m
        });
        await context.SaveChangesAsync();

        var dto = await CreateService(context).GetByBarcodeAsync("012345678905");

        dto.Should().NotBeNull();
        dto!.TotalStockAmount.Should().Be(0m);
        dto.StockByLocation.Should().BeEmpty();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static HomeManagementDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HomeManagementDbContext(options, null);
    }

    /// <summary>
    /// Seeds a product and one barcode for it.
    /// </summary>
    /// <remarks>
    /// Location and both quantity units are required navigations on Product, so the
    /// lookup's include chain inner-joins them. A product whose foreign keys point at
    /// rows that do not exist is invisible to the query — so they are seeded here rather
    /// than left at Guid.Empty.
    /// </remarks>
    private static Product SeedProduct(HomeManagementDbContext context, string name, string barcode)
    {
        var location = new Location { Id = Guid.NewGuid(), Name = "Pantry" };
        var unit = new QuantityUnit { Id = Guid.NewGuid(), Name = "Each", NamePlural = "Each" };

        context.Locations.Add(location);
        context.QuantityUnits.Add(unit);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            LocationId = location.Id,
            QuantityUnitIdPurchase = unit.Id,
            QuantityUnitIdStock = unit.Id
        };

        context.Products.Add(product);
        context.ProductBarcodes.Add(new ProductBarcode
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Barcode = barcode
        });

        return product;
    }

    private ProductSearchService CreateService(HomeManagementDbContext context)
    {
        var contextFactory = new Mock<IDbContextFactory<HomeManagementDbContext>>();
        contextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        return new ProductSearchService(
            context,
            contextFactory.Object,
            _fileUrlService.Object,
            _imageResolver.Object,
            _cache,
            _tenantProvider.Object,
            Mock.Of<ILogger<ProductSearchService>>());
    }
}
