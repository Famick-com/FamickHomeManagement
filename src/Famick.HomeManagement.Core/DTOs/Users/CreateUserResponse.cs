namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Response after creating a new user
/// </summary>
public class CreateUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The generated password, only returned when password was auto-generated.
    /// Admin can see this to share with the user.
    /// </summary>
    public string? GeneratedPassword { get; set; }

    public bool WelcomeEmailSent { get; set; }
}
