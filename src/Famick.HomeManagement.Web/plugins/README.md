# Home Management Plugins

This folder contains product lookup plugins for the Home Management application.

## Quick Start

1. Copy `config.example.json` to `config.json`
2. Add your API keys to `config.json`
3. Restart the application

```bash
cp config.example.json config.json
# Edit config.json with your API keys
```

> **Note:** `config.json` is gitignored to prevent committing API keys.

## Plugin Configuration

Plugins are configured in `config.json`. Each plugin entry has the following properties:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | string | Yes | Unique plugin identifier |
| `enabled` | boolean | Yes | Whether the plugin is active |
| `builtin` | boolean | Yes | `true` for built-in plugins, `false` for external DLLs |
| `assembly` | string | No | Path to DLL (relative to plugins folder, required for external plugins) |
| `displayName` | string | Yes | Human-readable plugin name |
| `config` | object | No | Plugin-specific configuration |

## Built-in Plugins

### USDA FoodData Central (`usda`)

Searches the USDA FoodData Central database for food products and nutrition information.

**Configuration:**
```json
{
  "apiKey": "YOUR_API_KEY",
  "baseUrl": "https://api.nal.usda.gov/fdc/v1/",
  "defaultMaxResults": 20
}
```

To get an API key, register at: https://fdc.nal.usda.gov/api-key-signup.html

## External Plugins

External plugins are DLL files placed in the `external/` subfolder. To add an external plugin:

1. Create a folder for your plugin: `external/myplugin/`
2. Place the plugin DLL: `external/myplugin/MyPlugin.dll`
3. Add an entry to `config.json`:

```json
{
  "id": "myplugin",
  "enabled": true,
  "builtin": false,
  "assembly": "external/myplugin/MyPlugin.dll",
  "displayName": "My Custom Plugin",
  "config": {
    "setting1": "value1"
  }
}
```

## Developing Plugins

To create a custom plugin, implement the `IProductLookupPlugin` interface.

### Pipeline Architecture

The plugin pipeline runs in two phases for optimal performance:

1. **Parallel Lookup** - All plugins fetch data from their external APIs concurrently via `LookupAsync`. Each plugin receives the search query and returns a list of `ProductLookupResult`. Plugins must NOT access the pipeline context during this phase.

2. **Sequential Enrichment** - After all lookups complete, each plugin's `EnrichPipelineAsync` is called in config.json order. This phase merges lookup results into the shared pipeline context using the "first plugin wins" pattern (`??=`).

### Interface

```csharp
public interface IProductLookupPlugin : IPlugin
{
    // Properties from IPlugin:
    // string PluginId { get; }
    // string DisplayName { get; }
    // string Version { get; }
    // bool IsAvailable { get; }
    // Task InitAsync(JsonElement? pluginConfig, CancellationToken ct = default);

    /// Fetch product data from the external source.
    /// Runs in parallel across all plugins.
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

### Quick Start Example

```csharp
public class MyNutritionPlugin : IProductLookupPlugin
{
    public string PluginId => "mynutrition";
    public string DisplayName => "My Nutrition API";
    public string Version => "1.0.0";
    public bool IsAvailable => true;

    public PluginAttribution? Attribution => new()
    {
        Url = "https://mynutritionapi.example.com",
        LicenseText = "CC BY 4.0"
    };

    public Task InitAsync(JsonElement? pluginConfig, CancellationToken ct = default)
        => Task.CompletedTask;

    public async Task<List<ProductLookupResult>> LookupAsync(
        string query, ProductLookupSearchType searchType,
        int maxResults = 20, CancellationToken ct = default)
    {
        // Call your external API here — this runs in parallel with other plugins
        var apiResults = await _httpClient.GetAsync($"/search?q={query}", ct);
        // Map API response to List<ProductLookupResult> and return
        // Set AttributionMarkdown on each result:
        // result.AttributionMarkdown = $"Data from [{DisplayName}]({Attribution!.Url}) ({Attribution.LicenseText}).";
        return mappedResults;
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

                // Merge attribution markdown
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
}
```

### Plugin Requirements

1. Reference `Famick.HomeManagement.Core` to get the interface definition
2. Implement a parameterless constructor (DI is not available for external plugins)
3. Handle your own HTTP client creation and configuration
4. Return empty list from `LookupAsync` on errors (don't throw exceptions)
5. Do NOT access the pipeline context from `LookupAsync` - it runs in parallel

## Docker Mounting

To add external plugins via Docker:

```yaml
volumes:
  - ./my-plugins:/app/plugins/external:ro
```
