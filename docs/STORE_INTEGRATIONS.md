# Store Integration Development Guide

This document describes how to create and integrate store plugins with the HomeManagement application.

## Overview

Store integrations allow users to connect their HomeManagement instance to external store APIs (like Kroger, Walmart, etc.) for:
- Searching for store locations near them
- Looking up product prices and availability
- Adding items to their store's shopping cart
- Downloading product images for local storage

Some store integrations require OAuth authentication (like Kroger), while others work with public APIs that don't require user authentication. Plugins declare whether OAuth is required via the `RequiresOAuth` capability flag.

For OAuth-enabled plugins, tokens are shared per tenant/integration, meaning if a user authenticates once with Kroger, all their stores using Kroger share that same token.

## Architecture

### Token Management

OAuth tokens are stored in the `TenantIntegrationTokens` table, keyed by `(TenantId, PluginId)`. This means:
- One OAuth connection per tenant per integration
- All stores using the same integration share the same token
- Token refresh is automatic with fallback to re-authentication

### Plugin Interface

All store integration plugins implement `IStoreIntegrationPlugin`:

```csharp
public interface IStoreIntegrationPlugin
{
    // Identity
    string PluginId { get; }           // e.g., "kroger"
    string DisplayName { get; }        // e.g., "Kroger Family of Stores"
    string Version { get; }
    bool IsAvailable { get; }

    // Capabilities
    StoreIntegrationCapabilities Capabilities { get; }

    // Initialization
    Task InitAsync(JsonElement? pluginConfig, CancellationToken ct);

    // OAuth Methods
    string GetAuthorizationUrl(string redirectUri, string state);
    Task<OAuthTokenResult> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken ct);
    Task<OAuthTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct);

    // Store Location Methods
    Task<List<StoreLocationResult>> SearchStoresByZipAsync(string zipCode, int radiusMiles, CancellationToken ct);
    Task<List<StoreLocationResult>> SearchStoresByCoordinatesAsync(double lat, double lon, int radiusMiles, CancellationToken ct);

    // Product Methods
    Task<List<StoreProductResult>> SearchProductsAsync(string accessToken, string storeLocationId, string query, int maxResults, CancellationToken ct);
    Task<StoreProductResult?> GetProductAsync(string accessToken, string storeLocationId, string productId, CancellationToken ct);
    Task<StoreProductResult?> LookupProductByBarcodeAsync(string? accessToken, string storeLocationId, string barcode, CancellationToken ct);

    // Shopping Cart Methods
    Task<ShoppingCartResult?> GetShoppingCartAsync(string accessToken, string storeLocationId, CancellationToken ct);
    Task<ShoppingCartResult?> AddToCartAsync(string accessToken, string storeLocationId, List<CartItemRequest> items, CancellationToken ct);
    Task<ShoppingCartResult?> UpdateCartItemAsync(string accessToken, string storeLocationId, string productId, int quantity, CancellationToken ct);
    Task<ShoppingCartResult?> RemoveFromCartAsync(string accessToken, string storeLocationId, string productId, CancellationToken ct);
}
```

### Capabilities

Each plugin declares its capabilities via `StoreIntegrationCapabilities`:

```csharp
public class StoreIntegrationCapabilities
{
    public bool RequiresOAuth { get; set; }           // Whether OAuth is required (false = always connected)
    public bool HasProductLookup { get; set; }        // Can search products by name/barcode
    public bool HasStoreProductLookup { get; set; }   // Can search products at specific store
    public bool HasShoppingCart { get; set; }         // Can add items to cart
    public bool CanReadShoppingCart { get; set; }     // Can read cart contents
    public bool CanDownloadProductImages { get; set; } // Can download product images
}
```

**Note:** If `RequiresOAuth` is `false`, the plugin is always considered "connected" and no OAuth flow is triggered.

## Plugin Loading Process

The `StoreIntegrationLoader` loads plugins from `plugins/config.json` at application startup:

1. Reads the `storeIntegrations` array from `plugins/config.json`
2. For each entry (in order):
   - If `enabled` is `false`, the plugin is skipped
   - If `builtin` is `true`, looks up the plugin from DI-registered instances by `id`
   - If `builtin` is `false`, loads the plugin from the specified `assembly` DLL
3. Calls `InitAsync()` on each loaded plugin, passing the `config` object
4. Plugin becomes available if `IsAvailable` returns `true` after initialization

**Important:** Plugins are loaded and processed in the order they appear in the config.json array.

## Creating a New Plugin

### Step 1: Create Plugin Class

Create a new class that implements `IStoreIntegrationPlugin`:

```csharp
public class MyStorePlugin : IStoreIntegrationPlugin
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MyStorePlugin> _logger;
    private string? _clientId;
    private string? _clientSecret;

    public string PluginId => "mystore";
    public string DisplayName => "My Store";
    public string Version => "1.0.0";
    public bool IsAvailable => !string.IsNullOrEmpty(_clientId) && !string.IsNullOrEmpty(_clientSecret);

    public StoreIntegrationCapabilities Capabilities => new()
    {
        RequiresOAuth = true,     // Set to false if your store uses public APIs
        HasProductLookup = true,
        HasStoreProductLookup = true,
        HasShoppingCart = false,  // Set to true if your store supports cart
        CanReadShoppingCart = false,
        CanDownloadProductImages = true
    };

    public MyStorePlugin(IHttpClientFactory httpClientFactory, ILogger<MyStorePlugin> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    public Task InitAsync(JsonElement? pluginConfig, CancellationToken ct)
    {
        if (pluginConfig.HasValue)
        {
            var config = pluginConfig.Value;
            if (config.TryGetProperty("clientId", out var clientId))
                _clientId = clientId.GetString();
            if (config.TryGetProperty("clientSecret", out var secret))
                _clientSecret = secret.GetString();
        }
        return Task.CompletedTask;
    }

    // ... implement other methods
}
```

