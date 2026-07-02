using System.Collections.Generic;
using System.Linq;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Infrastructure.Services.Search;
using Famick.HomeManagement.Plugin.Abstractions;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Search;

public class ProductSearchMergerTests
{
    private const string Local = ProductSearchService.LocalProductsDataSource;
    private const string Master = ProductSearchService.MasterCatalogDataSource;
    private const string Store = "Kroger";

    private static Barcode Bc(string raw)
    {
        BarcodeParser.TryParse(raw, out var b).Should().BeTrue($"'{raw}' should parse");
        return b!;
    }

    private static ProductMergeSource Group(bool store, params ProductLookupResult[] results) =>
        new() { Results = results, IsStoreAuthoritative = store };

    [Fact]
    public void StoreFields_Win_OverEarlierSources_OnMergedRow()
    {
        var local = new ProductLookupResult
        {
            Name = "Milk",
            Categories = new List<string> { "Dairy" },
            DataSources = new Dictionary<string, string> { { Local, "L1" } }
        };
        var store = new ProductLookupResult
        {
            Name = "Milk",
            Price = 3.99m,
            Aisle = "5",
            InStock = true,
            DataSources = new Dictionary<string, string> { { Store, "K1" } }
        };

        var merged = ProductSearchMerger.Merge(new[] { Group(false, local), Group(true, store) });

        merged.Should().HaveCount(1);
        var row = merged[0];
        row.Price.Should().Be(3.99m);
        row.Aisle.Should().Be("5");
        row.InStock.Should().BeTrue();
        row.Categories.Should().Contain("Dairy");                 // identity kept from local
        row.DataSources.Keys.Should().Contain(new[] { Local, Store }); // provenance from both
    }

    [Fact]
    public void StoreImage_Wins_ButFallsBackToLocalImageWhenStoreHasNone()
    {
        var localWithImg = new ProductLookupResult
        {
            Name = "Eggs",
            ImageUrl = new ResultImage { ImageUrl = "local.png", PluginId = Local },
            DataSources = new Dictionary<string, string> { { Local, "L1" } }
        };
        var storeWithImg = new ProductLookupResult
        {
            Name = "Eggs",
            ImageUrl = new ResultImage { ImageUrl = "store.png", PluginId = Store },
            DataSources = new Dictionary<string, string> { { Store, "K1" } }
        };
        var storeNoImg = new ProductLookupResult
        {
            Name = "Eggs",
            DataSources = new Dictionary<string, string> { { Store, "K1" } }
        };

        var storeWins = ProductSearchMerger.Merge(new[] { Group(false, localWithImg), Group(true, storeWithImg) });
        storeWins[0].ImageUrl!.ImageUrl.Should().Be("store.png");

        var localKept = ProductSearchMerger.Merge(new[]
        {
            Group(false, new ProductLookupResult
            {
                Name = "Eggs",
                ImageUrl = new ResultImage { ImageUrl = "local.png", PluginId = Local },
                DataSources = new Dictionary<string, string> { { Local, "L1" } }
            }),
            Group(true, storeNoImg)
        });
        localKept[0].ImageUrl!.ImageUrl.Should().Be("local.png");
    }

    [Fact]
    public void DedupesByBarcode_AcrossMasterAndLocal()
    {
        var master = new ProductLookupResult
        {
            Name = "Master Cola",
            Barcodes = new List<Barcode> { Bc("0001110000015") },
            DataSources = new Dictionary<string, string> { { Master, "M1" } }
        };
        var local = new ProductLookupResult
        {
            Name = "My Cola",   // different name, same barcode
            Barcodes = new List<Barcode> { Bc("0001110000015") },
            DataSources = new Dictionary<string, string> { { Local, "L1" } }
        };

        var merged = ProductSearchMerger.Merge(new[] { Group(false, master), Group(false, local) });

        merged.Should().HaveCount(1);
        merged[0].DataSources.Keys.Should().Contain(new[] { Master, Local });
    }

    [Fact]
    public void DedupesByName_CaseInsensitive_WhenNoBarcode()
    {
        var master = new ProductLookupResult
        {
            Name = "Whole Milk",
            DataSources = new Dictionary<string, string> { { Master, "M1" } }
        };
        var local = new ProductLookupResult
        {
            Name = "whole milk",
            DataSources = new Dictionary<string, string> { { Local, "L1" } }
        };

        var merged = ProductSearchMerger.Merge(new[] { Group(false, master), Group(false, local) });

        merged.Should().HaveCount(1);
    }

    [Fact]
    public void DistinctProducts_AreNotMerged()
    {
        var a = new ProductLookupResult { Name = "Milk", DataSources = new() { { Local, "L1" } } };
        var b = new ProductLookupResult { Name = "Bread", DataSources = new() { { Local, "L2" } } };

        var merged = ProductSearchMerger.Merge(new[] { Group(false, a, b) });

        merged.Should().HaveCount(2);
    }
}
