using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request to verify an email address using the token from the verification email
/// </summary>
public class VerifyEmailRequest
{
    /// <summary>
    /// The verification token from the email link
    /// </summary>
    [Required]
    public string Token { get; set; } = string.Empty;
}
