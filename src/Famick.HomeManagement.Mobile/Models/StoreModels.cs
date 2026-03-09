namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// Full shopping location detail matching ShoppingLocationDto from the API.
/// </summary>
public class ShoppingLocationDetail
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IntegrationType { get; set; }
    public bool IsConnected { get; set; }
    public bool HasIntegration => !string.IsNullOrEmpty(IntegrationType);
    public string? ExternalLocationId { get; set; }
    public string? ExternalChainId { get; set; }
    public string? StoreAddress { get; set; }
    public string? StorePhone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int ProductCount { get; set; }
}

/// <summary>
/// Available store integration plugin info.
/// </summary>
public class StoreIntegrationPlugin
{
    public string PluginId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsConnected { get; set; }
    public bool RequiresReauth { get; set; }
}

/// <summary>
/// Store search result from integration plugin.
/// </summary>
public class StoreSearchResult
{
    public string ExternalLocationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FullAddress { get; set; }
    public string? Phone { get; set; }
    public string? ChainId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>
/// Request to create a shopping location.
/// </summary>
public class CreateStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? StoreAddress { get; set; }
    public string? StorePhone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>
/// Request to update a shopping location.
/// </summary>
public class UpdateStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? StoreAddress { get; set; }
    public string? StorePhone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>
/// Request to link a shopping location to an external store.
/// </summary>
public class LinkStoreRequest
{
    public string PluginId { get; set; } = string.Empty;
    public string ExternalLocationId { get; set; } = string.Empty;
    public string? ExternalChainId { get; set; }
    public string? StoreName { get; set; }
    public string? StoreAddress { get; set; }
    public string? StorePhone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>
/// Result of a store OAuth flow attempt.
/// </summary>
public class StoreOAuthResult
{
    public bool Success { get; set; }
    public bool WasCancelled { get; set; }
    public string? ErrorMessage { get; set; }

    public static StoreOAuthResult Succeeded() => new() { Success = true };
    public static StoreOAuthResult Cancelled() => new() { WasCancelled = true, ErrorMessage = "Authentication was cancelled" };
    public static StoreOAuthResult Failed(string message) => new() { ErrorMessage = message };
}

/// <summary>
/// OAuth authorization URL response.
/// </summary>
public class StoreOAuthAuthorizeResponse
{
    public string AuthorizationUrl { get; set; } = string.Empty;
}

/// <summary>
/// OAuth callback response.
/// </summary>
public class StoreOAuthCallbackResponse
{
    public bool Success { get; set; }
    public Guid ShoppingLocationId { get; set; }
}

/// <summary>
/// OAuth callback request sent to the server.
/// </summary>
public class StoreOAuthCallbackRequest
{
    public string Code { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}
