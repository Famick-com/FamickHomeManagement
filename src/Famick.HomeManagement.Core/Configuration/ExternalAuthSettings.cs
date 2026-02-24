namespace Famick.HomeManagement.Core.Configuration;

/// <summary>
/// Configuration for external authentication providers
/// </summary>
public class ExternalAuthSettings
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "ExternalAuth";

    /// <summary>
    /// Whether password-based authentication is enabled.
    /// When false, users can only log in via external providers (Google, Apple, OIDC, Passkey).
    /// Default is true.
    /// </summary>
    public bool PasswordAuthEnabled { get; set; } = true;

    /// <summary>
    /// Apple Sign In configuration
    /// </summary>
    public AppleAuthSettings Apple { get; set; } = new();

    /// <summary>
    /// Google OAuth configuration
    /// </summary>
    public GoogleAuthSettings Google { get; set; } = new();

    /// <summary>
    /// Generic OpenID Connect configuration
    /// </summary>
    public OidcAuthSettings OpenIdConnect { get; set; } = new();

    /// <summary>
    /// WebAuthn/Passkey configuration
    /// </summary>
    public PasskeySettings Passkey { get; set; } = new();
}

/// <summary>
/// Apple Sign In configuration
/// </summary>
public class AppleAuthSettings
{
    /// <summary>
    /// Whether Apple Sign In is enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Apple Services ID (Client ID)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Apple Team ID
    /// </summary>
    public string TeamId { get; set; } = string.Empty;

    /// <summary>
    /// Apple Key ID for the Sign In private key
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Apple Sign In private key (PEM format)
    /// </summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// iOS App Bundle ID (for native Sign in with Apple).
    /// The identity token audience will be this value instead of ClientId for native iOS apps.
    /// </summary>
    public string BundleId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this provider is properly configured
    /// </summary>
    public bool IsConfigured => Enabled &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(TeamId) &&
        !string.IsNullOrWhiteSpace(KeyId) &&
        !string.IsNullOrWhiteSpace(PrivateKey);
}

/// <summary>
/// Google OAuth configuration
/// </summary>
public class GoogleAuthSettings
{
    /// <summary>
    /// Whether Google OAuth is enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Google OAuth Client ID (Web)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Google OAuth Client Secret
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// iOS Client ID for native Google Sign-In.
    /// Create this in Google Cloud Console under "iOS" application type.
    /// </summary>
    public string IosClientId { get; set; } = string.Empty;

    /// <summary>
    /// Android Client ID for native Google Sign-In.
    /// Create this in Google Cloud Console under "Android" application type.
    /// </summary>
    public string AndroidClientId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this provider is properly configured
    /// </summary>
    public bool IsConfigured => Enabled &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// Generic OpenID Connect configuration
/// </summary>
public class OidcAuthSettings
{
    /// <summary>
    /// Whether OpenID Connect is enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// OIDC Authority URL (issuer)
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// OIDC Client ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OIDC Client Secret
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the provider (e.g., "Company SSO")
    /// </summary>
    public string DisplayName { get; set; } = "SSO";

    /// <summary>
    /// Additional scopes to request (default: openid profile email)
    /// </summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>
    /// Whether this provider is properly configured
    /// </summary>
    public bool IsConfigured => Enabled &&
        !string.IsNullOrWhiteSpace(Authority) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// WebAuthn/Passkey configuration
/// </summary>
public class PasskeySettings
{
    /// <summary>
    /// Whether passkey authentication is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Relying Party ID (typically your domain, e.g., "example.com")
    /// </summary>
    public string RelyingPartyId { get; set; } = "localhost";

    /// <summary>
    /// Relying Party name displayed to users
    /// </summary>
    public string RelyingPartyName { get; set; } = "Famick Home Management";

    /// <summary>
    /// Allowed origins for WebAuthn operations
    /// </summary>
    public string[] Origins { get; set; } = ["https://localhost:5001"];

    /// <summary>
    /// Timeout for WebAuthn operations in milliseconds
    /// </summary>
    public uint Timeout { get; set; } = 60000;

    /// <summary>
    /// Whether user verification is required (true) or preferred (false)
    /// </summary>
    public bool RequireUserVerification { get; set; } = true;

    /// <summary>
    /// Whether this provider is properly configured
    /// </summary>
    public bool IsConfigured => Enabled &&
        !string.IsNullOrWhiteSpace(RelyingPartyId) &&
        Origins.Length > 0;
}
