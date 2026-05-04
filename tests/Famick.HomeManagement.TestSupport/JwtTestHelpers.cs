using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Famick.HomeManagement.TestSupport;

/// <summary>
/// Mints JWTs with arbitrary claims for tests. Mirrors the claim-emission shape used
/// by <c>Famick.HomeManagement.Core.Services.TokenService</c> (sub, email, tenant_id,
/// jti, iat — and Phase 1+: auth_time) so tokens minted here validate cleanly against
/// the production validator when configured with the matching <see cref="RsaSecurityKey"/>.
///
/// Phase 1 uses these helpers heavily for jwt_min_iat / auth_time / rotation tests.
/// </summary>
public static class JwtTestHelpers
{
    private const string DefaultIssuer = "https://test.famick.com";
    private const string DefaultAudience = "https://test.famick.com";

    /// <summary>
    /// Creates a fresh RSA-2048 key for use as both the signing key (in tests that
    /// mint tokens) and the validation key (in tests that verify them). Each call
    /// produces a new keypair — share the same instance across an issue/validate
    /// pair if you want validation to succeed.
    /// </summary>
    public static RsaSecurityKey CreateRsaKey(string? kid = null)
    {
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa)
        {
            KeyId = kid ?? ComputeKeyId(rsa)
        };
    }

    /// <summary>
    /// Mints an access JWT with arbitrary claims and timing.
    /// </summary>
    /// <param name="key">Signing key. Use <see cref="CreateRsaKey"/> for ephemeral test keys.</param>
    /// <param name="userId">Becomes the <c>sub</c> claim.</param>
    /// <param name="tenantId">Becomes the <c>tenant_id</c> claim.</param>
    /// <param name="email">Becomes the <c>email</c> claim. Default <c>test@example.com</c>.</param>
    /// <param name="iat">Issued-at timestamp. Defaults to <see cref="DateTime.UtcNow"/>.</param>
    /// <param name="authTime">
    /// <c>auth_time</c> claim — the time of most-recent first-factor authentication.
    /// Defaults to <paramref name="iat"/>.
    /// </param>
    /// <param name="expires">Expiration timestamp. Defaults to <paramref name="iat"/> + 60 minutes.</param>
    /// <param name="issuer">JWT <c>iss</c>. Default <c>https://test.famick.com</c>.</param>
    /// <param name="audience">JWT <c>aud</c>. Default <c>https://test.famick.com</c>.</param>
    /// <param name="extraClaims">Additional claims appended after the defaults.</param>
    public static string CreateAccessToken(
        RsaSecurityKey key,
        Guid userId,
        Guid tenantId,
        string? email = null,
        DateTime? iat = null,
        DateTime? authTime = null,
        DateTime? expires = null,
        string? issuer = null,
        string? audience = null,
        IEnumerable<Claim>? extraClaims = null)
    {
        var issuedAt = iat ?? DateTime.UtcNow;
        var authenticatedAt = authTime ?? issuedAt;
        var expiresAt = expires ?? issuedAt.AddMinutes(60);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email ?? "test@example.com"),
            new("tenant_id", tenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new("auth_time",
                new DateTimeOffset(authenticatedAt).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        if (extraClaims is not null)
        {
            claims.AddRange(extraClaims);
        }

        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: issuer ?? DefaultIssuer,
            audience: audience ?? DefaultAudience,
            claims: claims,
            notBefore: issuedAt.AddSeconds(-1),
            expires: expiresAt,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Mints an access JWT whose <c>exp</c> is already in the past. Convenience for
    /// tests that exercise revocation / refresh / step-up paths.
    /// </summary>
    public static string CreateExpiredAccessToken(
        RsaSecurityKey key,
        Guid userId,
        Guid tenantId,
        string? email = null,
        IEnumerable<Claim>? extraClaims = null)
    {
        var issuedAt = DateTime.UtcNow.AddMinutes(-120);
        return CreateAccessToken(
            key,
            userId,
            tenantId,
            email: email,
            iat: issuedAt,
            authTime: issuedAt,
            expires: issuedAt.AddMinutes(60),
            extraClaims: extraClaims);
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(publicKeyBytes);
        return Base64UrlEncoder.Encode(hash);
    }
}
