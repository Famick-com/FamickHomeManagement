namespace Famick.HomeManagement.Core.DTOs.Setup;

/// <summary>
/// Response indicating the setup status of the application
/// </summary>
public class SetupStatusResponse
{
    /// <summary>
    /// Indicates if initial setup is required
    /// </summary>
    public bool SetupRequired { get; set; }

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
