using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Core.DTOs.StoreIntegrations;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services.Search;

/// <summary>
/// Orchestrates the unified product search. Composes the existing single-source searches
/// (local, master, store, external plugins) — it does not reimplement them — then merges and
/// ranks. See <see cref="ProductSearchMerger"/> and <see cref="ProductSearchRanker"/>.
/// </summary>
public class UnifiedProductSearchService : IUnifiedProductSearchService
{
    private readonly IProductSearchService _searchService;
    private readonly IProductLookupService _lookupService;
    private readonly IStoreIntegrationService _storeIntegrationService;
    private readonly IDbContextFactory<HomeManagementDbContext> _contextFactory;
    private readonly ILogger<UnifiedProductSearchService> _logger;

    public UnifiedProductSearchService(
        IProductSearchService searchService,
        IProductLookupService lookupService,
        IStoreIntegrationService storeIntegrationService,
        IDbContextFactory<HomeManagementDbContext> contextFactory,
        ILogger<UnifiedProductSearchService> logger)
    {
        _searchService = searchService;
        _lookupService = lookupService;
        _storeIntegrationService = storeIntegrationService;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<List<ProductLookupResultDto>> SearchAsync(
        UnifiedProductSearchRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return new List<ProductLookupResultDto>();

        var query = request.Query.Trim();
        var max = request.MaxResults > 0 ? request.MaxResults : 20;
        var isShoppingList = request.Context == ProductSearchContext.ShoppingList;

        // Resolve the store to search. ShoppingList context always includes the list's store;
        // General context includes a store only when the caller opts into external sources.
        var storeId = await ResolveStoreIdAsync(request, ct);

        // Source selection: explicit overrides (used by legacy shims) win; otherwise the
        // context/IncludeExternal defaults apply.
        var runLocal = request.IncludeLocal ?? true;
        var runMaster = request.IncludeMaster ?? true;
        var runStore = (request.IncludeStore ?? (isShoppingList || request.IncludeExternal)) && storeId.HasValue;
        var runPlugins = request.IncludePlugins ?? (!isShoppingList && request.IncludeExternal);

        // ── Phase 1: fan out enabled sources in parallel ──
        var localTask = runLocal ? _searchService.SearchLocalForLookupAsync(query, max, ct) : EmptyResults();
        var masterTask = runMaster ? _searchService.SearchMasterCatalogForLookupAsync(query, max, ct) : EmptyResults();
        var pluginTask = runPlugins ? RunPluginsAsync(query, max, storeId, ct) : EmptyResults();
        var storeTask = runStore ? RunStoreAsync(storeId!.Value, query, max, ct) : EmptyStoreGroup();

        await Task.WhenAll(localTask, masterTask, pluginTask, storeTask);

        // ── Phase 2: merge (order sets identity-first-wins; store group is authoritative) ──
        var sources = new List<ProductMergeSource>
        {
            new() { Results = masterTask.Result },
            new() { Results = localTask.Result },
            new() { Results = pluginTask.Result },
            new() { Results = storeTask.Result, IsStoreAuthoritative = true },
        };

        var merged = ProductSearchMerger.Merge(sources);

        // ── Phase 3: exclude, rank, cap, map ──
        if (request.ExcludeProductId.HasValue)
        {
            var excludeId = request.ExcludeProductId.Value.ToString();
            merged = merged
                .Where(r => !r.DataSources.TryGetValue(ProductSearchService.LocalProductsDataSource, out var id)
                            || id != excludeId)
                .ToList();
        }

        var ranked = ProductSearchRanker.Rank(
            merged, query, isShoppingList,
            ProductSearchService.LocalProductsDataSource,
            ProductSearchService.MasterCatalogDataSource);

        return ranked
            .Take(max)
            .Select(ProductLookupResultDtoMapper.ToDto)
            .ToList();
    }

    private async Task<Guid?> ResolveStoreIdAsync(UnifiedProductSearchRequest request, CancellationToken ct)
    {
        if (request.ShoppingLocationId is { } explicitId && explicitId != Guid.Empty)
            return explicitId;

        if (request.ShoppingListId is { } listId && listId != Guid.Empty)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            var locationId = await context.ShoppingLists
                .Where(l => l.Id == listId)
                .Select(l => (Guid?)l.ShoppingLocationId)
                .FirstOrDefaultAsync(ct);
            if (locationId is { } id && id != Guid.Empty)
                return id;
        }

        return null;
    }

    private async Task<List<ProductLookupResult>> RunPluginsAsync(
        string query, int max, Guid? storeId, CancellationToken ct)
    {
        try
        {
            ProductLookupLocation? location = storeId.HasValue
                ? await _storeIntegrationService.ResolveLookupLocationAsync(storeId.Value, ct)
                : null;

            return await _lookupService.SearchAsync(
                query, max, ProductSearchMode.ExternalSourcesOnly, location, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External plugin search failed for query '{Query}'", query);
            return new List<ProductLookupResult>();
        }
    }

    private async Task<List<ProductLookupResult>> RunStoreAsync(
        Guid storeId, string query, int max, CancellationToken ct)
    {
        try
        {
            var storeResultsTask = _storeIntegrationService.SearchProductsAtStoreAsync(
                storeId, new StoreProductSearchRequest { Query = query, MaxResults = max }, ct);

            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            var location = await context.ShoppingLocations
                .FirstOrDefaultAsync(sl => sl.Id == storeId, ct);

            var storeResults = await storeResultsTask;
            return storeResults.Select(sr => ToLookupResult(sr, storeId, location)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Store integration search failed for location {LocationId}", storeId);
            return new List<ProductLookupResult>();
        }
    }

    // Mirrors ProductLookupController.MergeStoreResults' "new result" mapping so store rows are
    // shaped identically before merging.
    private static ProductLookupResult ToLookupResult(
        StoreProductResult sr, Guid storeId, Domain.Entities.ShoppingLocation? location)
    {
        return new ProductLookupResult
        {
            Name = sr.Name ?? string.Empty,
            Barcodes = sr.Barcodes,
            BrandName = sr.Brand,
            Description = sr.Description,
            ExternalProductId = sr.ExternalProductId,
            Price = sr.Price,
            PriceUnit = sr.PriceUnit,
            SalePrice = sr.SalePrice,
            Aisle = sr.Aisle,
            Shelf = sr.Shelf,
            Department = sr.Department,
            InStock = sr.InStock,
            Size = sr.Size,
            ProductUrl = sr.ProductUrl,
            ImageUrl = !string.IsNullOrEmpty(sr.ImageUrl)
                ? new ResultImage { ImageUrl = sr.ImageUrl, PluginId = location?.IntegrationType ?? "store" }
                : null,
            ShoppingLocationId = storeId,
            ShoppingLocationName = location?.Name,
            Categories = sr.Categories ?? new List<string>(),
            DataSources = new Dictionary<string, string>
            {
                { location?.Name ?? "Store", sr.ExternalProductId ?? "" }
            }
        };
    }

    private static Task<List<ProductLookupResult>> EmptyResults() =>
        Task.FromResult(new List<ProductLookupResult>());

    private static Task<List<ProductLookupResult>> EmptyStoreGroup() =>
        Task.FromResult(new List<ProductLookupResult>());
}
