using System.IdentityModel.Tokens.Jwt;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Phase 1 — covers TokenService.GenerateAccessToken's new auth_time and iat parameters.
/// The middleware <c>JwtMinIatMiddleware</c> reads these claims to make revocation
/// decisions, so the contract is: auth_time defaults to "now" on login, callers can
/// pass an explicit value to preserve across refresh, and the iat claim reflects what
/// the caller passed (not what the JWT library inferred from notBefore).
/// </summary>
public class TokenServiceTests
{
    private readonly TokenService _service;
    private readonly User _user;

    public TokenServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Issuer"] = "https://test.famick.com",
                ["JwtSettings:Audience"] = "https://test.famick.com",
                ["JwtSettings:ExpirationMinutes"] = "60"
            })
            .Build();

        IJwtSigningKeyService signingKeyService = new JwtSigningKeyService(
            configuration,
            NullLogger<JwtSigningKeyService>.Instance);

        _service = new TokenService(configuration, signingKeyService);

        _user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            TenantId = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "x",
            IsActive = true
        };
    }

    [Fact]
    public void GenerateAccessToken_emits_iat_and_auth_time_claims_defaulting_to_now()
    {
        var beforeIssuance = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = _service.GenerateAccessToken(_user, []);
        var afterIssuance = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var iat = long.Parse(jwt.Claims.Single(c => c.Type == "iat").Value);
        var authTime = long.Parse(jwt.Claims.Single(c => c.Type == "auth_time").Value);

        iat.Should().BeInRange(beforeIssuance, afterIssuance);
        authTime.Should().BeInRange(beforeIssuance, afterIssuance);
    }

    [Fact]
    public void GenerateAccessToken_preserves_explicit_authTime_across_refresh_simulation()
    {
        // Simulate a refresh: the caller passes the parent refresh token's AuthTime,
        // which is set on login and copied forward through every rotation. The
        // resulting access JWT must carry that exact auth_time, not "now".
        var originalLoginTime = DateTime.UtcNow.AddMinutes(-30);

        var token = _service.GenerateAccessToken(
            _user, [], roles: null, mustAcceptTerms: false,
            authTime: originalLoginTime);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var authTime = long.Parse(jwt.Claims.Single(c => c.Type == "auth_time").Value);

        authTime.Should().Be(new DateTimeOffset(originalLoginTime).ToUnixTimeSeconds());
    }

    [Fact]
    public void GenerateAccessToken_explicit_iat_overrides_default()
    {
        // Phase 1 — the change-password flow uses this to issue tokens with
        // iat = now_seconds + 1 so they survive the same-second jwt_min_iat bump.
        var pinnedIat = DateTime.UtcNow.AddSeconds(1);

        var token = _service.GenerateAccessToken(
            _user, [], roles: null, mustAcceptTerms: false,
            iat: pinnedIat);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var iat = long.Parse(jwt.Claims.Single(c => c.Type == "iat").Value);

        iat.Should().Be(new DateTimeOffset(pinnedIat).ToUnixTimeSeconds());
    }

    [Fact]
    public void GenerateAccessToken_authTime_defaults_to_iat_when_only_iat_is_passed()
    {
        // When only iat is overridden (e.g. change-password), authTime falls back to
        // the iat — these tokens are issued from a fresh first-factor authentication.
        var pinnedIat = DateTime.UtcNow.AddSeconds(5);

        var token = _service.GenerateAccessToken(
            _user, [], roles: null, mustAcceptTerms: false,
            iat: pinnedIat);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var authTime = long.Parse(jwt.Claims.Single(c => c.Type == "auth_time").Value);

        authTime.Should().Be(new DateTimeOffset(pinnedIat).ToUnixTimeSeconds());
    }

    [Fact]
    public void GenerateAccessToken_emits_kid_header_matching_signing_key()
    {
        var token = _service.GenerateAccessToken(_user, []);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Header.Kid.Should().NotBeNullOrEmpty(
            "JwksController matches tokens to public keys by kid");
    }

    [Fact]
    public void GenerateAccessToken_includes_must_change_password_claim_when_user_flag_set()
    {
        _user.MustChangePassword = true;
        try
        {
            var token = _service.GenerateAccessToken(_user, []);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            jwt.Claims.Should().Contain(c => c.Type == "must_change_password" && c.Value == "true");
        }
        finally
        {
            _user.MustChangePassword = false;
        }
    }

    [Fact]
    public void GenerateAccessToken_includes_roles_and_permissions()
    {
        var token = _service.GenerateAccessToken(
            _user, ["read", "write"], [Role.Admin]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "permission" && c.Value == "read");
        jwt.Claims.Should().Contain(c => c.Type == "permission" && c.Value == "write");
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Admin");
    }
}
