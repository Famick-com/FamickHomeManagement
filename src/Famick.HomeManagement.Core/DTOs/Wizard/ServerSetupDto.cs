namespace Famick.HomeManagement.Core.DTOs.Wizard;

/// <summary>
/// Step 0 of the first-run wizard: the bare-minimum server-level settings.
/// More advanced server config (SMTP, JWT issuer/audience, plugin path) is
/// edited from the admin "Server Settings" page after setup completes.
/// Persisted to <c>config/server-config.json</c> via <c>IServerConfigService</c>.
/// </summary>
public class ServerSetupDto
{
    /// <summary>
    /// Public URL the server is reached at (notification emails, OAuth
    /// redirects, etc.). e.g. <c>https://home.example.com</c>.
    /// </summary>
    public string PublicHostName { get; set; } = "https://localhost";

    /// <summary>
    /// IANA time zone the server reports user-facing timestamps in.
    /// </summary>
    public string TimeZone { get; set; } = "UTC";
}
