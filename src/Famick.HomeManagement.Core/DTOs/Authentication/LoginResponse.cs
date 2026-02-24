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
}

/// <summary>
/// User information DTO
/// </summary>
public class UserDto
{
    /// <summary>
    /// User ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's first name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// User's preferred language code
    /// </summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>
    /// User's full name
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// User's permission list
    /// </summary>
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// Tenant information for authentication context
/// </summary>
public class TenantInfoDto
{
    /// <summary>
    /// Tenant ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant/organization name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tenant subdomain
    /// </summary>
    public string Subdomain { get; set; } = string.Empty;

    /// <summary>
    /// Subscription tier (Free, Pro, Enterprise)
    /// </summary>
    public string SubscriptionTier { get; set; } = string.Empty;
}
