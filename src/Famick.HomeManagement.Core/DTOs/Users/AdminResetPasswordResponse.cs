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
}
