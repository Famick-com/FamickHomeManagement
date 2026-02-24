namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response after verifying an email address
/// </summary>
public class VerifyEmailResponse
{
    /// <summary>
    /// Whether the verification was successful
    /// </summary>
    public bool Verified { get; set; }

    /// <summary>
    /// The verified email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The household name from registration
    /// </summary>
    public string HouseholdName { get; set; } = string.Empty;

    /// <summary>
    /// Message describing the result
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Token to use for completing registration (same token, returned for convenience)
    /// </summary>
    public string Token { get; set; } = string.Empty;
}
