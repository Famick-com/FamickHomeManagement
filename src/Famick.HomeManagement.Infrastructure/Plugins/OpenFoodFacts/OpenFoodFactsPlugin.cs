using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Plugin.Abstractions;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Microsoft.Extensions.Logging;
namespace Famick.HomeManagement.Infrastructure.Plugins.OpenFoodFacts;

/// <summary>
/// Built-in plugin for Open Food Facts API.
/// Primarily enriches existing results with product images.
/// Should run after USDA in the pipeline to add images to results.
/// https://world.openfoodfacts.org/data
/// </summary>
public class OpenFoodFactsPlugin : IProductLookupPlugin
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenFoodFactsPlugin> _logger;
    private string _baseUrl = "https://world.openfoodfacts.org";
    private bool _isInitialized;

    public string PluginId => "openfoodfacts";
    public string DisplayName => "Open Food Facts";
    public string Version => "1.0.0";
    public bool IsAvailable => _isInitialized;

    public PluginAttribution? Attribution => new()
    {
        Url = "https://openfoodfacts.org",
        LicenseText = "Database: ODbL, Images: CC BY-SA",
        Description = "A free, open, collaborative database of food products from around the world. "
            + "Database contents are available under the Open Database License (ODbL). "
            + "Product images are available under the Creative Commons Attribution-ShareAlike (CC BY-SA) license.",
        ProductUrlTemplate = $"{_baseUrl.TrimEnd('/')}/product/{{barcode}}"
    };

    PluginAttribution? IPlugin.Attribution => throw new NotImplementedException();

    public OpenFoodFactsPlugin(IHttpClientFactory httpClientFactory, ILogger<OpenFoodFactsPlugin> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        // Set User-Agent as required by Open Food Facts API guidelines
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FamickHomeManagement/1.0 (https://github.com/Famick-com)");
    }

    public Task InitAsync(JsonElement? pluginConfig, CancellationToken ct = default)
    {
        if (pluginConfig.HasValue)
        {
            var config = pluginConfig.Value;

            if (config.TryGetProperty("baseUrl", out var baseUrl))
            {
                _baseUrl = baseUrl.GetString() ?? _baseUrl;
            }
        }

        _logger.LogInformation("OpenFoodFacts plugin initialized with base URL: {BaseUrl}", _baseUrl);
        _isInitialized = true;
        return Task.CompletedTask;
    }

    public Task EnrichPipelineAsync(
        ProductLookupPipelineContext context,
        List<ProductLookupResult> lookupResults,
        CancellationToken ct = default)
    {
        int enrichedCount = 0;
        int addedCount = 0;

        foreach (var result in lookupResults)
        {
            // Try to find existing result to enrich by barcode
            var existingResult = result.Barcodes
                .Select(barcode => context.FindMatchingResult(barcode))
                .FirstOrDefault(r => r != null);

            if (existingResult != null)
            {
                // Enrich existing result with OpenFoodFacts data (first plugin wins via ??=)
                existingResult.ImageUrl ??= result.ImageUrl;
                existingResult.ThumbnailUrl ??= result.ThumbnailUrl;
                existingResult.BrandName ??= result.BrandName;
                existingResult.ProductUrl ??= result.ProductUrl;
                existingResult.Nutrition ??= result.Nutrition;
                existingResult.Ingredients ??= result.Ingredients;
                existingResult.ServingSizeDescription ??= result.ServingSizeDescription;
                existingResult.DataSources.TryAdd(result.DataSources.First().Key, result.DataSources.First().Value);

                // Merge attribution markdown
                if (!string.IsNullOrEmpty(result.AttributionMarkdown))
                {
                    existingResult.AttributionMarkdown = existingResult.AttributionMarkdown != null
                        ? existingResult.AttributionMarkdown + "\n\n" + result.AttributionMarkdown
                        : result.AttributionMarkdown;
                }

                // Merge additional data (Nutriscore, NOVA)
                if (result.AdditionalData != null)
                {
                    existingResult.AdditionalData ??= new Dictionary<string, object>();
                    foreach (var kvp in result.AdditionalData)
                    {
                        existingResult.AdditionalData.TryAdd(kvp.Key, kvp.Value);
                    }
                }

                enrichedCount++;
            }
            else
            {
                // Only add new results if we have room
                if (context.Results.Count < context.MaxResults)
                {
                    context.AddResult(result);
                    addedCount++;
                }
            }
        }

        _logger.LogDebug("OpenFoodFacts: enriched {Enriched} results, added {Added} new results",
            enrichedCount, addedCount);
        return Task.CompletedTask;
    }

    private async Task<OpenFoodFactsProduct?> GetProductByBarcodeAsync(Barcode barcode, CancellationToken ct)
    {
        try
        {
            var barcodeString = barcode.Data + barcode.CheckDigit.ToString();

            var url = $"{_baseUrl.TrimEnd('/')}/api/v2/product/{barcodeString}.json";
            var response = await _httpClient.GetFromJsonAsync<OpenFoodFactsProductResponse>(url, ct);

            if (response?.Status == 1 && response.Product != null)
            {
                return response.Product;
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch product from OpenFoodFacts: {Barcode}", barcode);
            return null;
        }
    }

    private async Task<List<OpenFoodFactsProduct>?> SearchProductsAsync(string query, int maxResults, CancellationToken ct)
    {
        try
        {
            // Use the search endpoint
            var url = $"{_baseUrl.TrimEnd('/')}/cgi/search.pl?search_terms={Uri.EscapeDataString(query)}&page_size={maxResults}&json=1";
            var response = await _httpClient.GetFromJsonAsync<OpenFoodFactsSearchResponse>(url, ct);

            return response?.Products;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to search OpenFoodFacts: {Query}", query);
            return null;
        }
    }

    private ProductLookupResult MapToLookupResult(OpenFoodFactsProduct product)
    {
        var name = product.ProductNameEn ?? product.ProductName ?? "Unknown Product";

        var thumbUrl = product.ImageFrontSmallUrl ?? product.ImageSmallUrl ?? product.ImageFrontThumbUrl;

        var imageUrl = product.ImageFrontUrl ?? product.ImageUrl;

        var result = new ProductLookupResult
        {
            DataSources = { { DisplayName, product.Code ?? string.Empty } },
            Name = name,
            BrandName = product.Brands,
            Categories = product.CategoriesTags ?? new(),
            ThumbnailUrl = !string.IsNullOrEmpty(thumbUrl) ? new ResultImage { ImageUrl = thumbUrl, PluginId = DisplayName } : null,
            ImageUrl = !string.IsNullOrEmpty(imageUrl) ? new ResultImage { ImageUrl = imageUrl, PluginId = DisplayName } : null,
            Ingredients = product.IngredientsTextEn ?? product.IngredientsText,
            ServingSizeDescription = product.ServingSize,
            ProductUrl = !string.IsNullOrEmpty(product.Code)
                ? $"{_baseUrl.TrimEnd('/')}/product/{product.Code}"
                : null,
            Nutrition = MapNutrition(product),
            AdditionalData = new Dictionary<string, object>(),
            AttributionMarkdown = BuildAttributionMarkdown(product.Code)
        };

        Barcode? barcode = null;
        var hasBarcode = !string.IsNullOrEmpty(product.Code) && 
                         BarcodeParser.TryParse(product.Code, out barcode);
        if (hasBarcode && barcode is not null)
        {
            result.Barcodes = new List<Barcode> { barcode};
        }

        if (!string.IsNullOrEmpty(product.NutriscoreGrade))
        {
            result.AdditionalData["nutriscore_grade"] = product.NutriscoreGrade;
        }

        if (product.NovaGroup.HasValue)
        {
            result.AdditionalData["nova_group"] = product.NovaGroup.Value;
        }

        return result;
    }

    private static string? GetFirstCategory(List<string>? categoryTags)
    {
        if (categoryTags == null || categoryTags.Count == 0) return null;

        // Category tags are like "en:beverages", extract the readable part
        var firstTag = categoryTags.FirstOrDefault();
        if (firstTag == null) return null;

        var parts = firstTag.Split(':');
        if (parts.Length > 1)
        {
            return FormatCategoryName(parts[1]);
        }

        return FormatCategoryName(firstTag);
    }

    private static string FormatCategoryName(string category)
    {
        // Convert "plant-based-beverages" to "Plant Based Beverages"
        return string.Join(" ", category
            .Split('-')
            .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
    }

    private string BuildAttributionMarkdown(string? barcode)
    {
        var sb = new StringBuilder();
        sb.Append($"Data from [{DisplayName}]({Attribution!.Url}) ({Attribution.LicenseText}).");
        if (!string.IsNullOrEmpty(barcode))
        {
            var productUrl = $"{_baseUrl.TrimEnd('/')}/product/{barcode}";
            sb.Append($" [View product]({productUrl})");
        }
        return sb.ToString();
    }

    private ProductLookupNutrition? MapNutrition(OpenFoodFactsProduct product)
    {
        var nutriments = product.Nutriments;
        if (nutriments == null) return null;

        // Prefer serving values, fall back to 100g values
        var nutrition = new ProductLookupNutrition
        {
            Source = PluginId,
            ExternalSourceId = product.Code,
            ServingSize = product.ServingQuantity,
            Calories = nutriments.EnergyKcalServing ?? nutriments.EnergyKcal100g,
            Protein = nutriments.ProteinsServing ?? nutriments.Proteins100g,
            TotalFat = nutriments.FatServing ?? nutriments.Fat100g,
            SaturatedFat = nutriments.SaturatedFatServing ?? nutriments.SaturatedFat100g,
            TotalCarbohydrates = nutriments.CarbohydratesServing ?? nutriments.Carbohydrates100g,
            TotalSugars = nutriments.SugarsServing ?? nutriments.Sugars100g,
            DietaryFiber = nutriments.FiberServing ?? nutriments.Fiber100g,
            Sodium = nutriments.SodiumServing ?? nutriments.Sodium100g
        };

        return nutrition;
    }

    public async Task<List<ProductLookupResult>> LookupAsync(Barcode barcode, int maxResults = 20, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("OpenFoodFacts plugin is not available");
            return new List<ProductLookupResult>();
        }

        _logger.LogInformation("OpenFoodFacts plugin looking up: {barcode}", barcode.Data);

        var product = await GetProductByBarcodeAsync(barcode, ct);
        if (product == null)
        {
            _logger.LogDebug("No product found in OpenFoodFacts for barcode: {barcode}", barcode.Data);
            return new List<ProductLookupResult>();
        }

        return new List<ProductLookupResult> { MapToLookupResult(product) };
    }

    public async Task<List<ProductLookupResult>> LookupAsync(string searchTerm, int maxResults = 20, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("OpenFoodFacts plugin is not available");
            return new List<ProductLookupResult>();
        }

        _logger.LogInformation("OpenFoodFacts plugin looking up: {searchTerm}", searchTerm);

        var products = await SearchProductsAsync(searchTerm, maxResults, ct);
        if (products == null || products.Count == 0)
        {
            _logger.LogDebug("No products found in OpenFoodFacts for query: {searchTerm}", searchTerm);
            return new List<ProductLookupResult>();
        }

        return products
            .Where(p => !string.IsNullOrEmpty(p.Code))
            .Select(MapToLookupResult)
            .ToList();
    }
}
