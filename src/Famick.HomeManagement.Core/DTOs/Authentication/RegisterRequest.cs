using System.ComponentModel.DataAnnotations;
using Famick.HomeManagement.Core.Validators.Common;

namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request to register a new user account
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// User's email address (used for login)
    /// </summary>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's chosen password
    /// </summary>
    [Required]
    [MinLength(PasswordRules.MinimumLength, ErrorMessage = PasswordRules.TooShortMessage)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Password confirmation (must match Password)
    /// </summary>
    [Required]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// User's first name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Optional username (defaults to email if not provided)
    /// </summary>
    [MaxLength(100)]
    public string? Username { get; set; }
}
