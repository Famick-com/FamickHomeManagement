using System.Text.RegularExpressions;
using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for searching products using the plugin pipeline.
/// Local products are always searched first and appear at the top of results.
/// Plugins are then executed in the order defined in config.json, each can add or enrich results.
/// </summary>
public class ProductLookupService : IProductLookupService
{
    private readonly IPluginLoader _pluginLoader;
    private readonly HomeManagementDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITenantService _tenantService;
    private readonly IProductSearchService _searchService;
    private readonly ILogger<ProductLookupService> _logger;

    // Regex for barcode detection: 8-14 digits (UPC-A, UPC-E, EAN-8, EAN-13, etc.)
    private static readonly Regex BarcodePattern = new(@"^[0-9]{8,14}$", RegexOptions.Compiled);

    public ProductLookupService(
        IPluginLoader pluginLoader,
        HomeManagementDbContext dbContext,
        ITenantProvider tenantProvider,
        ITenantService tenantService,
        IProductSearchService searchService,
        ILogger<ProductLookupService> logger)
    {
        _pluginLoader = pluginLoader;
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _tenantService = tenantService;
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Determines if the input looks like a barcode
    /// </summary>
    public static bool IsBarcode(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var cleaned = input.Trim().Replace("-", "").Replace(" ", "");
        return BarcodePattern.IsMatch(cleaned);
    }

    public async Task<List<ProductLookupResult>> SearchAsync(
        string query,
        int maxResults = 20,
        ProductSearchMode searchMode = ProductSearchMode.AllSources,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<ProductLookupResult>();
        }

        // Auto-detect search type and clean query
        var cleanedQuery = query.Trim();
        ProductLookupSearchType searchType;

        if (IsBarcode(cleanedQuery))
        {
            cleanedQuery = cleanedQuery.Replace("-", "").Replace(" ", "");
            searchType = ProductLookupSearchType.Barcode;
        }
        else
        {
            searchType = ProductLookupSearchType.Name;
        }

        // Create pipeline context
        var context = new ProductLookupPipelineContext(cleanedQuery, searchType, maxResults);

        // If LocalProductsOnly mode, search local and return immediately
        if (searchMode == ProductSearchMode.LocalProductsOnly)
        {
            var localResults = await _searchService.SearchLocalForLookupAsync(cleanedQuery, searchType, maxResults, ct);
            if (localResults.Any())
            {
                context.AddResults(localResults);
            }
            _logger.LogInformation("LocalProductsOnly mode - returning {Count} local results for query '{Query}'",
                context.Results.Count, cleanedQuery);
            return context.Results;
        }

        // Get all available product lookup plugins, filtering out tenant-disabled ones
        var disabledIds = await _tenantService.GetDisabledPluginIdsAsync(ct);
        var allPlugins = _pluginLoader.GetAvailablePlugins<IProductLookupPlugin>()
            .Where(p => !disabledIds.Contains(p.PluginId));

        // Filter plugins based on search mode
        IEnumerable<IProductLookupPlugin> pluginsToRun = searchMode switch
        {
            ProductSearchMode.StoreIntegrationsOnly =>
                allPlugins.Where(p => p is IStoreIntegrationPlugin),
            _ => allPlugins
        };

        var pluginList = pluginsToRun.ToList();
        _logger.LogInformation("Searching with mode {SearchMode}, {PluginCount} plugins selected",
            searchMode, pluginList.Count);

        // Phase 1: Parallel — run local DB search and all plugin API lookups concurrently.
        // Local results only affect the enrichment phase (Phase 2), not what plugins fetch,
        // so there is no dependency between the two.
        var localSearchTask = searchMode != ProductSearchMode.ExternalSourcesOnly
            ? _searchService.SearchLocalForLookupAsync(cleanedQuery, searchType, maxResults, ct)
            : Task.FromResult(new List<ProductLookupResult>());

        var pluginLookupTasks = pluginList.Select(plugin =>
            SafeLookupAsync(plugin, cleanedQuery, searchType, maxResults, ct));

        // Await both the local search and all plugin lookups in parallel
        var allLookupResultsTask = Task.WhenAll(pluginLookupTasks);
        await Task.WhenAll(localSearchTask, allLookupResultsTask);

        var localResults2 = localSearchTask.Result;
        var allLookupResults = allLookupResultsTask.Result;

        // Add local results to the pipeline context first (they appear at the top)
        if (localResults2.Any())
        {
            context.AddResults(localResults2);
            _logger.LogInformation("Found {Count} local products for query '{Query}'",
                localResults2.Count, cleanedQuery);
        }

        // Phase 2: Sequential enrichment — merge plugin results in config.json order
        for (int i = 0; i < pluginList.Count; i++)
        {
            try
            {
                await pluginList[i].EnrichPipelineAsync(context, allLookupResults[i], ct);
                _logger.LogInformation("Completed enrichment for plugin {PluginId}. Result count: {Count}",
                    pluginList[i].PluginId, context.Results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plugin {PluginId} failed during enrichment", pluginList[i].PluginId);
            }
        }

        _logger.LogInformation("Pipeline completed with {Count} results for query '{Query}' ({SearchType})",
            context.Results.Count, cleanedQuery, searchType);

        // If this was a barcode search, set the original search barcode on all results
        // This allows storing both the scanned barcode (e.g., 12-digit UPC) and the
        // plugin-returned barcode (e.g., 13-digit EAN) when they differ
        if (searchType == ProductLookupSearchType.Barcode)
        {
            foreach (var result in context.Results)
            {
                result.OriginalSearchBarcode = cleanedQuery;
            }
        }

        return context.Results;
    }

    /// <summary>
    /// Search local products table first - these always take priority in results.
    /// </summary>
    public async Task ApplyLookupResultAsync(Guid productId, ProductLookupResult result, CancellationToken ct = default)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new InvalidOperationException("Tenant context is required");

        var product = await _dbContext.Products
            .Include(p => p.Nutrition)
            .Include(p => p.Barcodes)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, ct);

