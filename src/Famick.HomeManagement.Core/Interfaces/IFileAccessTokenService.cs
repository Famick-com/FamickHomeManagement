namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for generating and validating short-lived tokens for file access.
/// Used to allow browser-initiated requests (img src, iframe) to access protected files
/// without requiring the Authorization header.
/// </summary>
public interface IFileAccessTokenService
{
    /// <summary>
    /// Generates a signed token for accessing a file.
    /// </summary>
    /// <param name="resourceType">Type of resource (e.g., "equipment-document", "product-image")</param>
    /// <param name="resourceId">The resource ID (document or image ID)</param>
    /// <param name="tenantId">The tenant ID for validation</param>
    /// <param name="expirationMinutes">Token validity in minutes (default: 15)</param>
    /// <returns>A signed token string</returns>
    string GenerateToken(string resourceType, Guid resourceId, Guid tenantId, int expirationMinutes = 15);

    /// <summary>
    /// Validates a file access token.
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <param name="resourceType">Expected resource type</param>
    /// <param name="resourceId">Expected resource ID</param>
    /// <param name="tenantId">Expected tenant ID</param>
    /// <returns>True if the token is valid and not expired</returns>
    bool ValidateToken(string token, string resourceType, Guid resourceId, Guid tenantId);

    /// <summary>
    /// Parses a token and returns its claims if the signature is valid and not expired.
    /// Use this to extract the tenant ID before looking up the resource.
    /// </summary>
    /// <param name="token">The token to parse</param>
    /// <param name="claims">The parsed claims if successful</param>
    /// <returns>True if the token signature is valid and not expired</returns>
    bool TryParseToken(string token, out FileAccessTokenClaims? claims);
}

/// <summary>
/// Claims extracted from a file access token
/// </summary>
public record FileAccessTokenClaims(string ResourceType, Guid ResourceId, Guid TenantId);
