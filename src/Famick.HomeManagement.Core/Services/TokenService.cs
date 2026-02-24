using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public TokenService(IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");

        _secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");
        _issuer = jwtSettings["Issuer"]
            ?? throw new InvalidOperationException("JWT Issuer not configured");
        _audience = jwtSettings["Audience"]
            ?? throw new InvalidOperationException("JWT Audience not configured");
        _expirationMinutes = jwtSettings.GetValue<int>("ExpirationMinutes", 60);

        if (_secretKey.Length < 32)
        {
            throw new InvalidOperationException("JWT SecretKey must be at least 32 characters long");
        }
    }

    /// <inheritdoc />
    public string GenerateAccessToken(User user, IEnumerable<string> permissions, IEnumerable<Role>? roles = null, bool mustAcceptTerms = false)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("tenant_id", user.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now.AddSeconds(-1),
            expires: now.AddMinutes(_expirationMinutes),
            signingCredentials: credentials
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
        var key = Encoding.UTF8.GetBytes(_secretKey);

        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
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
