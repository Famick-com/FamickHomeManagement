using System.Collections.Generic;
using System.Linq;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Infrastructure.Services.Search;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Search;

public class ProductSearchRankerTests
{
    private const string Local = ProductSearchService.LocalProductsDataSource;
    private const string Master = ProductSearchService.MasterCatalogDataSource;

    private static ProductLookupResult Result(string name, string sourceKey) => new()
    {
        Name = name,
        DataSources = new Dictionary<string, string> { { sourceKey, "x" } }
    };

    [Fact]
    public void ShoppingList_Ranks_Master_Then_Local_Then_Store()
    {
        var store = Result("Milk", "Kroger");
        var local = Result("Milk", Local);
        var master = Result("Milk", Master);

        // deliberately unsorted input
        var input = new List<ProductLookupResult> { store, local, master };

        var ranked = ProductSearchRanker.Rank(input, "milk", shoppingListContext: true, Local, Master);

        ranked.Select(r => r.DataSources.Keys.First())
            .Should().ContainInOrder(Master, Local, "Kroger");
    }

    [Fact]
    public void General_Ranks_By_Relevance_StartsWith_Before_Contains()
    {
        var contains = Result("Almond Milk", Local);
        var startsWith = Result("Milk", Local);
        var input = new List<ProductLookupResult> { contains, startsWith };

        var ranked = ProductSearchRanker.Rank(input, "mi", shoppingListContext: false, Local, Master);

        ranked.First().Name.Should().Be("Milk");
    }

    [Fact]
    public void General_IgnoresSourcePriority()
    {
        // A store-only exact match should outrank a master-only weak match in General context.
        var masterContains = Result("Almond Milk", Master);
        var storeExact = Result("Mi", "Kroger");
        var input = new List<ProductLookupResult> { masterContains, storeExact };

        var ranked = ProductSearchRanker.Rank(input, "mi", shoppingListContext: false, Local, Master);

        ranked.First().Name.Should().Be("Mi");
    }
}
