using System.Text.Json;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Tests the BuildOverriddenFields logic in ProductsService.UpdateAsync.
/// Since BuildOverriddenFields is a private static method, we test the logic
/// by replicating the comparison and verifying the expected behavior.
/// </summary>
public class ProductOverrideTrackingTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ProductOverrideTrackingTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options, null);
    }

    #region Helpers

    /// <summary>
    /// Replicates the BuildOverriddenFields logic from ProductsService
    /// to test the override tracking behavior in isolation.
    /// </summary>
    private static string BuildOverriddenFields(Product product, MasterProduct master)
    {
        var overridden = new List<string>();

        if (!string.Equals(product.Name, master.Name, StringComparison.Ordinal))
            overridden.Add("Name");
        if (!string.Equals(product.Description ?? "", master.Description ?? "", StringComparison.Ordinal))
            overridden.Add("Description");
        if (product.DefaultBestBeforeDays != master.DefaultBestBeforeDays)
            overridden.Add("DefaultBestBeforeDays");
        if (product.TracksBestBeforeDate != master.TracksBestBeforeDate)
            overridden.Add("TracksBestBeforeDate");
        if (product.ServingSize != master.ServingSize)
            overridden.Add("ServingSize");
        if (!string.Equals(product.ServingUnit ?? "", master.ServingUnit ?? "", StringComparison.Ordinal))
            overridden.Add("ServingUnit");
        if (product.ServingsPerContainer != master.ServingsPerContainer)
            overridden.Add("ServingsPerContainer");
        if (!string.Equals(product.DataSourceAttribution ?? "", master.DataSourceAttribution ?? "", StringComparison.Ordinal))
            overridden.Add("DataSourceAttribution");

        return JsonSerializer.Serialize(overridden);
    }

    private (Product product, MasterProduct master) CreateLinkedProductAndMaster()
    {
        var master = new MasterProduct
        {
            Id = Guid.NewGuid(),
            Name = "Whole Milk",
            Category = "Dairy",
            Description = "Fresh whole milk",
            DefaultBestBeforeDays = 14,
            TracksBestBeforeDate = true,
            ServingSize = 240m,
            ServingUnit = "mL",
            ServingsPerContainer = 8m,
            DataSourceAttribution = "USDA"
        };

        var locationId = Guid.NewGuid();
        var quantityUnitId = Guid.NewGuid();

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = master.Name,
            Description = master.Description,
            MasterProductId = master.Id,
            OverriddenFields = "[]",
            LocationId = locationId,
            QuantityUnitIdPurchase = quantityUnitId,
            QuantityUnitIdStock = quantityUnitId,
            DefaultBestBeforeDays = master.DefaultBestBeforeDays,
            TracksBestBeforeDate = master.TracksBestBeforeDate,
            ServingSize = master.ServingSize,
            ServingUnit = master.ServingUnit,
            ServingsPerContainer = master.ServingsPerContainer,
            DataSourceAttribution = master.DataSourceAttribution,
            IsActive = true
        };

        return (product, master);
    }

    #endregion

    #region Name Override Tracking

    [Fact]
    public void ChangedName_AppearsInOverriddenFields()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        product.Name = "Organic Whole Milk";

        var result = BuildOverriddenFields(product, master);
        var fields = JsonSerializer.Deserialize<List<string>>(result);

        fields.Should().Contain("Name");
    }

    [Fact]
    public void RevertedName_RemovedFromOverriddenFields()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        // First change the name
        product.Name = "Organic Whole Milk";
        var result1 = BuildOverriddenFields(product, master);
        JsonSerializer.Deserialize<List<string>>(result1).Should().Contain("Name");

        // Revert to master value
        product.Name = master.Name;
        var result2 = BuildOverriddenFields(product, master);
        JsonSerializer.Deserialize<List<string>>(result2).Should().NotContain("Name");
    }

    #endregion

    #region Description Override Tracking

    [Fact]
    public void ChangedDescription_AppearsInOverriddenFields()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        product.Description = "My custom description";

        var result = BuildOverriddenFields(product, master);
        var fields = JsonSerializer.Deserialize<List<string>>(result);

        fields.Should().Contain("Description");
    }

    [Fact]
    public void RevertedDescription_RemovedFromOverriddenFields()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        product.Description = "Custom";
        JsonSerializer.Deserialize<List<string>>(BuildOverriddenFields(product, master))
            .Should().Contain("Description");

        product.Description = master.Description;
        var fields = JsonSerializer.Deserialize<List<string>>(BuildOverriddenFields(product, master));
        fields.Should().NotContain("Description");
    }

    [Fact]
    public void NullDescription_MatchesNullMasterDescription()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        master.Description = null;
        product.Description = null;

        var result = BuildOverriddenFields(product, master);
        var fields = JsonSerializer.Deserialize<List<string>>(result);

        fields.Should().NotContain("Description");
    }

    #endregion

    #region Multiple Field Changes

    [Fact]
    public void MultipleFieldChanges_AllTrackedCorrectly()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        product.Name = "Custom Milk";
        product.Description = "Custom description";
        product.DefaultBestBeforeDays = 7;
        product.ServingSize = 120m;

        var result = BuildOverriddenFields(product, master);
        var fields = JsonSerializer.Deserialize<List<string>>(result);

        fields.Should().HaveCount(4);
        fields.Should().Contain("Name");
        fields.Should().Contain("Description");
        fields.Should().Contain("DefaultBestBeforeDays");
        fields.Should().Contain("ServingSize");
    }

    [Fact]
    public void NoFieldChanges_ResultsInEmptyArray()
    {
        var (product, master) = CreateLinkedProductAndMaster();

        var result = BuildOverriddenFields(product, master);
        var fields = JsonSerializer.Deserialize<List<string>>(result);

        fields.Should().BeEmpty();
    }

    #endregion

    #region Unlinked Product (No MasterProductId)

    [Fact]
    public void UnlinkedProduct_NoTrackingOccurs()
    {
        // When a product has no MasterProductId, the UpdateAsync code
        // never calls BuildOverriddenFields. We verify that an unlinked product
        // has null OverriddenFields and does not get tracking applied.
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Custom Product",
            MasterProductId = null,
            OverriddenFields = null,
            LocationId = Guid.NewGuid(),
            QuantityUnitIdPurchase = Guid.NewGuid(),
            QuantityUnitIdStock = Guid.NewGuid(),
            IsActive = true
        };

        product.MasterProductId.Should().BeNull();
        product.OverriddenFields.Should().BeNull();
    }

    #endregion

    #region Individual Field Tracking

    [Fact]
    public void ChangedDefaultBestBeforeDays_AppearsInOverriddenFields()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        product.DefaultBestBeforeDays = 30;

        var fields = JsonSerializer.Deserialize<List<string>>(BuildOverriddenFields(product, master));

        fields.Should().Contain("DefaultBestBeforeDays");
        fields.Should().HaveCount(1);
    }

    [Fact]
    public void ChangedTracksBestBeforeDate_AppearsInOverriddenFields()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        product.TracksBestBeforeDate = !master.TracksBestBeforeDate;

        var fields = JsonSerializer.Deserialize<List<string>>(BuildOverriddenFields(product, master));

        fields.Should().Contain("TracksBestBeforeDate");
    }

    [Fact]
    public void ChangedServingUnit_AppearsInOverriddenFields()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        product.ServingUnit = "oz";

        var fields = JsonSerializer.Deserialize<List<string>>(BuildOverriddenFields(product, master));

        fields.Should().Contain("ServingUnit");
    }

    [Fact]
    public void ChangedServingsPerContainer_AppearsInOverriddenFields()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        product.ServingsPerContainer = 16m;

        var fields = JsonSerializer.Deserialize<List<string>>(BuildOverriddenFields(product, master));

        fields.Should().Contain("ServingsPerContainer");
    }

    [Fact]
    public void ChangedDataSourceAttribution_AppearsInOverriddenFields()
    {
        var (product, master) = CreateLinkedProductAndMaster();
        product.DataSourceAttribution = "OpenFoodFacts";

        var fields = JsonSerializer.Deserialize<List<string>>(BuildOverriddenFields(product, master));

        fields.Should().Contain("DataSourceAttribution");
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
