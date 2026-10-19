using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// A shared product enters the master catalog pending review, visible only to the
/// household that shared it, and a share never changes a master that already exists.
/// </summary>
public class ProductShareTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    private HomeManagementDbContext ContextFor(Guid? tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(tenantId);
        return new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(_databaseName).Options,
            tenantProvider.Object);
    }

    private static ProductsService Service(HomeManagementDbContext db, Guid tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(tenantId);
        return new ProductsService(
            db, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(), Mock.Of<IOutboundHttpFetcher>(),
            Mock.Of<IMasterProductImageResolver>(), Mock.Of<IProductSearchService>(),
            tenantProvider.Object, NullLogger<ProductsService>.Instance);
    }

    /// <summary>A product with a barcode, nutrition and an external image, in the "Dairy" group.</summary>
    private async Task<Guid> AddProductAsync(Guid tenantId, string name, string barcode)
    {
        await using var db = ContextFor(tenantId);
        var group = new ProductGroup { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Dairy" };
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Fridge" };
        var unit = new QuantityUnit { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Carton", NamePlural = "Cartons" };
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = name, Brand = "Acme", ProductGroupId = group.Id,
            LocationId = location.Id, QuantityUnitIdPurchase = unit.Id, QuantityUnitIdStock = unit.Id,
            IsActive = true, OverriddenFields = "[]"
        };
        db.ProductGroups.Add(group);
        db.Locations.Add(location);
        db.QuantityUnits.Add(unit);
        db.Products.Add(product);
        db.ProductBarcodes.Add(new ProductBarcode { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id, Barcode = barcode });
        db.ProductNutrition.Add(new ProductNutrition { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id, Calories = 150 });
        db.ProductImages.Add(new ProductImage
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id, FileName = "x", OriginalFileName = "x.png",
            ContentType = "image/png", ExternalUrl = "https://tracker.example.com/pixel.png", ExternalSource = "openfoodfacts"
        });
        await db.SaveChangesAsync();
        return product.Id;
    }

    private async Task<Guid> ShareAsAsync(Guid tenantId, Guid productId)
    {
        await using var db = ContextFor(tenantId);
        var dto = await Service(db, tenantId).ShareAsync(productId);
        return dto.MasterProductId!.Value;
    }

    [Fact]
    public async Task A_new_share_is_pending_and_carries_barcodes_and_nutrition_but_no_images()
    {
        var productId = await AddProductAsync(_tenantA, "Oat Milk", "012345678905");

        await using var db = ContextFor(_tenantA);
        var dto = await Service(db, _tenantA).ShareAsync(productId);

        dto.MasterProductPendingReview.Should().BeTrue();
        var master = await db.MasterProducts.IgnoreQueryFilters()
            .Include(m => m.Barcodes).Include(m => m.Nutrition).Include(m => m.Images)
            .SingleAsync();
        master.ReviewStatus.Should().Be(MasterProductReviewStatus.Pending);
        master.Source.Should().Be(MasterProductSource.TenantContributed);
        master.ContributedByTenantId.Should().Be(_tenantA);
        master.Barcodes.Select(b => b.Barcode).Should().Equal("012345678905");
        master.Nutrition.Should().NotBeNull();
        master.Images.Should().BeEmpty();
    }

    [Fact]
    public async Task Sharing_onto_an_existing_master_links_without_changing_it()
    {
        Guid masterId;
        await using (var seed = ContextFor(null))
        {
            var master = new MasterProduct { Id = Guid.NewGuid(), Name = "Oat Milk", Category = "Dairy", Brand = "Acme" };
            masterId = master.Id;
            seed.MasterProducts.Add(master);
            seed.MasterProductBarcodes.Add(new MasterProductBarcode { Id = Guid.NewGuid(), MasterProductId = master.Id, Barcode = "111111111117" });
            await seed.SaveChangesAsync();
        }
        var productId = await AddProductAsync(_tenantB, "Oat Milk", "999999999993");

        var linkedTo = await ShareAsAsync(_tenantB, productId);

        linkedTo.Should().Be(masterId);
        await using var db = ContextFor(null);
        (await db.MasterProductBarcodes.IgnoreQueryFilters().Select(b => b.Barcode).ToListAsync())
            .Should().Equal("111111111117");
        (await db.MasterProductNutrition.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
        (await db.MasterProductImages.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
        (await db.MasterProducts.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_pending_master_is_visible_only_to_the_household_that_shared_it()
    {
        var productId = await AddProductAsync(_tenantA, "Oat Milk", "012345678905");
        var masterId = await ShareAsAsync(_tenantA, productId);

        await using (var a = ContextFor(_tenantA))
        {
            (await a.MasterProducts.AnyAsync(m => m.Id == masterId)).Should().BeTrue();
            (await a.MasterProductBarcodes.AnyAsync()).Should().BeTrue();
        }

        await using (var b = ContextFor(_tenantB))
        {
            (await b.MasterProducts.AnyAsync()).Should().BeFalse();
            (await b.MasterProductBarcodes.AnyAsync()).Should().BeFalse();
            (await b.MasterProductNutrition.AnyAsync()).Should().BeFalse();
        }

        // With no household context (platform jobs, admin) only approved entries show
        await using (var none = ContextFor(null))
        {
            (await none.MasterProducts.AnyAsync()).Should().BeFalse();
            (await none.MasterProducts.IgnoreQueryFilters().AnyAsync()).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Another_household_cannot_create_a_product_from_a_pending_master()
    {
        var productId = await AddProductAsync(_tenantA, "Oat Milk", "012345678905");
        var masterId = await ShareAsAsync(_tenantA, productId);

        await using var b = ContextFor(_tenantB);
        var act = () => Service(b, _tenantB).EnsureProductFromMasterAsync(masterId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Once_approved_the_master_is_visible_to_every_household()
    {
        var productId = await AddProductAsync(_tenantA, "Oat Milk", "012345678905");
        var masterId = await ShareAsAsync(_tenantA, productId);

        await using (var admin = ContextFor(null))
        {
            var master = await admin.MasterProducts.IgnoreQueryFilters().SingleAsync(m => m.Id == masterId);
            master.ReviewStatus = MasterProductReviewStatus.Approved;
            await admin.SaveChangesAsync();
        }

        await using var b = ContextFor(_tenantB);
        (await b.MasterProducts.AnyAsync(m => m.Id == masterId)).Should().BeTrue();
        (await b.MasterProductBarcodes.Select(x => x.Barcode).ToListAsync()).Should().Equal("012345678905");
    }
}
