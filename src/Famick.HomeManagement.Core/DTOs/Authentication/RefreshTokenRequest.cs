namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request to refresh an access token
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// The refresh token to use for generating a new access token
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
