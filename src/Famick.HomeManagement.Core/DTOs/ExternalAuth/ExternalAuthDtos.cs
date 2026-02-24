namespace Famick.HomeManagement.Core.DTOs.ExternalAuth;

/// <summary>
/// Information about an available external authentication provider
/// </summary>
public class ExternalAuthProviderDto
{
    /// <summary>
    /// Provider identifier (e.g., "Google", "Apple", "OIDC")
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the provider (e.g., "Sign in with Google")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this provider is enabled and configured
    /// </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Response containing OAuth authorization URL
/// </summary>
public class ExternalAuthChallengeResponse
{
    /// <summary>
    /// The OAuth authorization URL to redirect the user to
    /// </summary>
    public string AuthorizationUrl { get; set; } = string.Empty;

    /// <summary>
    /// State parameter for CSRF protection
    /// </summary>
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// Request to process an OAuth callback
/// </summary>
public class ExternalAuthCallbackRequest
{
    /// <summary>
    /// Authorization code from the OAuth provider
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// State parameter for CSRF verification
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Whether to extend the refresh token expiration
    /// </summary>
    public bool RememberMe { get; set; }
}

/// <summary>
/// Request to link an external provider to an existing account (for verifying link)
/// </summary>
public class ExternalAuthLinkRequest
{
    /// <summary>
    /// Authorization code from the OAuth provider
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// State parameter for CSRF verification
    /// </summary>
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// Request to initiate linking an external provider (for generating challenge URL)
/// </summary>
public class ExternalAuthLinkChallengeRequest
{
    /// <summary>
    /// Callback URL for the OAuth flow
    /// </summary>
    public string CallbackUrl { get; set; } = string.Empty;
}

/// <summary>
/// Authentication configuration settings for the frontend
/// </summary>
public class AuthConfigurationDto
{
    /// <summary>
    /// Whether password-based authentication is enabled
    /// </summary>
    public bool PasswordAuthEnabled { get; set; } = true;

    /// <summary>
    /// Whether passkey authentication is enabled
    /// </summary>
    public bool PasskeyEnabled { get; set; }

    /// <summary>
    /// List of enabled external authentication providers
    /// </summary>
    public List<ExternalAuthProviderDto> Providers { get; set; } = [];
}

/// <summary>
/// Request for native Apple Sign in (iOS)
/// </summary>
public class NativeAppleSignInRequest
{
    /// <summary>
    /// The identity token (JWT) from Apple's native Sign in with Apple
    /// </summary>
    public string IdentityToken { get; set; } = string.Empty;

    /// <summary>
    /// The authorization code from Apple (can be used for server-to-server validation)
    /// </summary>
    public string? AuthorizationCode { get; set; }

    /// <summary>
    /// User's full name (only provided on first sign in)
    /// </summary>
    public AppleUserName? FullName { get; set; }

    /// <summary>
    /// User's email (only provided on first sign in, may be relay email)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Apple's stable user identifier
    /// </summary>
    public string? UserIdentifier { get; set; }

    /// <summary>
    /// Whether to extend the refresh token expiration
    /// </summary>
    public bool RememberMe { get; set; }
}

/// <summary>
/// User's name from Apple Sign in
/// </summary>
public class AppleUserName
{
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
}

/// <summary>
/// Request for native Google Sign in (iOS and Android)
/// </summary>
public class NativeGoogleSignInRequest
{
    /// <summary>
    /// The ID token (JWT) from Google's native Sign-In SDK
    /// </summary>
    public string IdToken { get; set; } = string.Empty;

    /// <summary>
    /// Whether to extend the refresh token expiration
    /// </summary>
    public bool RememberMe { get; set; }
}

/// <summary>
/// Information about a linked external account
/// </summary>
public class LinkedAccountDto
{
    /// <summary>
    /// Unique identifier for this link
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Provider identifier (e.g., "Google", "Apple", "OIDC")
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the provider
    /// </summary>
    public string ProviderDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Email from the provider (may differ from account email)
    /// </summary>
    public string? ProviderEmail { get; set; }

    /// <summary>
    /// When this link was created
    /// </summary>
    public DateTime LinkedAt { get; set; }

    /// <summary>
    /// When this provider was last used for login
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
}
