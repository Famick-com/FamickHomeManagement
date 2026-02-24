namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Request to update user's own profile
/// </summary>
public class UpdateProfileRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PreferredLanguage { get; set; }
}
