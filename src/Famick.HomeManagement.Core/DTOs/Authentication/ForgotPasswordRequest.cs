namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request to initiate a password reset
/// </summary>
public class ForgotPasswordRequest
{
    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;
}
