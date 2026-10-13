using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Shared.Net;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// The native Google and Apple sign-in endpoints take an ID token from the request body. They
/// used to check issuer, audience and expiry and then trust the rest, so anyone who could write
/// a JWT with our public client id and a victim's email got the victim's session. These tests
/// pin the fix: the provider's RS256 signature is verified against its JWKS, the email only
/// counts when the provider marks it verified, the request body never supplies it, and an
/// existing (provider, subject) link wins over the email match.
/// </summary>
public class ExternalAuthServiceNativeIdTokenTests : IDisposable
{
    private const string GoogleClientId = "web-client.apps.googleusercontent.com";
    private const string AppleBundleId = "com.famick.homemanagement";
    private const string VictimEmail = "victim@example.com";

    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly RSA _providerKey = RSA.Create(2048);
    private readonly RSA _attackerKey = RSA.Create(2048);
    private readonly string _dbName = $"native-idtoken-{Guid.NewGuid()}";
    private int _jwksFetches;

    /// <summary>The key set the stub JWKS endpoint serves. Tests swap it to simulate rotation.</summary>
    private (RSA Key, string Kid) _published;

    public ExternalAuthServiceNativeIdTokenTests()
    {
        _published = (_providerKey, "provider-key-1");
    }

    public void Dispose()
    {
        _providerKey.Dispose();
        _attackerKey.Dispose();
    }

    // ---------------------------------------------------------------------
    // Signature
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Google_ValidlySignedToken_SignsInTheMatchingUser()
    {
        var victim = SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db);

