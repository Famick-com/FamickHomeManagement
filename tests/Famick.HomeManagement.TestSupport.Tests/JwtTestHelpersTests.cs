using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace Famick.HomeManagement.TestSupport.Tests;

/// <summary>
/// Verifies that <see cref="JwtTestHelpers"/> mints tokens that pass standard
/// <see cref="TokenValidationParameters"/> validation. If this breaks, every
/// Phase 1+ JWT-related test breaks too — so the test is small but load-bearing.
/// </summary>
public class JwtTestHelpersTests
{
    [Fact]
    public void Created_token_validates_against_the_same_key()
    {
        var key = JwtTestHelpers.CreateRsaKey();
        var token = JwtTestHelpers.CreateAccessToken(
            key,
            userId: Guid.NewGuid(),
            tenantId: Guid.NewGuid());

        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = "https://test.famick.com",
            ValidateAudience = true,
            ValidAudience = "https://test.famick.com",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };

        var act = () => handler.ValidateToken(token, parameters, out _);
        act.Should().NotThrow();
    }

    [Fact]
    public void Created_token_carries_auth_time_claim()
    {
        var key = JwtTestHelpers.CreateRsaKey();
        var authTime = DateTime.UtcNow.AddMinutes(-5);
        var token = JwtTestHelpers.CreateAccessToken(
            key,
            userId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            authTime: authTime);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var authTimeClaim = jwt.Claims.FirstOrDefault(c => c.Type == "auth_time");
        authTimeClaim.Should().NotBeNull();
        long.Parse(authTimeClaim!.Value)
            .Should()
            .BeCloseTo(new DateTimeOffset(authTime).ToUnixTimeSeconds(), 1);
    }

    [Fact]
    public void Expired_token_helper_produces_a_token_that_fails_lifetime_validation()
    {
        var key = JwtTestHelpers.CreateRsaKey();
        var token = JwtTestHelpers.CreateExpiredAccessToken(
            key,
            userId: Guid.NewGuid(),
            tenantId: Guid.NewGuid());

        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            IssuerSigningKey = key,
            ValidIssuer = "https://test.famick.com",
            ValidAudience = "https://test.famick.com",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var act = () => handler.ValidateToken(token, parameters, out _);
        act.Should().Throw<SecurityTokenExpiredException>();
    }
}
