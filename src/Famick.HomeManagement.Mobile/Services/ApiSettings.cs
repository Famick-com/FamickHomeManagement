namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Server mode selection.
/// <list type="bullet">
///   <item><c>Cloud</c> — multi-tenant cloud app at <c>app.famick.com</c>.
///         Paused for cost reasons but not retired — kept here so the
///         flow comes back cleanly when the cloud-app returns.</item>
///   <item><c>SelfHosted</c> — direct connection to a self-hosted home
///         server reachable on LAN / Tailscale / public DNS. Configured
///         via QR-code scan or manual URL entry.</item>
///   <item><c>Proxied</c> — self-hosted home server, but reached via
///         the <c>auth.famick.com</c> WebSocket tunnel. Lets a mobile
///         client sign in to a home server it has no direct route to.
///         BaseUrl is resolved from the user's email at sign-in time
///         (see <see cref="EmailLookupApi"/>) and stored in
///         <see cref="ApiSettings.ProxiedBaseUrl"/>.</item>
/// </list>
/// </summary>
public enum ServerMode
{
    Cloud,
    SelfHosted,
    Proxied,
}

/// <summary>
/// Manages API settings including server mode and base URL.
/// Supports both Cloud (app.famick.com) and self-hosted deployments.
/// </summary>
public class ApiSettings
{
    private const string ServerModeKey = "server_mode";
    private const string SelfHostedUrlKey = "self_hosted_url";
    private const string TenantNameKey = "tenant_name";
    private const string ServerConfiguredKey = "server_configured";
    private const string ProxiedBaseUrlKey = "proxied_base_url";
    private const string ProxiedHomeServerIdKey = "proxied_home_server_id";
    private const string ProxiedDisplayNameKey = "proxied_display_name";

    /// <summary>
    /// Public origin of the AuthProxy email-lookup + HTTP-proxy service.
    /// Constant — there's only one production deployment of AuthProxy
    /// and it lives at famick-auth.up.railway.app today.
    /// </summary>
    public const string AuthProxyPublicBaseUrl = "https://famick-auth.up.railway.app";
    // Phase 4 chunk 4.H — when true, the app skips the local-server probe
    // and routes self-hosted traffic exclusively through the (Phase 8)
    // proxy. Toggle lives in settings; default false.
    public const string UseProxyOnlyKey = "use_proxy_only";

    /// <summary>
    /// Phase 4 chunk 4.H — opt-in for users on flaky LAN paths who'd rather
    /// pay the proxy round-trip than the 500 ms probe timeout every time
    /// the app comes back to the foreground. Stored in Preferences.
    /// </summary>
    public bool UseProxyOnly
    {
        get => Preferences.Default.Get(UseProxyOnlyKey, false);
        set => Preferences.Default.Set(UseProxyOnlyKey, value);
    }

    /// <summary>
    /// Fixed cloud URL for Famick cloud service (production).
    /// </summary>
    public const string CloudUrl = "https://app.famick.com";

    /// <summary>
    /// Debug cloud URL for testing cloud flow locally.
    /// Uses dev-local.famick.com which should be added to hosts file.
    /// iOS Simulator: Add "127.0.0.1 dev-local.famick.com" to /etc/hosts
    /// Android Emulator: Add "10.0.2.2 dev-local.famick.com" to emulator's /system/etc/hosts
    /// </summary>
    private static string DebugCloudUrl
    {
        get
        {
#if ANDROID
            // Android emulator - use HTTP to avoid cert issues with custom domain
            return "http://dev-local.famick.com:5002";
#else
            // iOS simulator - use HTTP to avoid cert issues with custom domain
            return "http://dev-local.famick.com:5002";
#endif
        }
    }

    /// <summary>
    /// Default self-hosted URL based on platform (for development).
    /// Points to self-hosted server on port 5003/5004.
    /// </summary>
    private static string DefaultSelfHostedUrl
    {
        get
        {
#if DEBUG
    #if ANDROID
            // Android emulator uses 10.0.2.2 to reach host's localhost
            return "https://10.0.2.2:5003";
    #else
            // iOS simulator - use 127.0.0.1 instead of localhost for reliability
            return "https://127.0.0.1:5003";
    #endif
#else
    #if ANDROID
            return "https://10.0.2.2:5003";
    #else
            // Check if running on a real device vs simulator
            if (DeviceInfo.DeviceType == DeviceType.Physical)
            {
                // Physical device needs host IP and HTTP (avoid SSL cert issues)
                return "http://10.18.16.105:5004";
            }
            return "https://localhost:5003";
    #endif
#endif
        }
    }

