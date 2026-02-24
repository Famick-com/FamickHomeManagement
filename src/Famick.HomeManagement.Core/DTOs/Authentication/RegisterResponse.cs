namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response after successful user registration
/// </summary>
public class RegisterResponse
{
    /// <summary>
    /// The newly created user's ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The registered email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Success message
    /// </summary>
    public string Message { get; set; } = "Registration successful. You can now log in.";

    /// <summary>
    /// JWT access token (auto-login after registration)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Refresh token (auto-login after registration)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token expiration time (if auto-login is enabled)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}
