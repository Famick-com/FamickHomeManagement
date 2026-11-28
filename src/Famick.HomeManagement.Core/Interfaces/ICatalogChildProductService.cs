using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// A scanned barcode the master catalog lists as a child barcode of one of its generic products:
/// the barcode of a specific product (one brand's UPC) that belongs under the generic one.
/// </summary>
/// <param name="Barcode">The barcode as scanned.</param>
/// <param name="MasterProductId">The catalog's generic product the barcode belongs under.</param>
/// <param name="MasterProductName">That product's name.</param>
/// <param name="Label">The catalog's label for the specific product (e.g. "Brand X, ½ gal"), if any.</param>
/// <param name="ParentProductId">The household's copy of the generic product, if it has one.</param>
public sealed record CatalogChildMatch(
    string Barcode,
    Guid MasterProductId,
    string MasterProductName,
    string? Label,
    Guid? ParentProductId);

/// <summary>
/// Resolves scans of master catalog child barcodes to the household's generic product, and creates
/// the specific product as a child of it. Used by both scan paths: shopping scans record the child as
/// pending and leave the creation to the move to inventory, inventory scans create it straight away.
/// </summary>
public interface ICatalogChildProductService
{
    /// <summary>
    /// Matches a scan to a catalog child barcode. Null when the household has a product of its own for
    /// the barcode, or the catalog doesn't list it as a child barcode. A child barcode copied onto the
    /// household's generic product matches, with that product as the parent.
    /// </summary>
    Task<CatalogChildMatch?> FindAsync(string barcode, CancellationToken ct = default);

    /// <summary>
    /// Creates the child product for a scanned barcode under <paramref name="parentProductId"/>, which
    /// takes the barcode, and starts filling in its details from the lookup plugins in the background.
    /// Returns the product that has the barcode, which is an existing one if another scan created it
    /// first.
    /// </summary>
    /// <param name="parentProductId">The household's copy of the generic product.</param>
    /// <param name="barcode">The scanned barcode.</param>
    /// <param name="label">The catalog's label for the specific product, used as its name until the
    /// lookup finds a better one.</param>
    /// <param name="childProductId">The id to give the new product, or null for a new one.</param>
    /// <param name="location">The store to ask a store plugin about, if any.</param>
    Task<Product> CreateChildAsync(
        Guid parentProductId,
        string barcode,
        string? label,
        Guid? childProductId = null,
        ProductLookupLocation? location = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fills in a child product created by <see cref="CreateChildAsync"/> from the lookup plugins: name,
    /// brand, image and nutrition from the top result. Does nothing once the product has been renamed
    /// from <paramref name="provisionalName"/>, or when no plugin finds the barcode; never throws for a
    /// plugin's failure.
    /// </summary>
    Task EnrichAsync(
        Guid productId,
        string barcode,
        string provisionalName,
        ProductLookupLocation? location = null,
        CancellationToken ct = default);
}
