using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Generates and validates HMAC-signed tokens for one-click email unsubscribe (RFC 8058)
/// </summary>
public interface IUnsubscribeTokenService
{
    /// <summary>
    /// Generates a signed token encoding the user, tenant, and notification type
    /// </summary>
    /// <param name="userId">The user to unsubscribe</param>
    /// <param name="tenantId">The tenant context</param>
    /// <param name="type">The notification type to unsubscribe from</param>
    /// <returns>A signed token string</returns>
    string GenerateToken(Guid userId, Guid tenantId, NotificationType type);

    /// <summary>
    /// Validates and parses an unsubscribe token
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <param name="claims">The parsed claims if successful</param>
    /// <returns>True if the token signature is valid and not expired</returns>
    bool TryParseToken(string token, out UnsubscribeTokenClaims? claims);
}

/// <summary>
/// Claims extracted from an unsubscribe token
/// </summary>
public record UnsubscribeTokenClaims(Guid UserId, Guid TenantId, NotificationType NotificationType);
