namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response after completing registration (account created)
/// </summary>
public class CompleteRegistrationResponse
{
    /// <summary>
    /// Whether the registration was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Success/error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// JWT access token for immediate login
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for obtaining new access tokens
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// When the access token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Created user information
    /// </summary>
    public UserDto User { get; set; } = null!;

    /// <summary>
    /// Created tenant/household information
    /// </summary>
    public TenantInfoDto Tenant { get; set; } = null!;
}
