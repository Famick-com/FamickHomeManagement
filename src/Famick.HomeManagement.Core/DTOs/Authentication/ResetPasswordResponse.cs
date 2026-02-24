namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response after attempting to reset a password
/// </summary>
public class ResetPasswordResponse
{
    /// <summary>
    /// Whether the password reset was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
