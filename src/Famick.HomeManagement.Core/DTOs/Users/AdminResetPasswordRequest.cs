namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Request for admin to reset a user's password
/// </summary>
public class AdminResetPasswordRequest
{
    /// <summary>
    /// Optional new password. If null, a random password will be generated.
    /// </summary>
    public string? NewPassword { get; set; }
}
