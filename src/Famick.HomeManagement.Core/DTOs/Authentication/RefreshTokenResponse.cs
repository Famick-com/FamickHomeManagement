namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response after successful token refresh
/// </summary>
public class RefreshTokenResponse : IRefreshTokenCarrier
{
    /// <summary>
    /// New JWT access token
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// New refresh token (rotated for security)
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    string? IRefreshTokenCarrier.RefreshToken
    {
        get => RefreshToken;
        set => RefreshToken = value!;
    }

    /// <summary>
    /// When the new access token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
