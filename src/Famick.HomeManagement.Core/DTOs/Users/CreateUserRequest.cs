using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Request to create a new user
/// </summary>
public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Optional password. If null, a random password will be generated.
    /// </summary>
    public string? Password { get; set; }

    public List<Role> Roles { get; set; } = new();

    /// <summary>
    /// If true, the user must change their password on first login
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// If true, sends a welcome email with login credentials
    /// </summary>
    public bool SendWelcomeEmail { get; set; }
}
