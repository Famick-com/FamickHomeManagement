namespace Famick.HomeManagement.Shared.Barcodes;

/// <summary>
/// How a scanned barcode was matched against a locally cached shopping list.
/// </summary>
public enum BarcodeMatchKind
{
    /// <summary>The barcode is one of the item's own barcodes.</summary>
    Direct,

    /// <summary>The barcode is a Type 2 variable-weight/price code whose item number matched.</summary>
    Type2ItemNumber
}

/// <summary>
/// A scanned barcode resolved against a cached shopping list.
/// </summary>
/// <typeparam name="TItem">The caller's list-item type.</typeparam>
/// <param name="Item">The matched list item.</param>
/// <param name="Kind">How the match was made.</param>
/// <param name="EmbeddedPrice">Price embedded in a Type 2 barcode, if any.</param>
/// <param name="EmbeddedWeight">Weight in lbs embedded in a Type 2 barcode, if any.</param>
public record BarcodeMatch<TItem>(
    TItem Item,
    BarcodeMatchKind Kind,
    decimal? EmbeddedPrice,
    decimal? EmbeddedWeight);

/// <summary>
/// Matches a scanned barcode against an already-loaded shopping list, without touching
/// the network.
/// </summary>
/// <remarks>
/// The mobile app caches every list item together with all barcodes of its linked product,
/// so the overwhelmingly common scan outcome — "this item is on my list" — is answerable on
/// the device. This type holds that logic so it can be used on both the online and offline
/// paths and unit-tested without a page or a MAUI host.
///
/// It deliberately mirrors the server's <c>ScanBarcodeAsync</c> matching rules: raw barcode
/// first, then Type 2 item numbers at both digit positions stores are known to use.
/// </remarks>
public static class ShoppingListBarcodeMatcher
{
    /// <summary>
    /// Finds the list item a scanned barcode belongs to.
    /// </summary>
    /// <typeparam name="TItem">The caller's list-item type.</typeparam>
    /// <param name="barcode">The scanned barcode, already scanner-normalized.</param>
    /// <param name="items">The cached list items to search, in display order.</param>
    /// <param name="barcodesOf">Every barcode associated with an item.</param>
    /// <returns>The match, or null when the barcode is not on the list.</returns>
    public static BarcodeMatch<TItem>? Match<TItem>(
        string? barcode,
        IEnumerable<TItem> items,
        Func<TItem, IEnumerable<string>> barcodesOf)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(barcodesOf);

        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        var candidates = items as IReadOnlyCollection<TItem> ?? items.ToList();

        // Parse Type 2 up front: the embedded price/weight has to travel with the match even
        // when the item was found by its raw barcode, otherwise a weighed item records as a
        // single unit and the shopper's price is silently dropped.
        var type2 = WeightBarcodeParser.IsType2Barcode(barcode)
            ? WeightBarcodeParser.ParseType2Barcode(barcode)
            : null;

        var embeddedPrice = type2?.EmbeddingType == Type2EmbeddingType.Price ? type2.EmbeddedValue : (decimal?)null;
        var embeddedWeight = type2?.EmbeddingType == Type2EmbeddingType.Weight ? type2.EmbeddedValue : (decimal?)null;

        var direct = FindByBarcode(barcode, candidates, barcodesOf);
        if (direct is not null)
            return new BarcodeMatch<TItem>(direct.Value.Item, BarcodeMatchKind.Direct, embeddedPrice, embeddedWeight);

        if (type2 is null)
            return null;

        // A Type 2 barcode's trailing digits encode the weight or price, so the raw string is
        // unique per package and can never match a stored barcode. Fall back to the item
        // number, trying both digit positions — US standard is 1-5, some stores use 2-6.
        foreach (var itemNumber in Type2ItemNumbers(barcode, type2))
        {
            var byItemNumber = FindByBarcode(itemNumber, candidates, barcodesOf);
            if (byItemNumber is not null)
            {
                return new BarcodeMatch<TItem>(
                    byItemNumber.Value.Item, BarcodeMatchKind.Type2ItemNumber, embeddedPrice, embeddedWeight);
            }
        }

        return null;
    }

    /// <summary>
    /// The distinct Type 2 item numbers worth trying for a barcode, in preference order.
    /// </summary>
    private static IEnumerable<string> Type2ItemNumbers(string barcode, Type2BarcodeInfo parsed)
    {
        yield return parsed.ItemNumber;

        var alternate = WeightBarcodeParser.ParseType2Barcode(barcode, 2);
        if (alternate is not null && alternate.ItemNumber != parsed.ItemNumber)
            yield return alternate.ItemNumber;
    }

    private static (TItem Item, string Barcode)? FindByBarcode<TItem>(
        string barcode,
        IEnumerable<TItem> items,
        Func<TItem, IEnumerable<string>> barcodesOf)
    {
        foreach (var item in items)
        {
            var barcodes = barcodesOf(item);
            if (barcodes is null)
                continue;

            foreach (var candidate in barcodes)
            {
                if (!string.IsNullOrWhiteSpace(candidate)
                    && candidate.Equals(barcode, StringComparison.OrdinalIgnoreCase))
                {
                    return (item, candidate);
                }
            }
        }

        return null;
    }
}
