using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Authentication;

/// <summary>
/// Phase 5 chunk 5.D — multi-issuer JWT validation.
///
/// Each Web host in the Phase 5 architecture mints JWTs with its own issuer
/// string but validates against a shared <c>ValidIssuers</c> array so tokens
/// from the other hosts validate during the parallel-window cutover. The
/// Issuer config keys in production are:
/// <list type="bullet">
/// <item>cloud Web (app.famick.com): <c>Famick.HomeManagement.Cloud</c></item>
/// <item>auth proxy (auth.famick.com): <c>https://auth.famick.com</c></item>
/// <item>self-hosted (private install): <c>Famick.HomeManagement</c></item>
/// </list>
///
/// All three Program.cs files use the same <c>ValidIssuers</c> assembly
/// pattern: read the configured <c>JwtSettings:Issuers</c> array, then
/// always append the singular <c>JwtSettings:Issuer</c> so locally-minted
/// tokens validate even when the array is unset or doesn't include the
/// minting issuer (e.g. in dev, where the Issuer overrides to
/// <c>https://localhost</c> but the base Issuers array doesn't).
///
/// These tests exercise the JWT validation framework with multi-issuer
/// <see cref="TokenValidationParameters"/> directly — proving the
/// contract the three Program.cs files rely on. They do NOT boot a
/// WebApplicationFactory; the per-host wiring is exercised by chunk 5.C's
/// AuthProxy.Web test and (later) chunk 5.M's cross-host parity test.
/// </summary>
public class MultiIssuerJwtTests
{
    private const string CloudIssuer = "Famick.HomeManagement.Cloud";
    private const string AuthProxyIssuer = "https://auth.famick.com";
    private const string SelfHostedIssuer = "Famick.HomeManagement";
    private const string TestAudience = "Famick.HomeManagement.Cloud.Api";

    [Fact]
    public void Cloud_issued_token_validates_when_cloud_issuer_in_valid_issuers()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var token = MintToken(signingKey, issuer: CloudIssuer);

        var validIssuers = new[] { CloudIssuer, AuthProxyIssuer };
        var principal = ValidateToken(token, signingKey, validIssuers);

        principal.Identity!.IsAuthenticated.Should().BeTrue();
        principal.FindFirst("iss")!.Value.Should().Be(CloudIssuer);
    }

    [Fact]
    public void Auth_proxy_issued_token_validates_when_auth_proxy_issuer_in_valid_issuers()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var token = MintToken(signingKey, issuer: AuthProxyIssuer);

        var validIssuers = new[] { CloudIssuer, AuthProxyIssuer };
        var principal = ValidateToken(token, signingKey, validIssuers);

        principal.Identity!.IsAuthenticated.Should().BeTrue();
        principal.FindFirst("iss")!.Value.Should().Be(AuthProxyIssuer);
    }

    [Fact]
    public void Self_hosted_validates_both_self_and_auth_proxy_issuers()
    {
        // Self-hosted's ValidIssuers shape in production. Proves a self-hosted
        // server can validate JWTs minted by either its own TokenService or by
        // auth.famick.com — the cross-trust premise the Phase 5 design relies on.
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var selfToken = MintToken(signingKey, issuer: SelfHostedIssuer);
        var authToken = MintToken(signingKey, issuer: AuthProxyIssuer);

        var selfHostedValidIssuers = new[] { SelfHostedIssuer, AuthProxyIssuer };

        ValidateToken(selfToken, signingKey, selfHostedValidIssuers).Identity!.IsAuthenticated.Should().BeTrue();
        ValidateToken(authToken, signingKey, selfHostedValidIssuers).Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void Token_with_unrecognized_issuer_is_rejected()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var attackerToken = MintToken(signingKey, issuer: "https://attacker.example.com");

        var validIssuers = new[] { CloudIssuer, AuthProxyIssuer };

        var act = () => ValidateToken(attackerToken, signingKey, validIssuers);

        act.Should().Throw<SecurityTokenInvalidIssuerException>(
            "validation MUST reject tokens whose iss is not in ValidIssuers, even with a valid signature");
    }

    [Fact]
    public void Token_with_unrecognized_issuer_is_rejected_even_with_valid_signature_from_shared_key()
    {
        // Defense-in-depth: signature being valid is necessary but not sufficient.
        // An attacker who somehow obtained the signing key still couldn't forge
        // a token with an arbitrary issuer past the iss check.
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var token = MintToken(signingKey, issuer: "Famick.HomeManagement");

        // ValidIssuers explicitly does NOT include the self-hosted issuer
        // (simulates an AuthProxy.Web instance receiving a self-hosted-minted token).
        var validIssuers = new[] { CloudIssuer, AuthProxyIssuer };

        var act = () => ValidateToken(token, signingKey, validIssuers);

        act.Should().Throw<SecurityTokenInvalidIssuerException>();
    }

    [Fact]
    public void Empty_valid_issuers_rejects_all_tokens()
    {
        // Sanity check that the test harness isn't trivially accepting.
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var token = MintToken(signingKey, issuer: CloudIssuer);

        var act = () => ValidateToken(token, signingKey, Array.Empty<string>());

        act.Should().Throw<SecurityTokenInvalidIssuerException>();
    }

    private static string MintToken(SecurityKey signingKey, string issuer)
    {
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = TestAudience,
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "test-user") }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static ClaimsPrincipal ValidateToken(string token, SecurityKey signingKey, IEnumerable<string> validIssuers)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuers = validIssuers,
            ValidAudience = TestAudience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero,
        }, out _);
    }
}
