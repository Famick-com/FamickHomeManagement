namespace Famick.HomeManagement.Core.DTOs.StoreIntegrations;

/// <summary>
/// OAuth callback parameters
/// </summary>
public class OAuthCallbackRequest
{
    /// <summary>
    /// Authorization code from the OAuth provider
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// State parameter (contains encoded shopping location ID and CSRF token)
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Error code (if authorization failed)
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Error description (if authorization failed)
    /// </summary>
    public string? ErrorDescription { get; set; }
}
