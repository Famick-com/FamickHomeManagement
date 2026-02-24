namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request to resend the email verification link
/// </summary>
public class ResendVerificationRequest
{
    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;
}
