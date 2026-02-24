namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response to a password reset request.
/// Always returns success to prevent email enumeration.
/// </summary>
public class ForgotPasswordResponse
{
    /// <summary>
    /// Success message (always returns success to prevent email enumeration)
    /// </summary>
    public string Message { get; set; } = "If an account with that email exists, a password reset link has been sent.";
}