        var response = await sut.ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = GoogleToken(VictimEmail) }, "127.0.0.1", "tests");

        response.User.Id.Should().Be(victim.Id);
    }

    [Fact]
    public async Task Google_ValidPayloadWithForgedSignature_IsRejected_AndLinksNothing()
    {
        // The attack from the finding: right issuer, right audience, future expiry, victim's
        // email, and a signature from a key the provider never published.
        SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db);

        var forged = GoogleToken(VictimEmail, signingKey: _attackerKey, kid: "provider-key-1");
        var act = () => sut.ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = forged }, "127.0.0.1", "tests");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        (await db.UserExternalLogins.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await db.RefreshTokens.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Google_UnsignedToken_IsRejected()
    {
        SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db);

        var unsigned = new JsonWebTokenHandler().CreateToken(Descriptor(
            GoogleIssuer, GoogleClientId, GoogleClaims(VictimEmail), signing: null));

        var act = () => sut.ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = unsigned }, "127.0.0.1", "tests");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Google_HmacSignedToken_IsRejected()
    {
        // Algorithm pinning: an HS256 token must not be accepted however it was keyed.
        SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db);

        var hmac = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 64))) { KeyId = "provider-key-1" },
            SecurityAlgorithms.HmacSha256);
        var token = new JsonWebTokenHandler().CreateToken(Descriptor(
            GoogleIssuer, GoogleClientId, GoogleClaims(VictimEmail), hmac));

        var act = () => sut.ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = token }, "127.0.0.1", "tests");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Theory]
    [InlineData("https://evil.example.com", GoogleClientId)]
    [InlineData(GoogleIssuer, "someone-elses-client")]
    public async Task Google_SignedTokenForTheWrongIssuerOrAudience_IsRejected(string issuer, string audience)
    {
        SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db);

        var token = SignedToken(issuer, audience, GoogleClaims(VictimEmail));
        var act = () => sut.ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = token }, "127.0.0.1", "tests");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Google_ExpiredToken_IsRejected()
    {
        SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db);

        var token = SignedToken(GoogleIssuer, GoogleClientId, GoogleClaims(VictimEmail),
            expires: DateTime.UtcNow.AddMinutes(-10));
        var act = () => sut.ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = token }, "127.0.0.1", "tests");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Google_KeyRotation_IsPickedUpByRefetchingTheJwks()
    {
        var victim = SeedUser(VictimEmail);
        using var db = NewDb();
        var cache = new MemoryCache(new MemoryCacheOptions());

        // Warm the cache with the first key.
        await BuildService(db, cache).ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = GoogleToken(VictimEmail) }, "127.0.0.1", "tests");
        _jwksFetches.Should().Be(1);

        // The provider rotates; a token under the new kid must not need a restart.
        using var rotated = RSA.Create(2048);
        _published = (rotated, "provider-key-2");
        var token = GoogleToken(VictimEmail, signingKey: rotated, kid: "provider-key-2");

        var response = await BuildService(db, cache).ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = token }, "127.0.0.1", "tests");

        response.User.Id.Should().Be(victim.Id);
        _jwksFetches.Should().Be(2);
    }

    [Fact]
    public async Task Google_UnknownKids_RefetchTheJwksAtMostOncePerInterval()
    {
        // Tokens with made-up kids must not become one request to Google apiece.
        SeedUser(VictimEmail);
        using var db = NewDb();
        var cache = new MemoryCache(new MemoryCacheOptions());

        await BuildService(db, cache).ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = GoogleToken(VictimEmail) }, "127.0.0.1", "tests");

        for (var i = 0; i < 5; i++)
        {
            var token = GoogleToken(VictimEmail, signingKey: _attackerKey, kid: $"made-up-{i}");
            var act = () => BuildService(db, cache).ProcessNativeGoogleSignInAsync(
                new NativeGoogleSignInRequest { IdToken = token }, "127.0.0.1", "tests");
            await act.Should().ThrowAsync<InvalidCredentialsException>();
        }

        _jwksFetches.Should().Be(2);
    }

    // ---------------------------------------------------------------------
    // Email
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Google_UnverifiedEmail_CannotClaimAnAccountByAddress()
    {
        SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db);

        var token = SignedToken(GoogleIssuer, GoogleClientId, GoogleClaims(VictimEmail, emailVerified: false));
        var act = () => sut.ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = token }, "127.0.0.1", "tests");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        (await db.UserExternalLogins.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Google_ExistingLink_WinsOverTheTokensEmail()
    {
        // The subject is already linked to the owner. A token for that subject now carrying
        // someone else's address signs in the owner, not the address's account.
        var owner = SeedUser("owner@example.com");
        var other = SeedUser(VictimEmail);
        SeedLink(owner, "Google", "google-sub-1");

        using var db = NewDb();
        var sut = BuildService(db);

        var token = SignedToken(GoogleIssuer, GoogleClientId, GoogleClaims(VictimEmail, sub: "google-sub-1"));
        var response = await sut.ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = token }, "127.0.0.1", "tests");

        response.User.Id.Should().Be(owner.Id);
        response.User.Id.Should().NotBe(other.Id);
    }

    [Fact]
    public async Task Google_ExistingLink_SignsInEvenWhenTheTokenHasNoEmail()
    {
        var owner = SeedUser("owner@example.com");
        SeedLink(owner, "Google", "google-sub-1");

        using var db = NewDb();
        var sut = BuildService(db);

        var token = SignedToken(GoogleIssuer, GoogleClientId, [new Claim("sub", "google-sub-1")]);
        var response = await sut.ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = token }, "127.0.0.1", "tests");

        response.User.Id.Should().Be(owner.Id);
    }

    [Fact]
    public async Task Apple_EmailFromTheRequestBody_IsIgnored()
    {
        // A genuine Apple token with no email claim, plus the victim's address in the body.
        // The body used to fill the gap and resolve the victim's account.
        SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db);

        var token = SignedToken(AppleIssuer, AppleBundleId, [new Claim("sub", "apple-sub-1")]);
        var act = () => sut.ProcessNativeAppleSignInAsync(
            new NativeAppleSignInRequest { IdentityToken = token, Email = VictimEmail }, "127.0.0.1", "tests");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        (await db.UserExternalLogins.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Apple_ValidlySignedToken_WithStringEmailVerified_SignsIn()
    {
        var victim = SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db);

        var token = SignedToken(AppleIssuer, AppleBundleId,
        [
            new Claim("sub", "apple-sub-1"),
            new Claim("email", VictimEmail),
            new Claim("email_verified", "true"),
        ]);
        var response = await sut.ProcessNativeAppleSignInAsync(
            new NativeAppleSignInRequest { IdentityToken = token }, "127.0.0.1", "tests");

        response.User.Id.Should().Be(victim.Id);
    }

    [Fact]
    public async Task Apple_ForgedSignature_IsRejected()
    {
        SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db);

        var token = SignedToken(AppleIssuer, AppleBundleId,
            [new Claim("sub", "apple-sub-1"), new Claim("email", VictimEmail), new Claim("email_verified", "true")],
            signingKey: _attackerKey);
        var act = () => sut.ProcessNativeAppleSignInAsync(
            new NativeAppleSignInRequest { IdentityToken = token }, "127.0.0.1", "tests");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    // ---------------------------------------------------------------------
    // Link + enablement
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LinkNativeGoogle_WithForgedSignature_IsRejected()
    {
        var user = SeedUser("owner@example.com");
        using var db = NewDb();
        var sut = BuildService(db);

        var forged = GoogleToken("owner@example.com", signingKey: _attackerKey, kid: "provider-key-1");
        var act = () => sut.LinkNativeGoogleAsync(user.Id, new NativeGoogleSignInRequest { IdToken = forged });

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        (await db.UserExternalLogins.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Google_WhenDisabled_IsRejectedBeforeAnyKeyFetch()
    {
        SeedUser(VictimEmail);
        using var db = NewDb();
        var sut = BuildService(db, settings: Settings(googleEnabled: false));

        var act = () => sut.ProcessNativeGoogleSignInAsync(
            new NativeGoogleSignInRequest { IdToken = GoogleToken(VictimEmail) }, "127.0.0.1", "tests");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _jwksFetches.Should().Be(0);
    }

    [Fact]
    public async Task Controller_NativeEndpoints_Return404WhenTheProviderIsDisabled()
    {
        var service = new Mock<IExternalAuthService>(MockBehavior.Strict);
        var controller = new ExternalAuthApiController(
            service.Object,
            Mock.Of<IPasskeyService>(),
            new RedirectUrlValidator(Options.Create(new RedirectUriAllowListOptions { Hosts = [] })),
            Mock.Of<Famick.HomeManagement.FeatureFlags.IFeatureFlagService>(),
            Options.Create(new ExternalAuthSettings()),
            NullLogger<ExternalAuthApiController>.Instance);

        (await controller.ProcessNativeGoogleSignIn(new NativeGoogleSignInRequest { IdToken = "x" }, default))
            .Should().BeOfType<NotFoundResult>();
        (await controller.ProcessNativeAppleSignIn(new NativeAppleSignInRequest { IdentityToken = "x" }, default))
            .Should().BeOfType<NotFoundResult>();
        (await controller.LinkNativeGoogle(new NativeGoogleSignInRequest { IdToken = "x" }, default))
            .Should().BeOfType<NotFoundResult>();
        (await controller.LinkNativeApple(new NativeAppleSignInRequest { IdentityToken = "x" }, default))
            .Should().BeOfType<NotFoundResult>();
    }

    // ---------------------------------------------------------------------
    // Harness
    // ---------------------------------------------------------------------

    private const string GoogleIssuer = "https://accounts.google.com";
    private const string AppleIssuer = "https://appleid.apple.com";

    private static ExternalAuthSettings Settings(bool googleEnabled = true, bool appleEnabled = true) => new()
    {
        Google = new GoogleAuthSettings
        {
            Enabled = googleEnabled,
            ClientId = GoogleClientId,
            ClientSecret = "google-secret",
        },
        Apple = new AppleAuthSettings
        {
            Enabled = appleEnabled,
            ClientId = "com.famick.web",
            BundleId = AppleBundleId,
        },
    };

    private ExternalAuthService BuildService(
        HomeManagementDbContext db,
        IMemoryCache? cache = null,
        ExternalAuthSettings? settings = null)
    {
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.GenerateAccessToken(
                It.IsAny<User>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<Role>?>(),
                It.IsAny<bool>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .Returns("access-token");
        tokenService.Setup(t => t.GenerateRefreshToken()).Returns(() => Guid.NewGuid().ToString());
        tokenService.Setup(t => t.GetTokenExpiration()).Returns(DateTime.UtcNow.AddHours(1));

        return new ExternalAuthService(
            db,
            tokenService.Object,
            new ConfigurationBuilder().Build(),
            Mock.Of<IContactService>(),
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            JwksHttpClientFactory(),
            Options.Create(settings ?? Settings()),
            NullLogger<ExternalAuthService>.Instance);
    }

    private IHttpClientFactory JwksHttpClientFactory()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var uri = request.RequestUri!.ToString();
            if (uri != ExternalAuthService.GoogleJwksUri && uri != ExternalAuthService.AppleJwksUri)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            Interlocked.Increment(ref _jwksFetches);
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(
                new RsaSecurityKey(_published.Key.ExportParameters(includePrivateParameters: false)) { KeyId = _published.Kid });
            jwk.Use = "sig";
            jwk.Alg = SecurityAlgorithms.RsaSha256;
            var body = JsonSerializer.Serialize(new
            {
                keys = new[] { new { kty = jwk.Kty, use = jwk.Use, alg = jwk.Alg, kid = jwk.Kid, n = jwk.N, e = jwk.E } },
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        return factory.Object;
    }

    private static List<Claim> GoogleClaims(string email, bool emailVerified = true, string sub = "google-sub-1") =>
    [
        new Claim("sub", sub),
        new Claim("email", email),
        new Claim("email_verified", emailVerified ? "true" : "false", ClaimValueTypes.Boolean),
        new Claim("name", "Test Person"),
    ];

    private string GoogleToken(string email, RSA? signingKey = null, string? kid = null) =>
        SignedToken(GoogleIssuer, GoogleClientId, GoogleClaims(email), signingKey, kid);

    private string SignedToken(
        string issuer,
        string audience,
        IEnumerable<Claim> claims,
        RSA? signingKey = null,
        string? kid = null,
        DateTime? expires = null)
    {
        var key = new RsaSecurityKey(signingKey ?? _providerKey) { KeyId = kid ?? _published.Kid };
        var signing = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        return new JsonWebTokenHandler().CreateToken(Descriptor(issuer, audience, claims, signing, expires));
    }

    private static SecurityTokenDescriptor Descriptor(
        string issuer,
        string audience,
        IEnumerable<Claim> claims,
        SigningCredentials? signing,
        DateTime? expires = null)
    {
        var exp = expires ?? DateTime.UtcNow.AddMinutes(30);
        return new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = exp.AddMinutes(-60),
            NotBefore = exp.AddMinutes(-60),
            Expires = exp,
            SigningCredentials = signing,
        };
    }

    private HomeManagementDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(_dbName).Options);

    private User SeedUser(string email)
    {
        using var db = NewDb();
        if (!db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            db.Tenants.Add(new Tenant { Id = TenantId, Name = "Test Household", CreatedAt = DateTime.UtcNow });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Email = email,
            Username = email,
            FirstName = "Test",
            LastName = "Person",
            PasswordHash = "not-a-real-hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private void SeedLink(User user, string provider, string sub)
    {
        using var db = NewDb();
        db.UserExternalLogins.Add(new UserExternalLogin
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            Provider = provider,
            ProviderUserId = sub,
            ProviderEmail = user.Email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }
}
