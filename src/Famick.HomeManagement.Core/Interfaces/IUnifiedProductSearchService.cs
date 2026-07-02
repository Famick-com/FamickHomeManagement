using Famick.HomeManagement.Core.DTOs.ProductLookup;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Single entry point for product search. Fans out to the enabled sources (master catalog,
/// local products, store integration, external plugins) in parallel, merges duplicates into
/// one row per product, and ranks per <see cref="ProductSearchContext"/>. Backs the unified
/// endpoint and all legacy search endpoints (as shims).
/// </summary>
public interface IUnifiedProductSearchService
{
    Task<List<ProductLookupResultDto>> SearchAsync(
        UnifiedProductSearchRequest request,
        CancellationToken ct = default);
}
