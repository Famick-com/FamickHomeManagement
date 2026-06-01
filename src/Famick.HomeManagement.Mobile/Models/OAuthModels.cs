namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// Authentication configuration from the server.
/// </summary>
public class AuthConfiguration
{
    public bool PasswordAuthEnabled { get; set; } = true;
    public bool PasskeyEnabled { get; set; }
    public List<ExternalAuthProvider> Providers { get; set; } = new();

    /// <summary>
    /// Phase 4 chunk 4.E — feature flags delivered alongside the rest of
    /// the auth config so the mobile app needs only one startup round-trip.
    /// All flags default to false on network failure or older servers.
    /// </summary>
    public ClientFeatureFlags FeatureFlags { get; set; } = new();
}

/// <summary>
/// Phase 4 chunk 4.E — subset of server-side feature flags surfaced to the
/// client. Mirrors <c>ClientFeatureFlagsDto</c> on the server.
/// </summary>
public class ClientFeatureFlags
{
    public bool TwoStepLoginV2 { get; set; }
    public bool CheckEndpointEnabled { get; set; }

    /// <summary>
    /// Phase 5 chunk 5.I/5.J — when true (cloud mode), the app routes auth
    /// calls to <c>auth.famick.com</c> instead of <c>app.famick.com</c>.
    /// Persisted into <c>ApiSettings.UseAuthFamickCom</c> on each config
    /// fetch so <c>ApiSettings.AuthBaseUrl</c> can act on it.
    /// </summary>
    public bool UseAuthFamickCom { get; set; }
}

/// <summary>
/// Phase 4 chunk 4.C — response from POST /check. Deserializes the
/// server-side kebab-case <c>account-type</c> field.
/// </summary>
public class CheckResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("account-type")]
    public string AccountType { get; set; } = string.Empty;
}

/// <summary>
/// External authentication provider information.
/// </summary>
public class ExternalAuthProvider
{
    /// <summary>
    /// Provider identifier (Google, Apple, OIDC).
    /// </summary>
    public string Provider { get; set; } = "";

    /// <summary>
    /// Display name for the button (e.g., "Sign in with Google", "Company SSO").
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Whether the provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Response from OAuth challenge endpoint.
/// </summary>
public class OAuthChallengeResponse
{
    /// <summary>
    /// The OAuth authorization URL to redirect the user to.
    /// </summary>
    public string AuthorizationUrl { get; set; } = "";

    /// <summary>
    /// State parameter for CSRF protection.
    /// </summary>
    public string State { get; set; } = "";
}

/// <summary>
/// Request to process OAuth callback.
/// </summary>
public class OAuthCallbackRequest
{
    /// <summary>
    /// Authorization code from the OAuth provider.
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// State parameter for CSRF validation.
    /// </summary>
    public string State { get; set; } = "";

    /// <summary>
    /// Whether to remember the login (extended refresh token).
    /// </summary>
    public bool RememberMe { get; set; }
}

/// <summary>
/// Request for native Apple Sign in from iOS.
/// </summary>
public class NativeAppleSignInRequest
{
    /// <summary>
    /// The identity token (JWT) from native Sign in with Apple.
    /// </summary>
    public string IdentityToken { get; set; } = "";

    /// <summary>
    /// The authorization code from Apple (optional).
    /// </summary>
    public string? AuthorizationCode { get; set; }

    /// <summary>
    /// User's name from Apple (only provided on first sign-in).
    /// </summary>
    public AppleUserName? FullName { get; set; }

    /// <summary>
    /// User's email (only provided on first sign-in).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Apple's stable user identifier.
    /// </summary>
    public string? UserIdentifier { get; set; }

    /// <summary>
    /// Whether to remember the login (extended refresh token).
    /// </summary>
    public bool RememberMe { get; set; }
}

/// <summary>
/// User's name from Apple Sign in.
/// </summary>
public class AppleUserName
{
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
}

/// <summary>
/// Request for native Google Sign in from iOS/Android.
/// </summary>
public class NativeGoogleSignInRequest
{
    /// <summary>
    /// The ID token (JWT) from native Google Sign-In SDK.
    /// </summary>
    public string IdToken { get; set; } = "";

    /// <summary>
    /// Whether to remember the login (extended refresh token).
    /// </summary>
    public bool RememberMe { get; set; }
}
