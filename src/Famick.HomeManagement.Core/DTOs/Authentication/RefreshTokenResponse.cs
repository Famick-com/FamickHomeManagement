namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response after successful token refresh
/// </summary>
public class RefreshTokenResponse
{
    /// <summary>
    /// New JWT access token
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// New refresh token (rotated for security)
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// When the new access token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
