# Plugin Authoring Guide

This guide explains how to create product lookup plugins for the Famick Home Management self-hosted application. Plugins let you add new external data sources — nutrition databases, regional food databases, specialty product APIs — that integrate into the product lookup pipeline.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [IPlugin Interface](#iplugin-interface)
- [IProductLookupPlugin Interface](#iproductlookupplugin-interface)
- [Attribution](#attribution)
- [Pipeline Context](#pipeline-context)
- [Result Models](#result-models)
- [Configuration](#configuration)
- [Registration](#registration)
- [Store Integration Plugins](#store-integration-plugins)
- [Testing](#testing)
- [Docker Deployment](#docker-deployment)

---

## Overview

The plugin system supports two types of plugins:

| Type | Interface | Purpose |
|------|-----------|---------|
| **Product Lookup** | `IProductLookupPlugin` | Search external databases for product information (nutrition, images, barcodes) |
| **Store Integration** | `IStoreIntegrationPlugin` | Connect to grocery store APIs for pricing, availability, and shopping carts |

This guide focuses on **product lookup plugins**, which are the most common type for community contributors. For store integration plugins, see [Store Integration Plugins](#store-integration-plugins).

### Pipeline Architecture

Product lookup uses a **two-phase pipeline** for optimal performance:

1. **Parallel Lookup** — All enabled plugins call their external APIs concurrently via `LookupAsync`. Each plugin receives the search query and returns a list of `ProductLookupResult`. Plugins must NOT access the pipeline context during this phase.

2. **Sequential Enrichment** — After all lookups complete, each plugin's `EnrichPipelineAsync` is called in `config.json` order. This phase merges lookup results into the shared pipeline context using the "first plugin wins" pattern (`??=`).

**Example flow** (barcode scan for a US food product):

```
Phase 1 (parallel):
  USDA FoodData Central  →  Returns result with nutrition data (no image)
  Open Food Facts         →  Returns result with product image + nutrition
  Your Custom Plugin      →  Returns result with regional data

Phase 2 (sequential, in config.json order):
  1. USDA enrichment      →  Adds USDA result to pipeline context
  2. OFF enrichment       →  Finds matching barcode, enriches USDA result with image
  3. Your plugin          →  Finds matching barcode, enriches with regional data
```

---

## Quick Start

Here's a minimal product lookup plugin that queries a fictional "My Nutrition API":

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Famick.HomeManagement.Core.Interfaces.Plugins;

namespace MyNutritionPlugin;

public class MyNutritionPlugin : IProductLookupPlugin
{
    private readonly HttpClient _httpClient = new();
    private string _apiKey = string.Empty;
    private bool _isInitialized;

    public string PluginId => "mynutrition";
    public string DisplayName => "My Nutrition API";
    public string Version => "1.0.0";
    public bool IsAvailable => _isInitialized && !string.IsNullOrEmpty(_apiKey);

    public PluginAttribution? Attribution => new()
    {
        Url = "https://mynutritionapi.example.com",
        LicenseText = "CC BY 4.0",
        Description = "My Nutrition API provides regional nutrition data. "
            + "Data is licensed under Creative Commons Attribution 4.0.",
        ProductUrlTemplate = "https://mynutritionapi.example.com/product/{barcode}"
    };

    public Task InitAsync(JsonElement? pluginConfig, CancellationToken ct = default)
    {
        if (pluginConfig.HasValue)
        {
            var config = pluginConfig.Value;
            if (config.TryGetProperty("apiKey", out var apiKey))
                _apiKey = apiKey.GetString() ?? string.Empty;
        }

        _isInitialized = true;
        return Task.CompletedTask;
    }

    public async Task<List<ProductLookupResult>> LookupAsync(
        string query,
        ProductLookupSearchType searchType,
        int maxResults = 20,
        CancellationToken ct = default)
    {
        if (!IsAvailable) return new List<ProductLookupResult>();

        if (searchType != ProductLookupSearchType.Barcode)
            return new List<ProductLookupResult>();

        var url = $"https://mynutritionapi.example.com/v1/barcode/{query}?key={_apiKey}";
        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return new List<ProductLookupResult>();

        var data = await response.Content.ReadFromJsonAsync<MyApiResponse>(ct);
        if (data == null) return new List<ProductLookupResult>();

        return new List<ProductLookupResult>
        {
            new()
            {
                DataSources = { { DisplayName, data.ProductId } },
                Name = data.ProductName,
                Barcode = query,
                Nutrition = new ProductLookupNutrition
                {
                    Source = PluginId,
                    Calories = data.Calories,
                    Protein = data.Protein,
                    TotalFat = data.Fat
                },
                AttributionMarkdown = BuildAttributionMarkdown(query)
            }
        };
    }

    public Task EnrichPipelineAsync(
        ProductLookupPipelineContext context,
        List<ProductLookupResult> lookupResults,
        CancellationToken ct = default)
    {
        foreach (var result in lookupResults)
        {
            var existing = context.FindMatchingResult(barcode: result.Barcode);

            if (existing != null)
            {
                // Enrich existing result (first plugin wins via ??=)
                existing.Nutrition ??= result.Nutrition;
                existing.DataSources.TryAdd(DisplayName, result.Barcode ?? "");

                // Merge attribution
                if (!string.IsNullOrEmpty(result.AttributionMarkdown))
                {
                    existing.AttributionMarkdown = existing.AttributionMarkdown != null
                        ? existing.AttributionMarkdown + "\n\n" + result.AttributionMarkdown
                        : result.AttributionMarkdown;
                }
            }
            else
            {
                context.AddResult(result);
            }
        }
        return Task.CompletedTask;
    }

    private string BuildAttributionMarkdown(string barcode)
    {
        var productUrl = Attribution!.ProductUrlTemplate!.Replace("{barcode}", barcode);
        return $"Data from [{DisplayName}]({Attribution.Url}) ({Attribution.LicenseText}). [View product]({productUrl})";
    }

    // Your API response model (private to this plugin)
    private record MyApiResponse(
        string ProductId,
        string ProductName,
        decimal? Calories,
        decimal? Protein,
        decimal? Fat);
}
```

To deploy this plugin, compile it as a class library DLL, place it in the `plugins/` folder, and add an entry to `plugins/config.json`. See [Configuration](#configuration) and [Docker Deployment](#docker-deployment) for details.

---

## IPlugin Interface

All plugins implement `IPlugin`, the base interface:

```csharp
public interface IPlugin
{
    string PluginId { get; }
    string DisplayName { get; }
    string Version { get; }
    bool IsAvailable { get; }
    PluginAttribution? Attribution { get; }
    Task InitAsync(JsonElement? pluginConfig, CancellationToken ct = default);
}
```

| Property | Description |
|----------|-------------|
| `PluginId` | Unique identifier used as the key in `plugins/config.json` (e.g., `"usda"`, `"openfoodfacts"`) |
| `DisplayName` | Human-readable name shown in the UI and stored in `DataSources` (e.g., `"USDA FoodData Central"`) |
| `Version` | Semantic version string for the plugin |
| `IsAvailable` | Whether the plugin is ready to process requests. Return `false` if required configuration (like an API key) is missing. |
| `Attribution` | Licensing and attribution metadata. Return `null` only if the plugin uses no external data that requires attribution. See [Attribution](#attribution). |
| `InitAsync` | Called once at startup with the plugin's `config` section from `plugins/config.json` as a `JsonElement`. Parse your configuration here. |

### InitAsync

The `pluginConfig` parameter contains the `"config"` object from the plugin's entry in `config.json`, or `null` if no config section exists.

Here is how the built-in USDA plugin reads its configuration:

```csharp
public Task InitAsync(JsonElement? pluginConfig, CancellationToken ct = default)
{
    if (pluginConfig.HasValue)
    {
        var config = pluginConfig.Value;

        if (config.TryGetProperty("apiKey", out var apiKey))
            _apiKey = apiKey.GetString() ?? string.Empty;

        if (config.TryGetProperty("baseUrl", out var baseUrl))
            _baseUrl = baseUrl.GetString() ?? _baseUrl;

        if (config.TryGetProperty("defaultMaxResults", out var maxResults))
            _defaultMaxResults = maxResults.GetInt32();
    }

    _isInitialized = true;
    return Task.CompletedTask;
}
```

Each plugin defines its own configuration schema — there is no fixed format beyond what you choose to support.

---

## IProductLookupPlugin Interface

Product lookup plugins extend `IPlugin` with two methods:

```csharp
public interface IProductLookupPlugin : IPlugin
{
    /// Fetch product data from the external source.
    /// Runs in parallel across all plugins — do NOT access the pipeline context here.
    Task<List<ProductLookupResult>> LookupAsync(
        string query,
        ProductLookupSearchType searchType,
        int maxResults = 20,
        CancellationToken ct = default);

    /// Merge this plugin's lookup results into the shared pipeline context.
    /// Called sequentially in config.json order after all lookups complete.
    Task EnrichPipelineAsync(
        ProductLookupPipelineContext context,
        List<ProductLookupResult> lookupResults,
        CancellationToken ct = default);
}
```

### LookupAsync

Called during Phase 1 (parallel). Receives the search query and returns results from your external API. This method runs concurrently with all other plugins, so it must NOT access the pipeline context.

```csharp
public async Task<List<ProductLookupResult>> LookupAsync(
    string query, ProductLookupSearchType searchType,
    int maxResults = 20, CancellationToken ct = default)
{
    if (!IsAvailable) return new List<ProductLookupResult>();

    // Call your external API
    var apiResults = await _httpClient.GetAsync($"/search?q={query}", ct);
    // Map and return results
    return mappedResults;
}
```

**Important**: Return an empty list on errors — don't throw exceptions. The pipeline orchestrator wraps each call in a try/catch, but handling errors yourself gives you better logging.

### EnrichPipelineAsync

Called during Phase 2 (sequential). Receives the pipeline context and the results your `LookupAsync` returned. Merge your results into the shared context:

```csharp
public Task EnrichPipelineAsync(
    ProductLookupPipelineContext context,
    List<ProductLookupResult> lookupResults,
    CancellationToken ct = default)
{
    foreach (var result in lookupResults)
    {
        var existing = context.FindMatchingResult(barcode: result.Barcode);
        if (existing != null)
        {
            // Enrich existing result (first plugin wins via ??=)
            existing.Nutrition ??= result.Nutrition;
            existing.ImageUrl ??= result.ImageUrl;
            existing.DataSources.TryAdd(DisplayName, result.Barcode ?? "");

            // Merge attribution
            if (!string.IsNullOrEmpty(result.AttributionMarkdown))
            {
                existing.AttributionMarkdown = existing.AttributionMarkdown != null
                    ? existing.AttributionMarkdown + "\n\n" + result.AttributionMarkdown
                    : result.AttributionMarkdown;
            }
        }
        else
        {
            context.AddResult(result);
        }
    }
    return Task.CompletedTask;
}
```

---

## Attribution

If your plugin fetches data from any external source, you should provide attribution via two mechanisms:

1. **`Attribution` property** — metadata displayed on the settings/about page
2. **`AttributionMarkdown` on results** — per-product attribution text stored with the product

### PluginAttribution

```csharp
public class PluginAttribution
{
    public required string Url { get; set; }
    public required string LicenseText { get; set; }
    public string? Description { get; set; }
    public string? ProductUrlTemplate { get; set; }
}
```

| Property | Required | Description |
|----------|----------|-------------|
| `Url` | Yes | URL to the data source website |
| `LicenseText` | Yes | Short license summary (e.g., `"CC0 1.0 (Public Domain)"`) |
| `Description` | No | Longer description for the settings/about page |
| `ProductUrlTemplate` | No | URL template with `{barcode}` placeholder for linking to product pages on the source site |

### AttributionMarkdown on Results

Each `ProductLookupResult` has an `AttributionMarkdown` property. Plugins set this in `LookupAsync` when generating results. The system stores the combined attribution markdown on the `Product` entity when applying lookup results.

**Why plugins generate their own attribution**: The plugin has the correct barcode (as returned by its API) for building URLs. This avoids broken links caused by barcode format differences between what the user scanned and what the API returned.

```csharp
// In your MapToLookupResult or LookupAsync:
result.AttributionMarkdown = $"Data from [{DisplayName}]({Attribution!.Url}) ({Attribution.LicenseText}).";

// With a product link:
var productUrl = $"https://myapi.example.com/product/{barcode}";
result.AttributionMarkdown = $"Data from [{DisplayName}]({Attribution!.Url}) ({Attribution.LicenseText}). [View product]({productUrl})";
```

During enrichment, if multiple plugins contribute to the same result, their attribution strings are concatenated:

```csharp
// In EnrichPipelineAsync:
if (!string.IsNullOrEmpty(result.AttributionMarkdown))
{
    existing.AttributionMarkdown = existing.AttributionMarkdown != null
        ? existing.AttributionMarkdown + "\n\n" + result.AttributionMarkdown
        : result.AttributionMarkdown;
}
```

The stored attribution is rendered as HTML on product pages using Markdig (with HTML disabled for safety).

### How Attribution Is Rendered

Attribution appears in two places in the UI:

1. **Product pages** — The stored `DataSourceAttribution` markdown is rendered as styled HTML below the product form. An accuracy disclaimer is automatically appended.
2. **Settings page** — The `DisplayName`, `Description`, `Url`, and `LicenseText` from the `Attribution` property are shown in the plugin information section.

### Examples from Built-in Plugins

**Open Food Facts** (ODbL + CC BY-SA):

```csharp
public PluginAttribution? Attribution => new()
{
    Url = "https://openfoodfacts.org",
    LicenseText = "Database: ODbL, Images: CC BY-SA",
    Description = "A free, open, collaborative database of food products from around the world. "
        + "Database contents are available under the Open Database License (ODbL). "
        + "Product images are available under the Creative Commons Attribution-ShareAlike "
        + "(CC BY-SA) license.",
    ProductUrlTemplate = "https://world.openfoodfacts.org/product/{barcode}"
};

// In MapToLookupResult:
result.AttributionMarkdown = BuildAttributionMarkdown(product.Code);

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
```

**USDA FoodData Central** (Public Domain):

```csharp
public PluginAttribution? Attribution => new()
{
    Url = "https://fdc.nal.usda.gov/",
    LicenseText = "CC0 1.0 (Public Domain)",
    Description = "U.S. Department of Agriculture, Agricultural Research Service, "
        + "Beltsville Human Nutrition Research Center. FoodData Central.",
    ProductUrlTemplate = null  // USDA doesn't have per-barcode product pages
};

// In MapToLookupResult:
result.AttributionMarkdown = $"Data from [{DisplayName}]({Attribution!.Url}) ({Attribution.LicenseText}).";
```

### When Attribution Is Null

Return `null` from `Attribution` only if your plugin generates data locally without fetching from any external source. Any plugin that queries an external API or database should provide attribution.

---

## Pipeline Context

`ProductLookupPipelineContext` is the shared state used during the enrichment phase of the pipeline. It is only accessed in `EnrichPipelineAsync`, never in `LookupAsync`.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Query` | `string` | The original search query (barcode or product name) |
| `SearchType` | `ProductLookupSearchType` | `Barcode` or `Name` |
| `MaxResults` | `int` | Maximum results requested (default 20) |
| `Results` | `List<ProductLookupResult>` | Accumulated results from previous plugins |

### Key Methods

#### AddResult / AddResults

Add new results to the pipeline:

```csharp
context.AddResult(new ProductLookupResult
{
    DataSources = { { DisplayName, externalId } },
    Name = "Product Name",
    Barcode = "012345678901"
});

// Or add multiple at once:
context.AddResults(myResults);
```

#### FindMatchingResult

Find an existing result to enrich rather than duplicate:

```csharp
// Match by barcode (handles UPC/EAN normalization automatically)
var existing = context.FindMatchingResult(barcode: "012345678901");

// Match by external ID and data source name
var existing = context.FindMatchingResult(
    externalId: "12345",
    dataSource: "USDA FoodData Central");
```

Barcode matching uses `NormalizeBarcode` internally, so `012345678901` (UPC-A with check) will match `0012345678901` (EAN-13) — you don't need to worry about format differences.

#### FindResultsByBarcode

Find all results matching a barcode (not just the first):

```csharp
foreach (var result in context.FindResultsByBarcode("012345678901"))
{
    // Enrich each matching result
}
```

### Barcode Utilities

The context provides static utility methods for barcode handling:

```csharp
// Normalize a barcode for comparison (strips check digits and leading zeros)
string normalized = ProductLookupPipelineContext.NormalizeBarcode("0012345678905");

// Validate a check digit
bool valid = ProductLookupPipelineContext.HasValidCheckDigit("012345678905", isEan: false);

// Calculate a check digit
char check = ProductLookupPipelineContext.CalculateCheckDigit("01234567890");

// Generate all format variants (EAN-13, UPC-A with check, UPC-A without check)
List<BarcodeVariant> variants = ProductLookupPipelineContext.GenerateBarcodeVariants("012345678905");
```

These are useful if you need to try multiple barcode formats against an external API that only accepts a specific format.

### Enrichment Pattern

The standard pattern for enriching vs. adding:

```csharp
// Try to find an existing result from a previous plugin
var existing = context.FindMatchingResult(barcode: barcode);

if (existing != null)
{
    // Enrich: only fill in what's missing (don't overwrite)
    existing.Nutrition ??= MapNutrition(apiResult);
    existing.ImageUrl ??= new ResultImage { ImageUrl = imageUrl, PluginId = DisplayName };
    existing.BrandName ??= apiResult.Brand;
    existing.DataSources.TryAdd(DisplayName, apiResult.ExternalId);

    // Always merge attribution
    if (!string.IsNullOrEmpty(result.AttributionMarkdown))
    {
        existing.AttributionMarkdown = existing.AttributionMarkdown != null
            ? existing.AttributionMarkdown + "\n\n" + result.AttributionMarkdown
            : result.AttributionMarkdown;
    }
}
else
{
    // Add as new result
    context.AddResult(MapToLookupResult(apiResult));
}
```

Use `??=` (null-coalescing assignment) to avoid overwriting data from earlier plugins. The convention is **first plugin to provide a value wins**.

---

## Result Models

### ProductLookupResult

The main result object. All fields are optional except `Name` and `DataSources`.

```csharp
public class ProductLookupResult
{
    // Required
    public Dictionary<string, string> DataSources { get; set; } = new();
    public string Name { get; set; } = string.Empty;

    // Product identification
    public string? Barcode { get; set; }
    public string? OriginalSearchBarcode { get; set; }
    public string? BrandName { get; set; }
    public string? BrandOwner { get; set; }
    public string? Description { get; set; }
    public List<string> Categories { get; set; } = new();

    // Media
    public ResultImage? ImageUrl { get; set; }
    public ResultImage? ThumbnailUrl { get; set; }

    // Nutrition
    public string? ServingSizeDescription { get; set; }
    public string? Ingredients { get; set; }
    public ProductLookupNutrition? Nutrition { get; set; }

    // Attribution
    public string? AttributionMarkdown { get; set; }

    // Extensibility
    public Dictionary<string, object>? AdditionalData { get; set; }
}
```

**`DataSources`**: A dictionary where the key is your plugin's `DisplayName` and the value is the external ID from your source. This tracks which plugins contributed to the result.

```csharp
DataSources = { { "My Nutrition API", "product-12345" } }
```

**`AttributionMarkdown`**: Markdown-formatted attribution text generated by the plugin. Set this in `LookupAsync` when creating results. During enrichment, attribution from multiple plugins is concatenated with double-newline separators.

**`AdditionalData`**: A free-form dictionary for plugin-specific data that doesn't fit the standard fields. For example, Open Food Facts stores Nutri-Score and NOVA group here:

```csharp
result.AdditionalData = new Dictionary<string, object>
{
    ["nutriscore_grade"] = "a",
    ["nova_group"] = 1
};
```

### ResultImage

```csharp
public class ResultImage
{
    public required string ImageUrl { get; set; }
    public required string PluginId { get; set; }
}
```

`PluginId` should be set to your plugin's `DisplayName` so the UI can show attribution for images.

### ProductLookupNutrition

Nutrition data per serving. Set `Source` to your `PluginId`.

```csharp
public class ProductLookupNutrition
{
    public required string Source { get; set; }
    public string? ExternalSourceId { get; set; }

    public decimal? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public decimal? ServingsPerContainer { get; set; }

    // Macronutrients
    public decimal? Calories { get; set; }
    public decimal? TotalFat { get; set; }
    public decimal? SaturatedFat { get; set; }
    public decimal? TransFat { get; set; }
    public decimal? Cholesterol { get; set; }
    public decimal? Sodium { get; set; }
    public decimal? TotalCarbohydrates { get; set; }
    public decimal? DietaryFiber { get; set; }
    public decimal? TotalSugars { get; set; }
    public decimal? AddedSugars { get; set; }
    public decimal? Protein { get; set; }

    // Vitamins
    public decimal? VitaminA { get; set; }
    public decimal? VitaminC { get; set; }
    public decimal? VitaminD { get; set; }
    // ... (VitaminE, VitaminK, Thiamin, Riboflavin, Niacin, VitaminB6, Folate, VitaminB12)

    // Minerals
    public decimal? Calcium { get; set; }
    public decimal? Iron { get; set; }
    public decimal? Magnesium { get; set; }
    public decimal? Phosphorus { get; set; }
    public decimal? Potassium { get; set; }
    public decimal? Zinc { get; set; }
}
```

Only populate the fields you have data for. Leave everything else as `null`.

---

## Configuration

### plugins/config.json

Plugins are configured in the `plugins/config.json` file located in the application's `plugins/` directory. The order of entries determines the pipeline execution order.

```json
{
  "plugins": [
    {
      "id": "usda",
      "enabled": true,
      "builtin": true,
      "displayName": "USDA FoodData Central",
      "config": {
        "apiKey": "YOUR_USDA_API_KEY",
        "baseUrl": "https://api.nal.usda.gov/fdc/v1/",
        "defaultMaxResults": 20
      }
    },
    {
      "id": "openfoodfacts",
      "enabled": true,
      "builtin": true,
      "displayName": "Open Food Facts",
      "config": {
        "baseUrl": "https://world.openfoodfacts.org"
      }
    },
    {
      "id": "mynutrition",
      "enabled": true,
      "builtin": false,
      "assembly": "MyNutritionPlugin.dll",
      "displayName": "My Nutrition API",
      "config": {
        "apiKey": "your-api-key-here"
      }
    }
  ]
}
```

### PluginConfigEntry Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | `string` | Must match the plugin's `PluginId` property |
| `enabled` | `bool` | Set to `false` to disable without removing |
| `builtin` | `bool` | `true` for compiled-in plugins, `false` for external DLLs |
| `assembly` | `string?` | Path to the DLL relative to the `plugins/` folder (external plugins only) |
| `displayName` | `string` | Human-readable name (informational — the plugin's `DisplayName` property is authoritative) |
| `config` | `object?` | Plugin-specific configuration passed to `InitAsync` as a `JsonElement` |

### Auto-Loading Behavior

If `plugins/config.json` does not exist, the application automatically loads all built-in plugins with default settings (no config passed). This means product lookup works out of the box — you only need to create `config.json` when you want to customize settings, change plugin order, or add external plugins.

---

## Registration

### Built-in Plugins

Built-in plugins are registered in `InfrastructureStartup.cs` as singleton services:

```csharp
// Register built-in plugins (order matters for pipeline - first registered runs first)
services.AddSingleton<IPlugin, UsdaFoodDataPlugin>();
services.AddSingleton<IPlugin, OpenFoodFactsPlugin>();

// Register plugin loader and lookup service
services.AddSingleton<IPluginLoader, PluginLoader>();
services.AddScoped<IProductLookupService, ProductLookupService>();
```

Built-in plugins receive constructor dependencies via DI (e.g., `IHttpClientFactory`, `ILogger<T>`).

### External Plugins (DLLs)

External plugins are loaded from DLL files at runtime. The plugin loader:

1. Reads the `assembly` path from `config.json`
2. Loads the assembly from the `plugins/` folder
3. Finds the first class implementing `IPlugin`
4. Creates an instance via `Activator.CreateInstance`
5. Calls `InitAsync` with the config

**Important**: Because external plugins are instantiated via `Activator.CreateInstance`, they must have a **parameterless constructor**. They cannot use constructor-based dependency injection. If you need an `HttpClient`, create one in the constructor or in `InitAsync`.

### Creating an External Plugin Project

```bash
dotnet new classlib -n MyNutritionPlugin -f net10.0
cd MyNutritionPlugin
```

Reference the shared Core package (which contains the plugin interfaces):

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Famick.HomeManagement.Core.csproj" />
  <!-- Or, if published as a NuGet package in the future: -->
  <!-- <PackageReference Include="Famick.HomeManagement.Core" Version="x.y.z" /> -->
</ItemGroup>
```

Build and copy the DLL to the `plugins/` folder:

```bash
dotnet build -c Release
cp bin/Release/net10.0/MyNutritionPlugin.dll /path/to/app/plugins/
```

---

## Store Integration Plugins

Store integration plugins implement `IStoreIntegrationPlugin` and provide OAuth-based connections to grocery store APIs (Kroger, Walmart, etc.) for pricing, availability, store location lookup, and shopping cart management.

Creating store integration plugins is significantly more complex than product lookup plugins — they require OAuth flows, token management, and multiple API integrations.

For the full store integration guide, see [STORE_INTEGRATIONS.md](./STORE_INTEGRATIONS.md).

---

## Testing

### Unit Testing Your Plugin

Test your plugin in isolation using the two-method pattern:

```csharp
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Xunit;

public class MyNutritionPluginTests
{
    [Fact]
    public async Task LookupAsync_BarcodeSearch_ReturnsResults()
    {
        // Arrange
        var plugin = new MyNutritionPlugin();
        await plugin.InitAsync(CreateTestConfig());

        // Act
        var results = await plugin.LookupAsync(
            "012345678901",
            ProductLookupSearchType.Barcode);

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal("My Nutrition API", results[0].DataSources.Keys.First());
        Assert.NotNull(results[0].AttributionMarkdown);
    }

    [Fact]
    public async Task EnrichPipelineAsync_EnrichesExistingResult()
    {
        // Arrange
        var plugin = new MyNutritionPlugin();
        await plugin.InitAsync(CreateTestConfig());

        var context = new ProductLookupPipelineContext(
            query: "012345678901",
            searchType: ProductLookupSearchType.Barcode);

        // Simulate a result from a previous plugin
        context.AddResult(new ProductLookupResult
        {
            DataSources = { { "Previous Plugin", "abc123" } },
            Name = "Test Product",
            Barcode = "012345678901",
            AttributionMarkdown = "Data from Previous Plugin."
        });

        // Get lookup results
        var lookupResults = await plugin.LookupAsync(
            "012345678901",
            ProductLookupSearchType.Barcode);

        // Act
        await plugin.EnrichPipelineAsync(context, lookupResults);

        // Assert
        Assert.Single(context.Results);  // Should enrich, not duplicate
        Assert.True(context.Results[0].DataSources.ContainsKey("My Nutrition API"));
        Assert.Contains("Previous Plugin", context.Results[0].AttributionMarkdown);
        Assert.Contains("My Nutrition API", context.Results[0].AttributionMarkdown);
    }

    [Fact]
    public async Task LookupAsync_NotAvailable_ReturnsEmpty()
    {
        // Arrange — don't provide required config
        var plugin = new MyNutritionPlugin();
        await plugin.InitAsync(null);

        // Act
        var results = await plugin.LookupAsync(
            "012345678901",
            ProductLookupSearchType.Barcode);

        // Assert
        Assert.Empty(results);
    }

    private static System.Text.Json.JsonElement CreateTestConfig()
    {
        var json = """{"apiKey": "test-key-123"}""";
        return System.Text.Json.JsonDocument.Parse(json).RootElement;
    }
}
```

### Integration Testing

For integration tests against a real API, use environment variables for API keys and mark tests appropriately:

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task LookupAsync_RealApi_ReturnsResults()
{
    var apiKey = Environment.GetEnvironmentVariable("MYNUTRITION_API_KEY");
    if (string.IsNullOrEmpty(apiKey))
    {
        // Skip if no API key is configured
        return;
    }

    var json = $$"""{"apiKey": "{{apiKey}}"}""";
    var config = System.Text.Json.JsonDocument.Parse(json).RootElement;

    var plugin = new MyNutritionPlugin();
    await plugin.InitAsync(config);

    var results = await plugin.LookupAsync(
        "049000042566",  // Coca-Cola Classic
        ProductLookupSearchType.Barcode);

    Assert.NotEmpty(results);
}
```

---

## Docker Deployment

For self-hosted Docker deployments, mount your plugin DLL and config using a Docker volume.

### Directory Structure

```
my-plugins/
├── config.json
└── MyNutritionPlugin.dll
```

### Docker Compose

```yaml
services:
  famick:
    image: famick/homemanagement:latest
    volumes:
      - ./my-plugins:/app/plugins
    # ... other configuration
```

The `plugins/` folder inside the container is at `/app/plugins`. Your volume mount replaces it entirely, so your `config.json` must include entries for any built-in plugins you want to keep enabled.

### Example config.json for Docker

```json
{
  "plugins": [
    {
      "id": "usda",
      "enabled": true,
      "builtin": true,
      "displayName": "USDA FoodData Central",
      "config": {
        "apiKey": "YOUR_USDA_API_KEY"
      }
    },
    {
      "id": "openfoodfacts",
      "enabled": true,
      "builtin": true,
      "displayName": "Open Food Facts"
    },
    {
      "id": "mynutrition",
      "enabled": true,
      "builtin": false,
      "assembly": "MyNutritionPlugin.dll",
      "displayName": "My Nutrition API",
      "config": {
        "apiKey": "your-api-key-here"
      }
    }
  ]
}
```

---

## Summary

| Step | Action |
|------|--------|
| 1 | Create a .NET class library targeting `net10.0` |
| 2 | Reference `Famick.HomeManagement.Core` |
| 3 | Implement `IProductLookupPlugin` with `LookupAsync` and `EnrichPipelineAsync` |
| 4 | Provide `PluginAttribution` if using external data |
| 5 | Set `AttributionMarkdown` on each `ProductLookupResult` in `LookupAsync` |
| 6 | Merge attribution in `EnrichPipelineAsync` when enriching existing results |
| 7 | Handle both `Barcode` and `Name` search types in `LookupAsync` |
| 8 | Use `FindMatchingResult` in `EnrichPipelineAsync` to enrich existing results instead of duplicating |
| 9 | Build the DLL and place it in the `plugins/` folder |
| 10 | Add an entry to `plugins/config.json` with `"builtin": false` and `"assembly"` pointing to your DLL |
| 11 | Restart the application |
