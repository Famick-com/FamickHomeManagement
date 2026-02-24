namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response after starting registration (verification email sent)
/// </summary>
public class StartRegistrationResponse
{
    /// <summary>
    /// Success message
    /// </summary>
    public string Message { get; set; } = "Verification email sent. Please check your inbox.";

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Masked email address for display (e.g., "j***@example.com")
    /// </summary>
    public string MaskedEmail { get; set; } = string.Empty;
}
