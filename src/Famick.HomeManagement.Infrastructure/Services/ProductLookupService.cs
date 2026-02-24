using System.Text.RegularExpressions;
using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Core.DTOs.StoreIntegrations;
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
    private readonly ILogger<ProductLookupService> _logger;
    private readonly IFileAccessTokenService _tokenService;
    private readonly IFileStorageService _fileStorage;

    // Regex for barcode detection: 8-14 digits (UPC-A, UPC-E, EAN-8, EAN-13, etc.)
    private static readonly Regex BarcodePattern = new(@"^[0-9]{8,14}$", RegexOptions.Compiled);

    /// <summary>
    /// Data source identifier for local products
    /// </summary>
    public const string LocalProductsDataSource = "Local Database";

    public ProductLookupService(
        IPluginLoader pluginLoader,
        HomeManagementDbContext dbContext,
        ITenantProvider tenantProvider,
        ITenantService tenantService,
        IFileAccessTokenService tokenService,
        IFileStorageService fileStorage,
        ILogger<ProductLookupService> logger)
    {
        _pluginLoader = pluginLoader;
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _tenantService = tenantService;
        _tokenService = tokenService;
        _fileStorage = fileStorage;
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

        // Search local products first unless ExternalSourcesOnly mode
        if (searchMode != ProductSearchMode.ExternalSourcesOnly)
        {
            var localResults = await SearchLocalProductsAsync(cleanedQuery, searchType, maxResults, ct);
            if (localResults.Any())
            {
                context.AddResults(localResults);
                _logger.LogInformation("Found {Count} local products for query '{Query}'",
                    localResults.Count, cleanedQuery);
            }
        }
        else
        {
            _logger.LogInformation("ExternalSourcesOnly mode - skipping local products search for query '{Query}'",
                cleanedQuery);
        }

        // If LocalProductsOnly mode, return immediately without searching external plugins
        if (searchMode == ProductSearchMode.LocalProductsOnly)
        {
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

        _logger.LogInformation("Searching with mode {SearchMode}, {PluginCount} plugins selected",
            searchMode, pluginsToRun.Count());

        // Phase 1: Parallel lookup — all plugins fetch from external APIs concurrently
        var pluginList = pluginsToRun.ToList();
        var lookupTasks = pluginList.Select(plugin =>
            SafeLookupAsync(plugin, cleanedQuery, searchType, maxResults, ct));
        var allLookupResults = await Task.WhenAll(lookupTasks);

        // Phase 2: Sequential enrichment — merge results in config.json order
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
    private async Task<List<ProductLookupResult>> SearchLocalProductsAsync(
        string query,
        ProductLookupSearchType searchType,
        int maxResults,
        CancellationToken ct)
    {
        var normalizedQuery = query.ToLowerInvariant();
        var results = new List<ProductLookupResult>();

        IQueryable<Product> productsQuery = _dbContext.Products
            .Include(p => p.Barcodes)
            .Include(p => p.Images)
            .Include(p => p.ProductGroup)
            .Include(p => p.ShoppingLocation)
            .Include(p => p.Nutrition)
            .Where(p => p.IsActive);

        if (searchType == ProductLookupSearchType.Barcode)
        {
            // For barcode search, match against product barcodes
            var normalizedBarcode = ProductLookupPipelineContext.NormalizeBarcode(query);
            productsQuery = productsQuery.Where(p =>
                p.Barcodes.Any(b => b.Barcode.Contains(query)));

            // Also do in-memory normalized barcode matching after query
            var products = await productsQuery.Take(maxResults).ToListAsync(ct);

            // Filter by normalized barcode comparison
            foreach (var product in products)
            {
                var matchingBarcode = product.Barcodes.FirstOrDefault(b =>
                    ProductLookupPipelineContext.NormalizeBarcode(b.Barcode)
                        .Equals(normalizedBarcode, StringComparison.OrdinalIgnoreCase) ||
                    b.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase));

                if (matchingBarcode != null || products.Count <= maxResults)
                {
                    results.Add(ConvertToLookupResult(product));
                }
            }
        }
        else
        {
            // For name search, match against name, description, and product group
            productsQuery = productsQuery.Where(p =>
                p.Name.ToLower().Contains(normalizedQuery) ||
                (p.Description != null && p.Description.ToLower().Contains(normalizedQuery)) ||
                (p.ProductGroup != null && p.ProductGroup.Name.ToLower().Contains(normalizedQuery)));

            var products = await productsQuery
                .OrderBy(p => p.Name)
                .Take(maxResults)
                .ToListAsync(ct);

            results = products.Select(ConvertToLookupResult).ToList();
        }

        return results;
    }

    /// <summary>
    /// Convert a local Product entity to a ProductLookupResult
    /// </summary>
    private ProductLookupResult ConvertToLookupResult(Product product)
    {
        var result = new ProductLookupResult
        {
            Name = product.Name,
            Description = product.Description,
            Barcode = product.Barcodes.FirstOrDefault()?.Barcode,
            Categories = product.ProductGroup != null
                ? new List<string> { product.ProductGroup.Name }
                : new List<string>(),
            ShoppingLocationId = product.ShoppingLocationId,
            ShoppingLocationName = product.ShoppingLocation?.Name,
            DataSources = new Dictionary<string, string>
            {
                { LocalProductsDataSource, product.Id.ToString() }
            }
        };

        // Add image if available
        var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary) ?? product.Images.FirstOrDefault();
        if (primaryImage != null)
        {
            if (!string.IsNullOrEmpty(primaryImage.ExternalUrl))
            {
                result.ImageUrl = new ResultImage
                {
                    ImageUrl = primaryImage.ExternalUrl,
                    PluginId = primaryImage.ExternalSource ?? LocalProductsDataSource
                };
                result.ThumbnailUrl = !string.IsNullOrEmpty(primaryImage.ExternalThumbnailUrl)
                    ? new ResultImage
                    {
                        ImageUrl = primaryImage.ExternalThumbnailUrl,
                        PluginId = primaryImage.ExternalSource ?? LocalProductsDataSource
                    }
                    : null;
            }
            else if (!string.IsNullOrEmpty(primaryImage.FileName))
            {
                // Generate URL with access token for local images
                var token = _tokenService.GenerateToken("product-image", primaryImage.Id, product.TenantId);
                var imageUrl = _fileStorage.GetProductImageUrl(product.Id, primaryImage.Id, token);
                result.ImageUrl = new ResultImage
                {
                    ImageUrl = imageUrl,
                    PluginId = LocalProductsDataSource
                };
            }
        }

        // Add nutrition data if available
        if (product.Nutrition != null)
        {
            result.BrandName = product.Nutrition.BrandName;
            result.BrandOwner = product.Nutrition.BrandOwner;
            result.Ingredients = product.Nutrition.Ingredients;
            result.ServingSizeDescription = product.Nutrition.ServingSizeDescription;
            result.Nutrition = new ProductLookupNutrition
            {
                Source = product.Nutrition.DataSource ?? LocalProductsDataSource,
                ServingSize = product.Nutrition.ServingSize,
                ServingUnit = product.Nutrition.ServingUnit,
                ServingsPerContainer = product.Nutrition.ServingsPerContainer,
                Calories = product.Nutrition.Calories,
                TotalFat = product.Nutrition.TotalFat,
                SaturatedFat = product.Nutrition.SaturatedFat,
                TransFat = product.Nutrition.TransFat,
                Cholesterol = product.Nutrition.Cholesterol,
                Sodium = product.Nutrition.Sodium,
                TotalCarbohydrates = product.Nutrition.TotalCarbohydrates,
                DietaryFiber = product.Nutrition.DietaryFiber,
                TotalSugars = product.Nutrition.TotalSugars,
                AddedSugars = product.Nutrition.AddedSugars,
                Protein = product.Nutrition.Protein,
                VitaminA = product.Nutrition.VitaminA,
                VitaminC = product.Nutrition.VitaminC,
                VitaminD = product.Nutrition.VitaminD,
                VitaminE = product.Nutrition.VitaminE,
                VitaminK = product.Nutrition.VitaminK,
                Thiamin = product.Nutrition.Thiamin,
                Riboflavin = product.Nutrition.Riboflavin,
                Niacin = product.Nutrition.Niacin,
                VitaminB6 = product.Nutrition.VitaminB6,
                Folate = product.Nutrition.Folate,
                VitaminB12 = product.Nutrition.VitaminB12,
                Calcium = product.Nutrition.Calcium,
                Iron = product.Nutrition.Iron,
                Magnesium = product.Nutrition.Magnesium,
                Phosphorus = product.Nutrition.Phosphorus,
                Potassium = product.Nutrition.Potassium,
                Zinc = product.Nutrition.Zinc
            };
        }

        return result;
    }

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
