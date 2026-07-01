using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;

namespace Famick.HomeManagement.Infrastructure.Services.Search;

/// <summary>
/// Orders merged product results for a given search context.
///
///   • ShoppingList — bucket by source priority (master → local → store), then relevance
///     within each bucket. A row that merged master + store stays in the master bucket but
///     still carries the store's price.
///   • General — pure relevance (exact &gt; starts-with &gt; contains &gt; other), then name.
///
/// Stable: results with equal rank keep their incoming (merge) order.
/// </summary>
public static class ProductSearchRanker
{
    public static List<ProductLookupResult> Rank(
        List<ProductLookupResult> results,
        string query,
        bool shoppingListContext,
        string localDataSource,
        string masterDataSource)
    {
        var normalizedQuery = (query ?? string.Empty).Trim().ToLowerInvariant();

        IEnumerable<(ProductLookupResult Result, int Index)> indexed =
            results.Select((r, i) => (r, i));

        IOrderedEnumerable<(ProductLookupResult Result, int Index)> ordered;

        if (shoppingListContext)
        {
            ordered = indexed
                .OrderBy(x => SourceBucket(x.Result, localDataSource, masterDataSource))
                .ThenBy(x => RelevanceRank(x.Result, normalizedQuery))
                .ThenBy(x => x.Index);
        }
        else
        {
            ordered = indexed
                .OrderBy(x => RelevanceRank(x.Result, normalizedQuery))
                .ThenBy(x => x.Result.Name.Length)
                .ThenBy(x => x.Index);
        }

        return ordered.Select(x => x.Result).ToList();
    }

    // master(0) → local(1) → store/other(2)
    private static int SourceBucket(ProductLookupResult r, string localDataSource, string masterDataSource)
    {
        if (r.DataSources.ContainsKey(masterDataSource)) return 0;
        if (r.DataSources.ContainsKey(localDataSource)) return 1;
        return 2;
    }

    // exact(0) > starts-with(1) > contains(2) > other(3)
    private static int RelevanceRank(ProductLookupResult r, string normalizedQuery)
    {
        if (normalizedQuery.Length == 0) return 0;
        var name = (r.Name ?? string.Empty).ToLowerInvariant();
        if (name == normalizedQuery) return 0;
        if (name.StartsWith(normalizedQuery, StringComparison.Ordinal)) return 1;
        if (name.Contains(normalizedQuery, StringComparison.Ordinal)) return 2;
        return 3;
    }
}
