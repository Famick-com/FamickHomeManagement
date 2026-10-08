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
    /// Relying Party ID used for ceremonies driven by the native mobile app, as opposed to a
    /// browser.
    /// </summary>
    /// <remarks>
    /// A native app can only run a passkey ceremony for a relying party it is *statically
    /// associated* with: on iOS through a <c>webcredentials:</c> entitlement, on Android through a
    /// <c>delegate_permission/common.get_login_creds</c> entry in the domain's
    /// <c>assetlinks.json</c>. Both are fixed at build time and validated by the OS against the
    /// relying party's domain, so a household's own hostname can never be used — the app would have
    /// to ship an entitlement per household.
    ///
    /// The way out is that <b>the relying party ID does not have to be the API host.</b> A
    /// self-hosted or proxied home server can verify an assertion whose RP ID is
    /// <c>app.famick.com</c>: the OS checks the association against that domain, which Famick
    /// controls and already serves the necessary association files, while the home server checks
    /// the signature against the public key it stored. One native RP ID therefore serves every
    /// deployment, with nothing required of the household.
    ///
    /// This is safe because only an app or page associated with <c>app.famick.com</c> can produce an
    /// assertion bearing that RP ID hash, and the credential still has to exist on the server doing
    /// the verifying.
    ///
    /// On the cloud app this equals <see cref="RelyingPartyId"/>, so web and native differ only in
    /// their allowed origins.
    /// </remarks>
    public string NativeRelyingPartyId { get; set; } = "app.famick.com";

    /// <summary>
    /// Allowed origins for native-app ceremonies.
    /// </summary>
    /// <remarks>
    /// These are the Famick app's own identifiers, identical on every deployment, which is why they
    /// are defaults in code rather than per-host configuration:
    /// <list type="bullet">
    ///   <item>iOS reports the origin as <c>https://&lt;rpId&gt;</c>.</item>
    ///   <item>Android reports <c>android:apk-key-hash:&lt;base64url SHA-256 of the signing
    ///         certificate&gt;</c>. Both the upload key and the Play App Signing key are listed,
    ///         because they have different fingerprints and installs from Play are re-signed.</item>
    /// </list>
    /// Fido2NetLib compares origins by exact string after
    /// <c>StringExtensions.ToFullyQualifiedOrigin()</c>, which returns a URI with no authority
    /// unchanged (its <c>HostNameType</c> is <c>Unknown</c>). So an <c>android:apk-key-hash:</c>
    /// value is matched verbatim — there is no wildcarding, and adding one does not widen what any
    /// other app can do.
    /// </remarks>
    public string[] NativeOrigins { get; set; } =
    [
        "https://app.famick.com",
        // Upload key
        "android:apk-key-hash:Zj1eMSz5GLC69HrtOnIBtqAZadSisybrydjDYFmBbYs",
        // Play App Signing key
        "android:apk-key-hash:ef35kpfyEEotVx5E-ywC_0ZGpnHgyVEu0NJwSw1le54",
    ];

    /// <summary>
    /// Whether this provider is properly configured
    /// </summary>
    public bool IsConfigured => Enabled &&
        !string.IsNullOrWhiteSpace(RelyingPartyId) &&
        Origins.Length > 0;

    /// <summary>
    /// Whether native-app passkey ceremonies can be served. Reported to clients as
    /// <c>passkeyNativeSupported</c> so the mobile app shows its passkey UI only where the server
    /// can actually complete the ceremony, rather than deciding from its own connection mode.
    /// </summary>
    public bool IsNativeConfigured => IsConfigured &&
        !string.IsNullOrWhiteSpace(NativeRelyingPartyId) &&
        NativeOrigins.Length > 0;
}
