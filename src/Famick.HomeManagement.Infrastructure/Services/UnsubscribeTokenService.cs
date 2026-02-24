using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// HMAC-signed token service for one-click email unsubscribe (RFC 8058).
/// Token format: unsubscribe|userId|tenantId|notificationType|expiration|signature (Base64URL encoded)
/// </summary>
public class UnsubscribeTokenService : IUnsubscribeTokenService
{
    private const int TokenExpirationDays = 7;

    private readonly byte[] _secretKey;
    private readonly ILogger<UnsubscribeTokenService> _logger;

    public UnsubscribeTokenService(string secretKey, ILogger<UnsubscribeTokenService> logger)
    {
        if (string.IsNullOrEmpty(secretKey) || secretKey.Length < 32)
        {
            throw new ArgumentException("Secret key must be at least 32 characters", nameof(secretKey));
        }

        _secretKey = Encoding.UTF8.GetBytes(secretKey);
        _logger = logger;
    }

    public string GenerateToken(Guid userId, Guid tenantId, NotificationType type)
    {
        var expiration = DateTimeOffset.UtcNow.AddDays(TokenExpirationDays).ToUnixTimeSeconds();
        var payload = $"unsubscribe|{userId}|{tenantId}|{type}|{expiration}";
        var signature = ComputeSignature(payload);

        var token = $"{payload}|{signature}";
        return Base64UrlEncode(token);
    }

    public bool TryParseToken(string token, out UnsubscribeTokenClaims? claims)
    {
        claims = null;

        try
        {
            var decoded = Base64UrlDecode(token);
            var parts = decoded.Split('|');

            if (parts.Length != 6 || parts[0] != "unsubscribe")
            {
                _logger.LogWarning("Invalid unsubscribe token format");
                return false;
            }

            var tokenUserId = parts[1];
            var tokenTenantId = parts[2];
            var tokenType = parts[3];
            var tokenExpiration = parts[4];
            var tokenSignature = parts[5];

            // Verify signature
            var payload = $"unsubscribe|{tokenUserId}|{tokenTenantId}|{tokenType}|{tokenExpiration}";
            var expectedSignature = ComputeSignature(payload);

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(tokenSignature),
                Encoding.UTF8.GetBytes(expectedSignature)))
            {
                _logger.LogWarning("Unsubscribe token signature validation failed");
                return false;
            }

            // Verify expiration
            if (!long.TryParse(tokenExpiration, out var expiration) ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiration)
            {
                _logger.LogWarning("Unsubscribe token has expired");
                return false;
            }

            // Parse claims
            if (!Guid.TryParse(tokenUserId, out var userId) ||
                !Guid.TryParse(tokenTenantId, out var tenantId) ||
                !Enum.TryParse<NotificationType>(tokenType, out var notificationType))
            {
                _logger.LogWarning("Invalid claims in unsubscribe token");
                return false;
            }

            claims = new UnsubscribeTokenClaims(userId, tenantId, notificationType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unsubscribe token parsing failed");
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
