using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Messaging.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// When a social sign-in may reach an existing account by its email, driven through the browser
/// code-exchange callback for each provider.
/// <list type="bullet">
/// <item>The email counts only when the provider marks it verified, as on the native paths. A
/// self-hosted OIDC provider that never sends the claim can be trusted by setting.</item>
/// <item>A verified email still never links an account that has a password or passkey: its owner
/// signs in and links the provider from settings.</item>
/// <item>An existing (provider, subject) link signs in whatever the email says.</item>
/// </list>
/// </summary>
public class ExternalAuthServiceEmailLinkingTests
{
    private const string Email = "resident@example.com";
    private const string Callback = "https://app.famick.com/callback";
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly string _dbName = $"email-linking-{Guid.NewGuid()}";

    /// <summary>The claims the stub token endpoints put in the next id_token.</summary>
    private readonly List<Claim> _idTokenClaims = [];

    [Theory]
    [InlineData("GOOGLE")]
    [InlineData("APPLE")]
    [InlineData("OIDC")]
    public async Task An_unverified_email_links_nothing(string provider)
    {
        SeedUser(password: false);
        TokenSays(Email, emailVerified: "false");

        var act = () => SignInAsync(provider);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        (await LinkCountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("GOOGLE")]
    [InlineData("APPLE")]
    [InlineData("OIDC")]
    public async Task An_email_with_no_verified_claim_links_nothing(string provider)
    {
        SeedUser(password: false);
        TokenSays(Email, emailVerified: null);

        var act = () => SignInAsync(provider);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        (await LinkCountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("GOOGLE")]
    [InlineData("APPLE")]
    [InlineData("OIDC")]
    public async Task A_verified_email_links_an_account_with_no_password_or_passkey(string provider)
    {
        var user = SeedUser(password: false);
        TokenSays(Email, emailVerified: "true");

        var response = await SignInAsync(provider);

        response.User.Id.Should().Be(user.Id);
        (await LinkCountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("GOOGLE")]
    [InlineData("APPLE")]
    [InlineData("OIDC")]
    public async Task A_verified_email_does_not_link_an_account_with_a_password(string provider)
    {
        SeedUser(password: true);
        TokenSays(Email, emailVerified: "true");

        var act = () => SignInAsync(provider);

        (await act.Should().ThrowAsync<InvalidCredentialsException>())
            .WithMessage("*already exists*link*settings*");
        (await LinkCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_verified_email_does_not_link_an_account_with_a_passkey()
    {
        var user = SeedUser(password: false);
        using (var db = NewDb())
        {
            db.UserPasskeyCredentials.Add(new UserPasskeyCredential
            {
                Id = Guid.NewGuid(), TenantId = TenantId, UserId = user.Id,
                CredentialId = "credential", PublicKey = "key", CreatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
        }
        TokenSays(Email, emailVerified: "true");

        var act = () => SignInAsync("GOOGLE");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        (await LinkCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task An_existing_link_signs_in_whatever_the_email_says()
    {
        // The account's owner linked it from settings; the email no longer decides anything.
        var user = SeedUser(password: true);
        using (var db = NewDb())
        {
            db.UserExternalLogins.Add(new UserExternalLogin
            {
                Id = Guid.NewGuid(), TenantId = TenantId, UserId = user.Id,
                Provider = "GOOGLE", ProviderUserId = SubjectFor(Email),
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
        }
        TokenSays(Email, emailVerified: "false");

        var response = await SignInAsync("GOOGLE");

        response.User.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task A_self_hosted_oidc_provider_can_be_trusted_without_the_claim()
    {
        var user = SeedUser(password: false);
        TokenSays(Email, emailVerified: null);

        var response = await SignInAsync("OIDC", trustUnverifiedOidcEmail: true);

        response.User.Id.Should().Be(user.Id);
    }

    [Theory]
    [InlineData("GOOGLE")]
    [InlineData("APPLE")]
    public async Task The_oidc_trust_setting_does_not_extend_to_google_or_apple(string provider)
    {
        SeedUser(password: false);
        TokenSays(Email, emailVerified: null);

        var act = () => SignInAsync(provider, trustUnverifiedOidcEmail: true);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        (await LinkCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task The_oidc_trust_setting_still_does_not_link_an_account_with_a_password()
    {
        SeedUser(password: true);
        TokenSays(Email, emailVerified: null);

        var act = () => SignInAsync("OIDC", trustUnverifiedOidcEmail: true);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        (await LinkCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Completing_a_signup_needs_a_password_or_passkey()
    {
        // The request used to accept a provider name and an unchecked provider "token", which was
        // stored as the provider account id and let the signup skip the password.
        using var db = NewDb();
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            Email = "new@example.com",
            HouseholdName = "New Household",
            TokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("signup-token"))),
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            VerifiedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        var registration = new RegistrationService(
            db, Mock.Of<IEmailService>(), Mock.Of<IMessageService>(), Mock.Of<IPasswordHasher>(),
            Mock.Of<IPasskeyService>(), Mock.Of<ITokenService>(), new ConfigurationBuilder().Build(),
            NullLogger<RegistrationService>.Instance);

        var response = await registration.CompleteRegistrationAsync(
            new CompleteRegistrationRequest { Token = "signup-token", FirstName = "New", LastName = "Person" },
            "127.0.0.1", "tests");

        response.Success.Should().BeFalse();
        (await db.Users.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
        (await db.UserExternalLogins.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    // --- harness ---

    private void TokenSays(string email, string? emailVerified)
    {
        _idTokenClaims.Clear();
        _idTokenClaims.Add(new Claim("sub", SubjectFor(email)));
        _idTokenClaims.Add(new Claim("email", email));
        if (emailVerified is not null)
        {
            _idTokenClaims.Add(new Claim("email_verified", emailVerified, ClaimValueTypes.Boolean));
        }
    }

    private static string SubjectFor(string email) => $"sub-for-{email}";

    private async Task<LoginResponse> SignInAsync(string provider, bool trustUnverifiedOidcEmail = false)
    {
        var sut = BuildService(trustUnverifiedOidcEmail);
        var challenge = await sut.GetAuthorizationUrlAsync(provider, Callback, CancellationToken.None);
        return await sut.ProcessCallbackAsync(
            provider,
            new ExternalAuthCallbackRequest { Code = "code", State = challenge.State },
            Callback, "127.0.0.1", "tests", CancellationToken.None);
    }

    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private ExternalAuthService BuildService(bool trustUnverifiedOidcEmail)
    {
        using var appleKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var settings = new ExternalAuthSettings
        {
            Google = new GoogleAuthSettings { Enabled = true, ClientId = "google-client", ClientSecret = "google-secret" },
            Apple = new AppleAuthSettings
            {
                Enabled = true, ClientId = "com.famick.web", TeamId = "TEAMID", KeyId = "KEYID",
                PrivateKey = appleKey.ExportPkcs8PrivateKeyPem(),
            },
            OpenIdConnect = new OidcAuthSettings
            {
                Enabled = true, Authority = "https://idp.example.com", ClientId = "oidc-client",
                ClientSecret = "oidc-secret", TrustUnverifiedEmail = trustUnverifiedOidcEmail,
            },
        };

        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.GenerateAccessToken(
                It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Role>?>(),
                It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns("access-token");
        tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");
        tokenService.Setup(t => t.GetTokenExpiration()).Returns(DateTime.UtcNow.AddHours(1));

        // An existing user means the first-run branch never runs, so no SelfHosted:TenantId.
        return new ExternalAuthService(
            NewDb(), tokenService.Object, new ConfigurationBuilder().Build(), Mock.Of<IContactService>(),
            _cache, HttpClients(), Options.Create(settings), NullLogger<ExternalAuthService>.Instance);
    }

    private IHttpClientFactory HttpClients()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath.EndsWith("/.well-known/openid-configuration"))
            {
                return Json("""
                    {
                      "issuer": "https://idp.example.com",
                      "authorization_endpoint": "https://idp.example.com/authorize",
                      "token_endpoint": "https://idp.example.com/token",
                      "jwks_uri": "https://idp.example.com/jwks"
                    }
                    """);
            }

            if (uri.AbsoluteUri is "https://oauth2.googleapis.com/token"
                or "https://appleid.apple.com/auth/token"
                or "https://idp.example.com/token")
            {
                return Json($$"""{"id_token":"{{IdToken()}}"}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        return factory.Object;
    }

    private string IdToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 64)));
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://idp.example.com",
            Subject = new ClaimsIdentity(_idTokenClaims),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        }));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private HomeManagementDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(_dbName).Options);

    private User SeedUser(bool password)
    {
        using var db = NewDb();
        db.Tenants.Add(new Tenant { Id = TenantId, Name = "Test Household", CreatedAt = DateTime.UtcNow });
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = TenantId, Email = Email, Username = Email,
            FirstName = "Resident", LastName = "Example",
            PasswordHash = password ? "a-password-hash" : string.Empty,
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private async Task<int> LinkCountAsync()
    {
        using var db = NewDb();
        return await db.UserExternalLogins.IgnoreQueryFilters().CountAsync();
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }
}
