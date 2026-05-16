namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response after successful authentication
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// JWT access token (short-lived, 60 minutes)
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for obtaining new access tokens (7 days)
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// When the access token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Indicates the user must change their password before accessing other endpoints
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Indicates the user must accept Terms of Service and Privacy Policy before accessing the app.
    /// Only applies to cloud deployments.
    /// </summary>
    public bool MustAcceptTerms { get; set; }

    /// <summary>
    /// Authenticated user information
    /// </summary>
    public UserDto User { get; set; } = null!;

    /// <summary>
    /// User's tenant information
    /// </summary>
    public TenantInfoDto Tenant { get; set; } = null!;

    /// <summary>
    /// Phase 4 chunk 4.D — Canonical (scheme://host[:port]) URL of the
    /// self-hosted server the user authenticated against. Null on cloud
    /// logins. Mobile clients store this and prompt on change (compared
    /// against the prior value the server recorded; see
    /// <see cref="Famick.HomeManagement.Domain.Entities.User.LastDeliveredLocalServer"/>).
    /// Omitted from JSON when null so older mobile builds ignore it.
    /// </summary>
    public string? LocalServer { get; set; }
}

/// <summary>
/// User information DTO
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PreferredLanguage { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// Tenant information for authentication context.
/// Includes subscription state for client-side feature gating.
/// </summary>
public class TenantInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string SubscriptionTier { get; set; } = string.Empty;
    public bool IsTrialActive { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public bool IsExpired { get; set; }
}
