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
/// <remarks>
/// Declared here rather than beside <see cref="ApiSettings"/> so that
/// <see cref="ServerModeClassification"/> can be reached from a test project.
/// <c>ApiSettings</c> reads <c>Preferences</c> and <c>DeviceInfo</c>, which pulls in
/// MAUI and cannot be referenced from a plain <c>net10.0</c> assembly.
/// </remarks>
public enum ServerMode
{
    Cloud,
    SelfHosted,
    Proxied,
}

/// <summary>
/// Decides whether a <see cref="ServerMode"/> is a paying cloud tenant.
/// </summary>
/// <remarks>
/// Split out from <see cref="ApiSettings"/> so it can be tested. It decides whether the
/// app offers to sell a subscription, and it has been wrong before: the classification
/// used to fall back to matching the base URL against famick.com, which called a
/// <see cref="ServerMode.Proxied"/> household a cloud tenant — those are self-hosted, free
/// under ELv2, and merely reached through <c>auth.famick.com</c>, so their base URL really
/// is on a famick.com host. The result was a self-hosted household subjected to cloud tier
/// gating and offered a subscription for something it already has.
/// </remarks>
public static class ServerModeClassification
{
    /// <summary>
    /// True when the mode denotes a cloud tenant, which is the only kind of household
    /// with a subscription to sell.
    /// </summary>
    /// <remarks>
    /// The mode is authoritative — every path that points the app at a server sets it — so
    /// nothing is inferred from the URL. An unrecognised value keeps the long-standing
    /// default of treating the household as cloud; see <see cref="ApiSettings.Mode"/>, which
    /// rejects undefined values before they reach here.
    /// </remarks>
    public static bool IsCloud(this ServerMode mode) => mode switch
    {
        ServerMode.Cloud => true,
        ServerMode.SelfHosted => false,
        ServerMode.Proxied => false,
        _ => true,
    };

    /// <summary>True when the household runs its own server, proxied or not.</summary>
    public static bool IsSelfHosted(this ServerMode mode) => !mode.IsCloud();
}
