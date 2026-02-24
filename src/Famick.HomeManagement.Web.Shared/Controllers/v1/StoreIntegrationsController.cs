using Famick.HomeManagement.Core.DTOs.StoreIntegrations;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// Controller for managing store integrations (OAuth, store linking, product-store linking)
/// </summary>
[ApiController]
[Route("api/v1/storeintegrations")]
[Authorize]
public class StoreIntegrationsController : ApiControllerBase
{
    private readonly IStoreIntegrationService _storeIntegrationService;
    private readonly ITenantService _tenantService;

    public StoreIntegrationsController(
        IStoreIntegrationService storeIntegrationService,
        ITenantService tenantService,
        ITenantProvider tenantProvider,
        ILogger<StoreIntegrationsController> logger)
        : base(tenantProvider, logger)
    {
        _storeIntegrationService = storeIntegrationService;
        _tenantService = tenantService;
    }

    #region Plugin Discovery

    /// <summary>
    /// Get all available store integration plugins
    /// </summary>
    [HttpGet("plugins")]
    [ProducesResponseType(typeof(List<StoreIntegrationPluginInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailablePlugins(CancellationToken ct)
    {
        var plugins = await _storeIntegrationService.GetAvailablePluginsAsync(ct);
        return ApiResponse(plugins);
    }

    /// <summary>
    /// Get the list of disabled product lookup plugin IDs for the current tenant
    /// </summary>
    [HttpGet("plugins/disabled")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDisabledPlugins(CancellationToken ct)
    {
        var disabledIds = await _tenantService.GetDisabledPluginIdsAsync(ct);
        return ApiResponse(disabledIds);
    }

    /// <summary>
    /// Set the list of disabled product lookup plugin IDs for the current tenant (admin only)
    /// </summary>
    [HttpPost("plugins/disabled")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetDisabledPlugins(
        [FromBody] List<string> disabledIds,
        CancellationToken ct)
    {
        await _tenantService.SetDisabledPluginIdsAsync(disabledIds, ct);
        return NoContent();
    }

    #endregion

    #region OAuth Flow

    /// <summary>
    /// Get the OAuth authorization URL for a plugin
    /// </summary>
    /// <param name="pluginId">Plugin identifier (e.g., "kroger")</param>
    /// <param name="shoppingLocationId">Shopping location to link</param>
    /// <param name="redirectUri">OAuth redirect URI</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("oauth/authorize/{pluginId}")]
    [ProducesResponseType(typeof(OAuthAuthorizeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAuthorizationUrl(
        string pluginId,
        [FromQuery] Guid shoppingLocationId,
        [FromQuery] string redirectUri,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "pluginId", new[] { "Plugin ID is required" } }
            });
        }

        if (shoppingLocationId == Guid.Empty)
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "shoppingLocationId", new[] { "Shopping location ID is required" } }
            });
        }

        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "redirectUri", new[] { "Redirect URI is required" } }
            });
        }

        try
        {
            var authorizationUrl = await _storeIntegrationService.GetAuthorizationUrlAsync(
                pluginId,
                shoppingLocationId,
                redirectUri,
                ct);

            return ApiResponse(new OAuthAuthorizeResponse { AuthorizationUrl = authorizationUrl });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to get authorization URL for plugin {PluginId}", pluginId);
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Handle OAuth callback (complete the authorization flow)
    /// </summary>
    /// <param name="pluginId">Plugin identifier</param>
    /// <param name="request">OAuth callback parameters</param>
    /// <param name="redirectUri">Original redirect URI used in authorization</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost("oauth/callback/{pluginId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(OAuthCallbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> HandleOAuthCallback(
        string pluginId,
        [FromBody] OAuthCallbackRequest request,
        [FromQuery] string redirectUri,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.Error))
        {
            _logger.LogWarning("OAuth authorization denied: {Error} - {Description}",
                request.Error, request.ErrorDescription);
            return ErrorResponse($"Authorization denied: {request.ErrorDescription ?? request.Error}");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "code", new[] { "Authorization code is required" } }
            });
        }

        if (string.IsNullOrWhiteSpace(request.State))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "state", new[] { "State parameter is required" } }
            });
        }

        // Parse the state to get shopping location ID
        // State format: base64("{shoppingLocationId}|{randomGuid}")
        Guid shoppingLocationId;
        try
        {
            var decodedState = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.State));
            var parts = decodedState.Split('|');
            if (parts.Length < 1 || !Guid.TryParse(parts[0], out shoppingLocationId))
            {
                return ValidationErrorResponse(new Dictionary<string, string[]>
                {
                    { "state", new[] { "Invalid state parameter" } }
                });
            }
        }
        catch (FormatException)
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "state", new[] { "Invalid state parameter" } }
            });
        }

        try
        {
            var success = await _storeIntegrationService.CompleteOAuthFlowAsync(
                pluginId,
                shoppingLocationId,
                request.Code,
                redirectUri,
                ct);

            return ApiResponse(new OAuthCallbackResponse
            {
                Success = success,
                ShoppingLocationId = shoppingLocationId
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to complete OAuth flow for plugin {PluginId}", pluginId);
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Disconnect a store integration (remove OAuth tokens)
    /// </summary>
    /// <param name="shoppingLocationId">Shopping location ID</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost("shoppinglocations/{shoppingLocationId:guid}/disconnect")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisconnectIntegration(
        Guid shoppingLocationId,
        CancellationToken ct)
    {
        try
        {
            // With shared tokens, "disconnect" now just unlinks the store from the integration.
            // OAuth tokens are managed at the plugin level via DisconnectPluginAsync.
            await _storeIntegrationService.UnlinkStoreLocationAsync(shoppingLocationId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to disconnect integration for shopping location {Id}", shoppingLocationId);
            return NotFoundResponse("Shopping location not found");
        }
    }

    /// <summary>
    /// Disconnect OAuth for a plugin (affects all stores using this plugin)
    /// </summary>
    /// <param name="pluginId">Plugin identifier</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost("plugins/{pluginId}/disconnect")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DisconnectPlugin(
        string pluginId,
        CancellationToken ct)
    {
        await _storeIntegrationService.DisconnectPluginAsync(pluginId, ct);
        return NoContent();
    }

    #endregion

    #region Store Location

    /// <summary>
    /// Search for store locations by ZIP code or coordinates
    /// </summary>
    [HttpGet("stores/search")]
    [ProducesResponseType(typeof(List<StoreLocationResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchStores(
        [FromQuery] StoreSearchRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PluginId))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "pluginId", new[] { "Plugin ID is required" } }
            });
        }

        if (string.IsNullOrWhiteSpace(request.ZipCode) && !request.Latitude.HasValue)
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "search", new[] { "Either ZIP code or coordinates are required" } }
            });
        }

        try
        {
            var results = await _storeIntegrationService.SearchStoresAsync(request, ct);
            return ApiResponse(results);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to search stores for plugin {PluginId}", request.PluginId);
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Link a shopping location to an external store
    /// </summary>
    [HttpPost("shoppinglocations/{shoppingLocationId:guid}/link")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkStoreLocation(
        Guid shoppingLocationId,
        [FromBody] LinkStoreLocationRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PluginId))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "pluginId", new[] { "Plugin ID is required" } }
            });
        }

        if (string.IsNullOrWhiteSpace(request.ExternalLocationId))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "externalLocationId", new[] { "External location ID is required" } }
            });
        }

        try
        {
            var success = await _storeIntegrationService.LinkStoreLocationAsync(
                shoppingLocationId,
                request,
                ct);

            if (!success)
            {
                return NotFoundResponse("Shopping location not found");
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to link store location {Id}", shoppingLocationId);
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Unlink a shopping location from its external store
    /// </summary>
    [HttpDelete("shoppinglocations/{shoppingLocationId:guid}/link")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkStoreLocation(
        Guid shoppingLocationId,
        CancellationToken ct)
    {
        try
        {
            await _storeIntegrationService.UnlinkStoreLocationAsync(shoppingLocationId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to unlink store location {Id}", shoppingLocationId);
            return NotFoundResponse("Shopping location not found");
        }
    }

    #endregion

    #region Product Operations

    /// <summary>
    /// Search for products at a linked store
    /// </summary>
    [HttpGet("shoppinglocations/{shoppingLocationId:guid}/products/search")]
    [ProducesResponseType(typeof(List<StoreProductResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchProductsAtStore(
        Guid shoppingLocationId,
        [FromQuery] StoreProductSearchRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "query", new[] { "Search query is required" } }
            });
        }

        try
        {
            var results = await _storeIntegrationService.SearchProductsAtStoreAsync(
                shoppingLocationId,
                request,
                ct);

            return ApiResponse(results);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to search products at store {Id}", shoppingLocationId);
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Link a product to a store product
    /// </summary>
    [HttpPost("products/{productId:guid}/stores/{shoppingLocationId:guid}/link")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ProductStoreMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkProductToStore(
        Guid productId,
        Guid shoppingLocationId,
        [FromBody] LinkProductToStoreRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalProductId))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "externalProductId", new[] { "External product ID is required" } }
            });
        }

        try
        {
            var result = await _storeIntegrationService.LinkProductToStoreAsync(
                productId,
                shoppingLocationId,
                request,
                ct);

            if (result == null)
            {
                return NotFoundResponse("Product or shopping location not found");
            }

            return ApiResponse(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to link product {ProductId} to store {LocationId}",
                productId, shoppingLocationId);
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Get product-store metadata
    /// </summary>
    [HttpGet("products/{productId:guid}/stores/{shoppingLocationId:guid}")]
    [ProducesResponseType(typeof(ProductStoreMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductStoreMetadata(
        Guid productId,
        Guid shoppingLocationId,
        CancellationToken ct)
    {
        var result = await _storeIntegrationService.GetProductStoreMetadataAsync(
            productId,
            shoppingLocationId,
            ct);

        if (result == null)
        {
            return NotFoundResponse("Product-store link not found");
        }

        return ApiResponse(result);
    }

    /// <summary>
    /// Get all store metadata for a product
    /// </summary>
    [HttpGet("products/{productId:guid}/stores")]
    [ProducesResponseType(typeof(List<ProductStoreMetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllProductMetadata(
        Guid productId,
        CancellationToken ct)
    {
        var results = await _storeIntegrationService.GetAllProductMetadataAsync(productId, ct);
        return ApiResponse(results);
    }

    /// <summary>
    /// Get all product metadata for a store
    /// </summary>
    [HttpGet("shoppinglocations/{shoppingLocationId:guid}/products")]
    [ProducesResponseType(typeof(List<ProductStoreMetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllStoreMetadata(
        Guid shoppingLocationId,
        CancellationToken ct)
    {
        var results = await _storeIntegrationService.GetAllStoreMetadataAsync(shoppingLocationId, ct);
        return ApiResponse(results);
    }

    /// <summary>
    /// Refresh product price/availability from the store API
    /// </summary>
    [HttpPost("products/{productId:guid}/stores/{shoppingLocationId:guid}/refresh")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ProductStoreMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefreshProductMetadata(
        Guid productId,
        Guid shoppingLocationId,
        CancellationToken ct)
    {
        try
        {
            var result = await _storeIntegrationService.RefreshProductMetadataAsync(
                productId,
                shoppingLocationId,
                ct);

            if (result == null)
            {
                return NotFoundResponse("Product-store link not found");
            }

            return ApiResponse(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to refresh product {ProductId} metadata at store {LocationId}",
                productId, shoppingLocationId);
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Unlink a product from a store
    /// </summary>
    [HttpDelete("products/{productId:guid}/stores/{shoppingLocationId:guid}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnlinkProductFromStore(
        Guid productId,
        Guid shoppingLocationId,
        CancellationToken ct)
    {
        await _storeIntegrationService.UnlinkProductFromStoreAsync(productId, shoppingLocationId, ct);
        return NoContent();
    }

    #endregion
}
