namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Stores OAuth tokens shared across all stores for a specific tenant and integration.
/// Each tenant has at most one token per integration plugin (e.g., one Kroger token per tenant).
/// </summary>
public class TenantIntegrationToken : BaseTenantEntity
{
    /// <summary>
    /// The plugin ID (e.g., "kroger", "walmart")
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth access token (should be encrypted at rest)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// OAuth refresh token (should be encrypted at rest)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// When the access token expires
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When the token was last successfully refreshed
    /// </summary>
    public DateTime? LastRefreshedAt { get; set; }

    /// <summary>
    /// Whether token refresh has failed and re-authentication is required.
    /// When true, the user must go through the OAuth flow again.
    /// </summary>
    public bool RequiresReauth { get; set; }
}
