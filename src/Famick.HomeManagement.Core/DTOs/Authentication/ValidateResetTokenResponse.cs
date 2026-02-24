namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response when validating a password reset token
/// </summary>
public class ValidateResetTokenResponse
{
    /// <summary>
    /// Whether the token is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// The email address associated with the token (only populated if valid)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Error message if token is invalid
    /// </summary>
    public string? ErrorMessage { get; set; }
}
