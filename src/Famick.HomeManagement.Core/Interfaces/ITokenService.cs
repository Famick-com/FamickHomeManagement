using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for generating and validating JWT access tokens and refresh tokens
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user with permissions and roles.
    /// </summary>
    /// <param name="user">The user to generate the token for</param>
    /// <param name="permissions">The user's permissions to include in the token claims</param>
    /// <param name="roles">The user's roles to include in the token claims</param>
    /// <param name="mustAcceptTerms">Whether the user must accept terms before regular API use (cloud-only flag)</param>
    /// <param name="authTime">
    /// Time of the user's most recent first-factor authentication. Set on login / step-up
    /// re-auth = now. Preserved verbatim across <c>/auth/refresh</c> by passing the parent
    /// refresh token's <c>AuthTime</c>. Defaults to <see cref="DateTime.UtcNow"/> when null
    /// (login default; do not pass null on the refresh path).
    /// </param>
    /// <param name="iat">
    /// Optional override for the JWT's <c>iat</c> claim. Used by the change-password flow
    /// to set <c>iat = jwt_min_iat + 1</c> so just-issued tokens survive the bump performed
    /// inside the same critical section. Defaults to <see cref="DateTime.UtcNow"/> when null.
    /// </param>
    /// <returns>The signed JWT token string</returns>
    string GenerateAccessToken(
        User user,
        IEnumerable<string> permissions,
        IEnumerable<Role>? roles = null,
        bool mustAcceptTerms = false,
        DateTime? authTime = null,
        DateTime? iat = null);

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
