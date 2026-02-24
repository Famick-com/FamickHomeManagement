using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for generating and validating JWT access tokens and refresh tokens
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user with permissions and roles
    /// </summary>
    /// <param name="user">The user to generate the token for</param>
    /// <param name="permissions">The user's permissions to include in the token claims</param>
    /// <param name="roles">The user's roles to include in the token claims</param>
    /// <returns>The signed JWT token string</returns>
    string GenerateAccessToken(User user, IEnumerable<string> permissions, IEnumerable<Role>? roles = null, bool mustAcceptTerms = false);

    /// <summary>
    /// Generates a cryptographically secure random refresh token
    /// </summary>
    /// <returns>A base64-encoded random token string</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates a JWT access token and extracts the user ID
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>The user ID if the token is valid, null otherwise</returns>
    Guid? ValidateAccessToken(string token);

    /// <summary>
    /// Gets the expiration time for newly generated access tokens
    /// </summary>
    /// <returns>The DateTime when a newly generated token would expire</returns>
    DateTime GetTokenExpiration();
}
