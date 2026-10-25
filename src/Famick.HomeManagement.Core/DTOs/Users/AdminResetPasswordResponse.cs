namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Response after admin resets a user's password
/// </summary>
public class AdminResetPasswordResponse
{
    public bool Success { get; set; }

    /// <summary>
    /// The generated password, only returned when password was auto-generated.
    /// </summary>
    public string? GeneratedPassword { get; set; }

    /// <summary>
    /// True when the member was emailed a link to choose a new password, so there is no password for
    /// the admin to hand over.
    /// </summary>
    public bool ResetEmailSent { get; set; }
}
