using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Famick.HomeManagement.Core.Services;

/// <summary>
/// JWT token generation and validation service
/// </summary>
public class TokenService : ITokenService
{
    private readonly IJwtSigningKeyService _signingKeyService;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public TokenService(IConfiguration configuration, IJwtSigningKeyService signingKeyService)
    {
        _signingKeyService = signingKeyService;

        var jwtSettings = configuration.GetSection("JwtSettings");

        _issuer = jwtSettings["Issuer"]
            ?? throw new InvalidOperationException("JWT Issuer not configured");
        _audience = jwtSettings["Audience"]
            ?? throw new InvalidOperationException("JWT Audience not configured");
        _expirationMinutes = jwtSettings.GetValue<int>("ExpirationMinutes", 60);
    }

    /// <inheritdoc />
    public string GenerateAccessToken(
        User user,
        IEnumerable<string> permissions,
        IEnumerable<Role>? roles = null,
        bool mustAcceptTerms = false,
        DateTime? authTime = null,
        DateTime? iat = null)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var issuedAt = iat ?? DateTime.UtcNow;
        var authenticatedAt = authTime ?? issuedAt;
        var authTimeUnixSeconds = new DateTimeOffset(authenticatedAt).ToUnixTimeSeconds();
        var iatUnixSeconds = new DateTimeOffset(issuedAt).ToUnixTimeSeconds();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("tenant_id", user.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // iat as an explicit integer claim. JWT libraries also emit one based on
            // the token's notBefore/issuedAt — but we want the value the
            // jwt_min_iat middleware reads to be the one we computed (so the
            // change-password flow can pin iat = now_seconds + 1 and survive its
            // own bump). Phase 1: middleware reads this claim by name.
            new(JwtRegisteredClaimNames.Iat, iatUnixSeconds.ToString(),
                ClaimValueTypes.Integer64),
            // auth_time = most recent first-factor authentication. Login = now.
            // /auth/refresh preserves verbatim from the parent refresh token. Step-up
            // re-auth = now. Read by the (Phase 2) step-up middleware.
            new("auth_time", authTimeUnixSeconds.ToString(),
                ClaimValueTypes.Integer64)
        };

        // Add must_change_password claim if the user needs to change their password
        if (user.MustChangePassword)
        {
            claims.Add(new Claim("must_change_password", "true"));
        }

        // Add must_accept_terms claim if the user needs to accept terms (cloud only)
        if (mustAcceptTerms)
        {
            claims.Add(new Claim("must_accept_terms", "true"));
        }

        // Add permissions as separate claims
        foreach (var permission in permissions ?? Enumerable.Empty<string>())
        {
            claims.Add(new Claim("permission", permission));
        }

        // Add roles as separate claims
        foreach (var role in roles ?? Enumerable.Empty<Role>())
        {
            claims.Add(new Claim("role", role.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: issuedAt.AddSeconds(-1),
            expires: issuedAt.AddMinutes(_expirationMinutes),
            signingCredentials: _signingKeyService.SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    /// <inheritdoc />
    public Guid? ValidateAccessToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                // Phase 1 — accept any key currently active (current + previous-during-overlap).
                // The JWT library matches the token's kid header against this collection.
                IssuerSigningKeys = _signingKeyService.ActiveValidationKeys,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            // Note: JWT "sub" claim gets mapped to ClaimTypes.NameIdentifier during validation
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                return null;
            }

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public DateTime GetTokenExpiration()
    {
        return DateTime.UtcNow.AddMinutes(_expirationMinutes);
    }
}
