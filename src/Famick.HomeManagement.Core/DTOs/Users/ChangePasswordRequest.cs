namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Request to change user's own password
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>
    /// Current password for verification
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// New password
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// New password confirmation
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}
