namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request to reset a password using a valid reset token
/// </summary>
public class ResetPasswordRequest
{
    /// <summary>
    /// The password reset token from the email link
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// The new password
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// Confirmation of the new password
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}
