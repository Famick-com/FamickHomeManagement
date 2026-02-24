namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request to authenticate a user with email and password
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's password
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Whether to extend the refresh token expiration.
    /// When true, uses extended expiration (default 30 days).
    /// When false, uses standard expiration (default 7 days).
    /// </summary>
    public bool RememberMe { get; set; }
}