    /// <summary>
    /// Gets or sets the current server mode.
    /// Defaults to Cloud unless explicitly set (e.g., from QR code scan).
    /// </summary>
    public ServerMode Mode
    {
        get
        {
            var defaultMode = nameof(ServerMode.Cloud);
            var stored = Preferences.Default.Get(ServerModeKey, defaultMode);
            return Enum.TryParse<ServerMode>(stored, out var mode) ? mode : ServerMode.Cloud;
        }
        set => Preferences.Default.Set(ServerModeKey, value.ToString());
    }

    /// <summary>
    /// Gets or sets the self-hosted server URL.
    /// Only used when Mode is SelfHosted.
    /// </summary>
    public string SelfHostedUrl
    {
        get
        {
            // Check if we have a stored URL (from QR code scan)
            var storedUrl = Preferences.Default.Get<string?>(SelfHostedUrlKey, null);
            if (!string.IsNullOrEmpty(storedUrl))
                return storedUrl;

#if DEBUG
            // In DEBUG mode, fall back to compile-time default only if no stored URL
            return DefaultSelfHostedUrl;
#else
            return DefaultSelfHostedUrl;
#endif
        }
        set => Preferences.Default.Set(SelfHostedUrlKey, value);
    }

    /// <summary>
    /// Gets the active API base URL based on current mode.
    /// Uses stored URL for self-hosted, the resolved <c>/h/{guid}/</c>
    /// URL for proxied, or the cloud URL for cloud mode.
    /// </summary>
    public string BaseUrl
    {
        get
        {
            var mode = Mode;
            string url;

            if (mode == ServerMode.SelfHosted)
            {
                // Self-hosted: use the stored URL (from QR code or manual entry)
                url = SelfHostedUrl;
            }
            else if (mode == ServerMode.Proxied)
            {
                // Proxied: BaseUrl resolves to whatever the email-lookup
                // returned (a /h/{guid}/ URL on AuthProxy). Falls back to
                // the AuthProxy origin so the very first /check call has
                // somewhere to go even before lookup completes — that
                // request will 404, but it won't crash with a malformed
                // URI.
                url = ProxiedBaseUrl ?? AuthProxyPublicBaseUrl;
            }
            else
            {
#if DEBUG
                // In DEBUG mode with Cloud, use local dev URL for testing
                url = DebugCloudUrl;
#else
                // In RELEASE mode, use production cloud URL
                url = CloudUrl;
#endif
            }

            Console.WriteLine($"[ApiSettings] BaseUrl called - Mode: {mode}, URL: {url}");
            return url;
        }
    }

    /// <summary>
    /// Stored result of the most recent successful
    /// <c>POST /auth/lookup-email</c> against AuthProxy. Format is the
    /// pre-built proxied base — e.g.
    /// <c>https://famick-auth.up.railway.app/h/&lt;guid&gt;/</c>.
    /// Null when proxied mode is active but the email hasn't been
    /// resolved yet (first sign-in attempt).
    /// </summary>
    public string? ProxiedBaseUrl
    {
        get => Preferences.Default.Get<string?>(ProxiedBaseUrlKey, null);
        set
        {
            if (value == null) Preferences.Default.Remove(ProxiedBaseUrlKey);
            else Preferences.Default.Set(ProxiedBaseUrlKey, value);
        }
    }

