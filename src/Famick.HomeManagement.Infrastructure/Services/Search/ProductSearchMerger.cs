using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;

namespace Famick.HomeManagement.Infrastructure.Services.Search;

/// <summary>
/// One group of results from a single source, in the order it should be folded into the
/// merged set. Identity fields are won by the first source to provide them, so pass the
/// canonical sources (master, local) before the enrichment sources (plugins), and pass the
/// store group LAST with <see cref="IsStoreAuthoritative"/> set so its fields overwrite.
/// </summary>
public sealed class ProductMergeSource
{
    public required IReadOnlyList<ProductLookupResult> Results { get; init; }

    /// <summary>
    /// When true, this source's store-specific fields (price, aisle, department, in-stock,
    /// image, store context) overwrite whatever an earlier source set on a merged row.
    /// </summary>
    public bool IsStoreAuthoritative { get; init; }
}

/// <summary>
/// Merges product results from several sources into a single deduplicated list.
///
/// Dedupe key: normalized barcode first, then normalized name as a fallback (so the same
/// product from master, local, and a store collapses to one row even without a shared barcode).
///
/// Field-merge policy (two tiers):
///   • Identity fields (name, description, brand, category, nutrition, ingredients) — FIRST
///     source wins (<c>??=</c> semantics).
///   • Store-authoritative fields (price, price unit, sale price, aisle, shelf, department,
///     in-stock, size, product url, store image, shopping-location context) — the store
///     source OVERWRITES when it supplies a value; non-store sources only fill blanks.
///
/// The merger mutates and returns the earliest-seen result object for each row, so callers
/// must pass freshly-produced result lists (which every source here does).
/// </summary>
public static class ProductSearchMerger
{
    public static List<ProductLookupResult> Merge(IReadOnlyList<ProductMergeSource> sources)
    {
        var merged = new List<ProductLookupResult>();

        foreach (var source in sources)
        {
            foreach (var incoming in source.Results)
            {
                var existing = FindMatch(merged, incoming);
                if (existing == null)
                {
                    merged.Add(incoming);
                    continue;
                }

                MergeInto(existing, incoming, source.IsStoreAuthoritative);
            }
        }

        return merged;
    }

    private static ProductLookupResult? FindMatch(List<ProductLookupResult> merged, ProductLookupResult incoming)
    {
        // Priority 1: barcode match (Barcode overrides Equals — normalizes across UPC-A/EAN-13).
        if (incoming.Barcodes.Any())
        {
            var byBarcode = merged.FirstOrDefault(m =>
                m.Barcodes.Any(mb => incoming.Barcodes.Any(ib => mb.Equals(ib))));
            if (byBarcode != null) return byBarcode;
        }

        // Priority 2: normalized-name fallback.
        var name = Normalize(incoming.Name);
        if (name.Length == 0) return null;
        return merged.FirstOrDefault(m => Normalize(m.Name) == name);
    }

    private static void MergeInto(ProductLookupResult existing, ProductLookupResult incoming, bool storeAuthoritative)
    {
        // ── Identity fields: first source wins ──
        if (string.IsNullOrWhiteSpace(existing.Name)) existing.Name = incoming.Name;
        existing.Description ??= incoming.Description;
        existing.BrandName ??= incoming.BrandName;
        existing.BrandOwner ??= incoming.BrandOwner;
        existing.Ingredients ??= incoming.Ingredients;
        existing.ServingSizeDescription ??= incoming.ServingSizeDescription;
        existing.Nutrition ??= incoming.Nutrition;
        existing.OriginalSearchBarcode ??= incoming.OriginalSearchBarcode;

        // Categories: union, case-insensitive.
        foreach (var category in incoming.Categories)
        {
            if (!existing.Categories.Any(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)))
                existing.Categories.Add(category);
        }

        // Barcodes: union.
        var barcodes = existing.Barcodes.ToList();
        foreach (var barcode in incoming.Barcodes)
        {
            if (!barcodes.Any(b => b.Equals(barcode)))
                barcodes.Add(barcode);
        }
        existing.Barcodes = barcodes;

        // ── Store-authoritative fields: store overwrites, others fill blanks ──
        existing.Price = PickStore(existing.Price, incoming.Price, storeAuthoritative);
        existing.PriceUnit = PickStore(existing.PriceUnit, incoming.PriceUnit, storeAuthoritative);
        existing.SalePrice = PickStore(existing.SalePrice, incoming.SalePrice, storeAuthoritative);
        existing.Aisle = PickStore(existing.Aisle, incoming.Aisle, storeAuthoritative);
        existing.Shelf = PickStore(existing.Shelf, incoming.Shelf, storeAuthoritative);
        existing.Department = PickStore(existing.Department, incoming.Department, storeAuthoritative);
        existing.InStock = PickStore(existing.InStock, incoming.InStock, storeAuthoritative);
        existing.Size = PickStore(existing.Size, incoming.Size, storeAuthoritative);
        existing.ProductUrl = PickStore(existing.ProductUrl, incoming.ProductUrl, storeAuthoritative);
        existing.ShoppingLocationId = PickStore(existing.ShoppingLocationId, incoming.ShoppingLocationId, storeAuthoritative);
        existing.ShoppingLocationName = PickStore(existing.ShoppingLocationName, incoming.ShoppingLocationName, storeAuthoritative);
        existing.ImageUrl = PickStore(existing.ImageUrl, incoming.ImageUrl, storeAuthoritative);
        existing.ThumbnailUrl = PickStore(existing.ThumbnailUrl, incoming.ThumbnailUrl, storeAuthoritative);

        // ── Provenance ──
        foreach (var kvp in incoming.DataSources)
            existing.DataSources.TryAdd(kvp.Key, kvp.Value);

        if (!string.IsNullOrWhiteSpace(incoming.AttributionMarkdown))
        {
            existing.AttributionMarkdown = string.IsNullOrWhiteSpace(existing.AttributionMarkdown)
                ? incoming.AttributionMarkdown
                : $"{existing.AttributionMarkdown}\n\n{incoming.AttributionMarkdown}";
        }
    }

    /// <summary>
    /// Store-field selection: if the incoming value comes from the store source and is set,
    /// it wins; otherwise keep the existing value, falling back to incoming when blank.
    /// </summary>
    private static T PickStore<T>(T existing, T incoming, bool storeAuthoritative)
    {
        var incomingSet = incoming is not null;
        if (storeAuthoritative && incomingSet) return incoming;
        return existing is not null ? existing : incoming;
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
