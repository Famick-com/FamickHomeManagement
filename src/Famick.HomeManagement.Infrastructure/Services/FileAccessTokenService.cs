using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for generating and validating short-lived HMAC-signed tokens for file access.
/// Tokens are URL-safe Base64 encoded and contain: resourceType|resourceId|tenantId|expiration|signature
/// </summary>
public class FileAccessTokenService : IFileAccessTokenService
{
    private readonly byte[] _secretKey;
    private readonly ILogger<FileAccessTokenService> _logger;

    public FileAccessTokenService(string secretKey, ILogger<FileAccessTokenService> logger)
    {
        if (string.IsNullOrEmpty(secretKey) || secretKey.Length < 32)
        {
            throw new ArgumentException("Secret key must be at least 32 characters", nameof(secretKey));
        }

        _secretKey = Encoding.UTF8.GetBytes(secretKey);
        _logger = logger;
    }

    public string GenerateToken(string resourceType, Guid resourceId, Guid tenantId, int expirationMinutes = 15)
    {
        var expiration = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes).ToUnixTimeSeconds();
        var payload = $"{resourceType}|{resourceId}|{tenantId}|{expiration}";
        var signature = ComputeSignature(payload);

        var token = $"{payload}|{signature}";
        return Base64UrlEncode(token);
    }

    public bool ValidateToken(string token, string resourceType, Guid resourceId, Guid tenantId)
    {
        try
        {
            var decoded = Base64UrlDecode(token);
            var parts = decoded.Split('|');

            if (parts.Length != 5)
            {
                _logger.LogWarning("Invalid token format: wrong number of parts");
                return false;
            }

            var tokenResourceType = parts[0];
            var tokenResourceId = parts[1];
            var tokenTenantId = parts[2];
            var tokenExpiration = parts[3];
            var tokenSignature = parts[4];

            // Verify resource type
            if (tokenResourceType != resourceType)
            {
                _logger.LogWarning("Token resource type mismatch: expected {Expected}, got {Actual}",
                    resourceType, tokenResourceType);
                return false;
            }

            // Verify resource ID
            if (!Guid.TryParse(tokenResourceId, out var parsedResourceId) || parsedResourceId != resourceId)
            {
                _logger.LogWarning("Token resource ID mismatch");
                return false;
            }

            // Verify tenant ID
            if (!Guid.TryParse(tokenTenantId, out var parsedTenantId) || parsedTenantId != tenantId)
            {
                _logger.LogWarning("Token tenant ID mismatch");
                return false;
            }

            // Verify expiration
            if (!long.TryParse(tokenExpiration, out var expiration))
            {
                _logger.LogWarning("Invalid token expiration format");
                return false;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiration)
            {
                _logger.LogWarning("Token has expired");
                return false;
            }

            // Verify signature
            var payload = $"{tokenResourceType}|{tokenResourceId}|{tokenTenantId}|{tokenExpiration}";
            var expectedSignature = ComputeSignature(payload);

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(tokenSignature),
                Encoding.UTF8.GetBytes(expectedSignature)))
            {
                _logger.LogWarning("Token signature validation failed");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed with exception");
            return false;
        }
    }

    public bool TryParseToken(string token, out FileAccessTokenClaims? claims)
    {
        claims = null;

        try
        {
            var decoded = Base64UrlDecode(token);
            var parts = decoded.Split('|');

            if (parts.Length != 5)
            {
                _logger.LogWarning("Invalid token format: wrong number of parts");
                return false;
            }

            var tokenResourceType = parts[0];
            var tokenResourceId = parts[1];
            var tokenTenantId = parts[2];
            var tokenExpiration = parts[3];
            var tokenSignature = parts[4];

            // Verify signature first
            var payload = $"{tokenResourceType}|{tokenResourceId}|{tokenTenantId}|{tokenExpiration}";
            var expectedSignature = ComputeSignature(payload);

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(tokenSignature),
                Encoding.UTF8.GetBytes(expectedSignature)))
            {
                _logger.LogWarning("Token signature validation failed");
                return false;
            }

            // Verify expiration
            if (!long.TryParse(tokenExpiration, out var expiration))
            {
                _logger.LogWarning("Invalid token expiration format");
                return false;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiration)
            {
                _logger.LogWarning("Token has expired");
                return false;
            }

            // Parse IDs
            if (!Guid.TryParse(tokenResourceId, out var resourceId))
            {
                _logger.LogWarning("Invalid resource ID in token");
                return false;
            }

            if (!Guid.TryParse(tokenTenantId, out var tenantId))
            {
                _logger.LogWarning("Invalid tenant ID in token");
                return false;
            }

            claims = new FileAccessTokenClaims(tokenResourceType, resourceId, tenantId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token parsing failed with exception");
            return false;
        }
    }

    private string ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(_secretKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string Base64UrlDecode(string input)
    {
        var base64 = input
            .Replace('-', '+')
            .Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }
}
