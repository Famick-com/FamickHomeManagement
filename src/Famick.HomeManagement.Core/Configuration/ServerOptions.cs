namespace Famick.HomeManagement.Core.Configuration;

/// <summary>
/// Server-level configuration that the first-run wizard and the admin
/// "Server Settings" page persist to the self-hosted <c>config/server-config.json</c>
/// overlay. Lives alongside the baked-in <c>appsettings.json</c> so operators can edit
/// it (via UI or by hand) without rebuilding the image.
/// </summary>
public class ServerOptions
{
    public const string SectionName = "Server";

    /// <summary>
    /// True once the first-run wizard finishes. Drives the redirect that sends
    /// fresh installs through setup before any other UI.
    /// </summary>
    public bool SetupComplete { get; set; }

    /// <summary>
    /// The public URL operators reach the server at (used for absolute links in
    /// notification emails, OAuth redirects, etc.). e.g. <c>https://home.example.com</c>.
    /// </summary>
    public string PublicHostName { get; set; } = "https://localhost";

    /// <summary>
    /// IANA time zone the server reports user-facing timestamps in. Defaults to UTC.
    /// </summary>
    public string TimeZone { get; set; } = "UTC";
}
