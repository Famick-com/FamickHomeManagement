using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Controls which product fields are included in text-based search.
/// </summary>
[Flags]
public enum ProductSearchFields
{
    Name = 1,
    Description = 2,
    ProductGroupName = 4,
    ShoppingLocationName = 8,
    Barcodes = 16,
    All = Name | Description | ProductGroupName | ShoppingLocationName | Barcodes,
    NameAndDescription = Name | Description
}

/// <summary>
/// Consolidated product search service. All product search, autocomplete, barcode lookup,
/// and filtered listing logic lives here behind shared building blocks.
/// </summary>
public interface IProductSearchService
{
    /// <summary>
    /// Full-text search across Name, Description, ProductGroup, ShoppingLocation, Barcodes.
    /// Returns full ProductDto with image URLs.
    /// </summary>
    Task<List<ProductDto>> SearchAsync(string searchTerm, CancellationToken ct = default);

    /// <summary>
    /// Lightweight autocomplete for active products by Name.
    /// Results are cached via IDistributedCache.
    /// </summary>
    Task<List<ProductAutocompleteDto>> AutocompleteAsync(string searchTerm, int maxResults = 10, CancellationToken ct = default);

    /// <summary>
    /// 3-phase barcode lookup: exact match, variant (EAN-13/UPC-A), normalized fallback.
    /// Returns single ProductDto with stock summary.
    /// </summary>
    Task<ProductDto?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);

    /// <summary>
    /// Combined tenant product + master catalog parent product search.
    /// Returns up to 50 results, deduplicated by name. Tenant and master queries run in parallel.
    /// </summary>
    Task<List<ParentProductSearchResultDto>> SearchParentProductsAsync(string searchTerm, CancellationToken ct = default);

    /// <summary>
    /// Builds an IQueryable with text search, category filters, sorting, and full includes.
    /// Used by ListAsync/ListPagedAsync in ProductsService.
    /// </summary>
    Task<IQueryable<Product>> BuildProductQueryAsync(ProductFilterRequest? filter, CancellationToken ct = default);

    /// <summary>
    /// Local product search for the plugin lookup pipeline.
    /// Auto-detects barcode vs name search and returns ProductLookupResult.
    /// </summary>
    Task<List<ProductLookupResult>> SearchLocalForLookupAsync(
        string query,
        int maxResults,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates all cached search results for the current tenant.
    /// Call on product create/update/delete.
    /// </summary>
    void InvalidateCache();
}
