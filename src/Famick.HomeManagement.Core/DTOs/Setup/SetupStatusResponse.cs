using Famick.HomeManagement.Core.Platform;

namespace Famick.HomeManagement.Core.DTOs.Setup;

/// <summary>
/// Response indicating the setup status of the application
/// </summary>
public class SetupStatusResponse
{
    /// <summary>
    /// The deployment platform the server is running on, so the SPA can adapt its
    /// UI without re-deriving config flags. Fetched anonymously at boot.
    /// </summary>
    public ServerPlatform Platform { get; set; }

    /// <summary>
    /// Indicates if initial setup is required
    /// </summary>
    public bool SetupRequired { get; set; }

    /// <summary>
    /// Whether accounts are created here by signing up, rather than during first-run setup, so
    /// the sign-in page knows whether to offer a sign-up link. False on a server holding a
    /// single household, where the one account already exists and there is nothing to join.
    /// <para>
    /// True does not promise sign-ups are currently being accepted — the sign-up page says so
    /// itself when they are closed.
    /// </para>
    /// </summary>
    public bool SupportsSignUp { get; set; }

    /// <summary>
    /// The reason setup is required (e.g., "no_users")
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Whether the registration page should show a legal consent checkbox.
    /// True for cloud, false for self-hosted.
    /// </summary>
    public bool RequireLegalConsent { get; set; }
}
