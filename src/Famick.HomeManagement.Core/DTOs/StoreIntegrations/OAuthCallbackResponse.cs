namespace Famick.HomeManagement.Core.DTOs.StoreIntegrations;

/// <summary>
/// Response after completing OAuth callback
/// </summary>
public class OAuthCallbackResponse
{
    /// <summary>
    /// Whether the OAuth flow completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The shopping location that was connected
    /// </summary>
    public Guid ShoppingLocationId { get; set; }
}

/// <summary>
/// Response containing OAuth authorization URL
/// </summary>
public class OAuthAuthorizeResponse
{
    /// <summary>
    /// URL to redirect the user to for OAuth authorization
    /// </summary>
    public string AuthorizationUrl { get; set; } = string.Empty;
}