### Step 2: Add API Models

Create a models file for your store's API responses:

```csharp
public class MyStoreTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

// ... add other model classes for your API
```

### Step 3: Register Built-in Plugin (Optional)

For built-in plugins (compiled into the application), register via dependency injection in `InfrastructureStartup.cs`:

```csharp
// Register built-in store integration plugins
services.AddSingleton<IStoreIntegrationPlugin, KrogerStorePlugin>();
services.AddSingleton<IStoreIntegrationPlugin, MyStorePlugin>();  // Add your plugin here
```

For external plugins (separate DLL files), skip this step - they are loaded dynamically from the assembly path specified in config.json.

### Step 4: Configure Plugin in config.json

Add an entry to the `storeIntegrations` array in `plugins/config.json`. Plugins are loaded in the order they appear in this array.

**For built-in plugins:**

```json
{
  "storeIntegrations": [
    {
      "id": "kroger",
      "enabled": true,
      "builtin": true,
      "displayName": "Kroger Family of Stores",
      "config": {
        "clientId": "your-client-id",
        "clientSecret": "your-client-secret",
        "scope": "product.compact"
      }
    },
    {
      "id": "mystore",
      "enabled": true,
      "builtin": true,
      "displayName": "My Store",
      "config": {
        "clientId": "your-client-id",
        "clientSecret": "your-client-secret"
      }
    }
  ]
}
```

**For external plugins (loaded from DLL):**

```json
{
  "storeIntegrations": [
    {
      "id": "mystore",
      "enabled": true,
      "builtin": false,
      "assembly": "MyStore.Plugin.dll",
      "displayName": "My Store",
      "config": {
        "clientId": "your-client-id",
        "clientSecret": "your-client-secret"
      }
    }
  ]
}
```

**Configuration entry fields:**

| Field | Required | Description |
|-------|----------|-------------|
| `id` | Yes | Plugin ID (must match `PluginId` property in the plugin class) |
| `enabled` | Yes | Set to `true` to load the plugin, `false` to skip |
| `builtin` | Yes | `true` for plugins registered via DI, `false` for external DLLs |
| `assembly` | No* | Path to DLL file (relative to plugins folder). *Required if `builtin` is `false` |
| `displayName` | No | Human-readable name (overrides plugin's `DisplayName`) |
| `config` | No | Plugin-specific configuration (passed to `InitAsync`) |

## OAuth Flow

### Authorization URL

When a user wants to connect their account:

1. Call `GetAuthorizationUrl(redirectUri, state)`
2. Redirect user to the returned URL
3. Store API redirects back with authorization code

### Token Exchange

After receiving the callback:

1. Parse the authorization code from callback
2. Call `ExchangeCodeForTokenAsync(code, redirectUri)`
3. Tokens are automatically stored in `TenantIntegrationTokens`

### Token Refresh

Token refresh is handled automatically by `StoreIntegrationService`:

1. Before any API call, `GetAccessTokenAsync(pluginId)` is called
2. If token is expired (5 min buffer), automatic refresh is attempted
3. If refresh fails, `RequiresReauth` flag is set
4. User sees "Reconnect" option in UI

## Product Images

For self-hosted deployments, product images can be downloaded locally:

```csharp
// In your service code
var fileName = await _fileStorageService.DownloadAndSaveProductImageAsync(
    productId,
    imageUrl,
    "kroger",  // source identifier
    ct);

if (fileName != null)
{
    // Create local image record
    var productImage = new ProductImage
    {
        ProductId = productId,
        FileName = fileName,
        ExternalSource = "kroger"
    };
}
```

Images are stored in: `wwwroot/uploads/products/{productId}/{source}_{timestamp}_{random}.{ext}`

## Example: Kroger Plugin

See `Infrastructure/Plugins/Kroger/KrogerStorePlugin.cs` for a complete implementation example:

- OAuth 2.0 with client credentials for public operations
- User OAuth for authenticated operations
- Store location search by ZIP code or coordinates
- Product search and barcode lookup
- Shopping cart operations
- Image URL extraction

## Testing Plugins

When testing your plugin:

1. Ensure API credentials are configured in `plugins/config.json`
2. Verify `IsAvailable` returns true after initialization
3. Test OAuth flow with a test user account
4. Verify token refresh works correctly
5. Test each capability you've declared

## Error Handling

- Return `OAuthTokenResult.Fail(errorMessage)` for OAuth failures
- Throw `InvalidOperationException` for configuration issues
- Return `null` for "not found" scenarios in product/cart operations
- Log all errors with appropriate context

## Security Considerations

- Client secrets should be stored securely (not in source control)
- OAuth state parameters should be validated
- Tokens are stored per-tenant, never shared across tenants
- Use HTTPS for all API calls
