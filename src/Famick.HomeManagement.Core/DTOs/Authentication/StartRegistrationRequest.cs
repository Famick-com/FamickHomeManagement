using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request to start the registration process (mobile onboarding flow).
/// Sends a verification email to the provided address.
/// </summary>
public class StartRegistrationRequest
{
    /// <summary>
    /// Name for the household (will become the tenant name)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string HouseholdName { get; set; } = string.Empty;

    /// <summary>
    /// User's email address (will receive verification email)
    /// </summary>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