    public Guid? ProxiedHomeServerId
    {
        get
        {
            var raw = Preferences.Default.Get<string?>(ProxiedHomeServerIdKey, null);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
        set
        {
            if (value is null) Preferences.Default.Remove(ProxiedHomeServerIdKey);
            else Preferences.Default.Set(ProxiedHomeServerIdKey, value.Value.ToString());
        }
    }

    public string? ProxiedDisplayName
    {
        get => Preferences.Default.Get<string?>(ProxiedDisplayNameKey, null);
        set
        {
            if (value == null) Preferences.Default.Remove(ProxiedDisplayNameKey);
            else Preferences.Default.Set(ProxiedDisplayNameKey, value);
        }
    }

    /// <summary>
    /// Checks if the app has been configured (server mode selected).
    /// </summary>
    public bool IsConfigured => Preferences.Default.ContainsKey(ServerModeKey);

    /// <summary>
    /// Gets or sets the tenant/household name (displayed on login page).
    /// </summary>
    public string? TenantName
    {
        get => Preferences.Default.Get<string?>(TenantNameKey, null);
        set
        {
            if (value != null)
                Preferences.Default.Set(TenantNameKey, value);
            else
                Preferences.Default.Remove(TenantNameKey);
        }
    }

    /// <summary>
    /// Checks if a server has been configured (via QR scan or previous login).
    /// This determines whether to show the login page or welcome page on first run.
    /// </summary>
    public bool HasServerConfigured()
    {
        return Preferences.Default.Get(ServerConfiguredKey, false);
    }

    /// <summary>
    /// Marks that a server has been configured (after QR scan or successful login).
    /// </summary>
    public void MarkServerConfigured()
    {
        Preferences.Default.Set(ServerConfiguredKey, true);
    }

    /// <summary>
    /// Configures the server from a QR code scan result.
    /// </summary>
    /// <param name="url">Server URL from QR code</param>
    /// <param name="tenantName">Tenant/household name from QR code</param>
    public void ConfigureFromQrCode(string url, string? tenantName)
    {
        Mode = ServerMode.SelfHosted;
        SelfHostedUrl = url;
        TenantName = tenantName;
        MarkServerConfigured();
    }

    /// <summary>
    /// Configures for cloud mode with tenant name.
    /// </summary>
    /// <param name="tenantName">Tenant/household name</param>
    public void ConfigureForCloud(string? tenantName)
    {
        Mode = ServerMode.Cloud;
        TenantName = tenantName;
        MarkServerConfigured();
    }

    /// <summary>
    /// Configures for proxied mode without yet knowing the home server —
    /// used the moment the user taps "Sign In" on the welcome page,
    /// before they've typed their email. The actual
    /// <c>/h/{guid}/</c> BaseUrl gets stored later via
    /// <see cref="ConfigureProxiedHomeServer"/> once
    /// <see cref="EmailLookupApi"/> resolves it.
    /// </summary>
    public void ConfigureForProxied()
    {
        Mode = ServerMode.Proxied;
        // Note: do NOT call MarkServerConfigured() here — that flag
        // means "we know which server to talk to", which we won't until
        // the email lookup completes successfully.
    }

    /// <summary>
    /// Records the resolved home-server identity after a successful
    /// email lookup, and marks the app as fully configured.
    /// </summary>
    public void ConfigureProxiedHomeServer(EmailLookupSuccess result)
    {
        Mode = ServerMode.Proxied;
        ProxiedBaseUrl = result.BaseUrl;
        ProxiedHomeServerId = result.HomeServerId;
        ProxiedDisplayName = result.DisplayName;
        TenantName = string.IsNullOrEmpty(result.DisplayName) ? null : result.DisplayName;
        MarkServerConfigured();
    }

    /// <summary>
    /// Resets to default settings (Cloud mode).
    /// </summary>
    public void Reset()
    {
        Preferences.Default.Remove(ServerModeKey);
        Preferences.Default.Remove(SelfHostedUrlKey);
        Preferences.Default.Remove(TenantNameKey);
        Preferences.Default.Remove(ServerConfiguredKey);
        Preferences.Default.Remove(ProxiedBaseUrlKey);
        Preferences.Default.Remove(ProxiedHomeServerIdKey);
        Preferences.Default.Remove(ProxiedDisplayNameKey);
    }

    /// <summary>
    /// Cloud domain patterns for detecting cloud servers.
    /// </summary>
    private static readonly string[] CloudDomains = { "famick.com" };

    /// <summary>
    /// Checks if the current server is a cloud server (based on domain).
    /// Cloud servers use *.famick.com domains.
    /// </summary>
    public bool IsCloudServer()
    {
        // If mode is explicitly set to Cloud, it's a cloud server
        if (Mode == ServerMode.Cloud)
            return true;

        // Check the URL domain pattern
        var url = BaseUrl;
        if (string.IsNullOrEmpty(url))
            return true; // Default to cloud behavior when not configured

        try
        {
            var uri = new Uri(url);
            return CloudDomains.Any(domain =>
                uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return true; // Default to cloud behavior on parse error
        }
    }

    /// <summary>
    /// Checks if the current server is a self-hosted server.
    /// Self-hosted servers are anything that's not a cloud server.
    /// </summary>
    public bool IsSelfHostedServer() => !IsCloudServer();
}