        if (product == null)
        {
            throw new InvalidOperationException($"Product with ID {productId} not found");
        }

        // Create or update nutrition data
        var nutrition = product.Nutrition ?? new ProductNutrition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId
        };

        nutrition.BrandOwner = result.BrandOwner;
        nutrition.BrandName = result.BrandName;
        nutrition.Ingredients = result.Ingredients;
        nutrition.ServingSizeDescription = result.ServingSizeDescription;
        nutrition.LastUpdatedFromSource = DateTime.UtcNow;

        if (result.Nutrition != null)
        {
            MapNutritionData(nutrition, result.Nutrition);

            // Also update Product serving fields from nutrition data
            if (result.Nutrition.ServingSize.HasValue)
            {
                product.ServingSize = result.Nutrition.ServingSize;
            }
            if (!string.IsNullOrEmpty(result.Nutrition.ServingUnit))
            {
                product.ServingUnit = result.Nutrition.ServingUnit;
            }
            if (result.Nutrition.ServingsPerContainer.HasValue)
            {
                product.ServingsPerContainer = result.Nutrition.ServingsPerContainer;
            }
        }

        if (product.Nutrition == null)
        {
            _dbContext.ProductNutrition.Add(nutrition);
        }

        // Add barcodes in all formats for maximum scanning compatibility
        // Generate variants from both the plugin-returned barcode and the original scan barcode
        var allVariants = new HashSet<BarcodeVariant>();
        var inputBarcodes = new List<string>();

        if (!string.IsNullOrEmpty(result.Barcode))
        {
            inputBarcodes.Add(result.Barcode);
        }

        if (!string.IsNullOrEmpty(result.OriginalSearchBarcode))
        {
            inputBarcodes.Add(result.OriginalSearchBarcode);
        }

        // Generate all format variants for each input barcode
        foreach (var inputBarcode in inputBarcodes)
        {
            var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(inputBarcode);
            foreach (var variant in variants)
            {
                allVariants.Add(variant);
            }
        }

        // If no variants were generated (e.g., non-US EAN-13), fall back to storing raw barcodes
        if (allVariants.Count == 0)
        {
            var dataSourceNote = $"From {string.Join(", ", result.DataSources.Select(i => i.Key))}";
            foreach (var inputBarcode in inputBarcodes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                allVariants.Add(new BarcodeVariant(inputBarcode, "Unknown", dataSourceNote));
            }
        }

        // Add each barcode variant if not already present
        foreach (var variant in allVariants)
        {
            // Check if barcode already exists on this product
            var existingOnProduct = product.Barcodes
                .FirstOrDefault(b => b.Barcode.Equals(variant.Barcode, StringComparison.OrdinalIgnoreCase));

            if (existingOnProduct != null)
            {
                continue; // Already on this product
            }

            // Check if barcode exists on ANY product in this tenant (unique constraint)
            var existingInTenant = await _dbContext.ProductBarcodes
                .AnyAsync(b => b.TenantId == tenantId &&
                              b.Barcode == variant.Barcode &&
                              b.ProductId != productId, ct);

            if (existingInTenant)
            {
                _logger.LogWarning(
                    "Barcode {Barcode} ({Format}) already exists on another product in tenant {TenantId}, skipping",
                    variant.Barcode, variant.Format, tenantId);
                continue;
            }

            _dbContext.ProductBarcodes.Add(new ProductBarcode
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = productId,
                Barcode = variant.Barcode,
                Note = variant.Note
            });
        }

        // Store plugin-generated attribution markdown
        product.DataSourceAttribution = BuildCombinedAttribution(result);

        // Add external image if available and not already present
        if (result.ImageUrl != null)
        {
            // Check if we already have an image from this source
            var existingImage = product.Images
                .FirstOrDefault(i => i.ExternalSource == result.ImageUrl.PluginId);

            if (existingImage != null)
            {
                // Update existing external image
                existingImage.ExternalUrl = result.ImageUrl.ImageUrl;
                existingImage.ExternalThumbnailUrl = result.ThumbnailUrl?.ImageUrl;
            }
            else
            {
                // Add new external image as primary if no primary exists
                var hasPrimary = product.Images.Any(i => i.IsPrimary);

                _dbContext.ProductImages.Add(new ProductImage
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = productId,
                    ExternalUrl = result.ImageUrl.ImageUrl,
                    ExternalThumbnailUrl = result.ThumbnailUrl?.ImageUrl,
                    ExternalSource = result.ImageUrl.PluginId,
                    FileName = string.Empty, // No local file
                    OriginalFileName = $"External image from {result.ImageUrl.PluginId}",
                    ContentType = "image/jpeg", // Assume JPEG for external images
                    FileSize = 0,
                    SortOrder = product.Images.Count,
                    IsPrimary = !hasPrimary // Make primary if no other primary exists
                });
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public IReadOnlyList<PluginInfo> GetAvailablePlugins()
    {
        return _pluginLoader.Plugins
            .OfType<IProductLookupPlugin>()
            .Select(p => new PluginInfo
            {
                PluginId = p.PluginId,
                DisplayName = p.DisplayName,
                Version = p.Version,
                IsAvailable = p.IsAvailable,
                AttributionUrl = p.Attribution?.Url,
                LicenseText = p.Attribution?.LicenseText,
                Description = p.Attribution?.Description,
                ProductUrlTemplate = p.Attribution?.ProductUrlTemplate
            })
            .ToList()
            .AsReadOnly();
    }

    private async Task<List<ProductLookupResult>> SafeLookupAsync(
        IProductLookupPlugin plugin,
        string query,
        ProductLookupSearchType searchType,
        int maxResults,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Starting lookup for plugin {PluginId}", plugin.PluginId);
            var results = await plugin.LookupAsync(query, searchType, maxResults, ct);
            _logger.LogInformation("Plugin {PluginId} returned {Count} results", plugin.PluginId, results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin {PluginId} failed during lookup", plugin.PluginId);
            return new List<ProductLookupResult>();
        }
    }

    private static string? BuildCombinedAttribution(ProductLookupResult result)
    {
        if (string.IsNullOrEmpty(result.AttributionMarkdown))
            return null;

        var lines = new List<string> { result.AttributionMarkdown };
        lines.Add("");
        lines.Add("*Nutrition and product information may not be 100% accurate. Always verify with product packaging.*");
        return string.Join("\n\n", lines);
    }

    private static void MapNutritionData(ProductNutrition target, ProductLookupNutrition source)
    {
        target.ServingSize = source.ServingSize;
        target.ServingUnit = source.ServingUnit;
        target.ServingsPerContainer = source.ServingsPerContainer;
        target.Calories = source.Calories;
        target.TotalFat = source.TotalFat;
        target.SaturatedFat = source.SaturatedFat;
        target.TransFat = source.TransFat;
        target.Cholesterol = source.Cholesterol;
        target.Sodium = source.Sodium;
        target.TotalCarbohydrates = source.TotalCarbohydrates;
        target.DietaryFiber = source.DietaryFiber;
        target.TotalSugars = source.TotalSugars;
        target.AddedSugars = source.AddedSugars;
        target.Protein = source.Protein;
        target.VitaminA = source.VitaminA;
        target.VitaminC = source.VitaminC;
        target.VitaminD = source.VitaminD;
        target.VitaminE = source.VitaminE;
        target.VitaminK = source.VitaminK;
        target.Thiamin = source.Thiamin;
        target.Riboflavin = source.Riboflavin;
        target.Niacin = source.Niacin;
        target.VitaminB6 = source.VitaminB6;
        target.Folate = source.Folate;
        target.VitaminB12 = source.VitaminB12;
        target.Calcium = source.Calcium;
        target.Iron = source.Iron;
        target.Magnesium = source.Magnesium;
        target.Phosphorus = source.Phosphorus;
        target.Potassium = source.Potassium;
        target.Zinc = source.Zinc;
    }
}
