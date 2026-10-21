using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Web;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// The browser sign-in and link flows: what the stored OAuth state binds a callback to. Each
/// challenge stores its state in the shared state store and puts a nonce in the authorization URL;
/// the callback must present that state once, for the flow and user it was issued to, and the
/// id_token from the code exchange must echo the nonce.
/// </summary>
public class ExternalAuthServiceStateTests
{
    private const string Callback = "https://app.famick.com/auth/external/callback/google";
    private const string Email = "member@example.com";
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly string _dbName = $"oauth-state-{Guid.NewGuid()}";
    private readonly IDistributedCache _stateStore =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    /// <summary>What the stub token endpoint signs into the next id_token's nonce claim.</summary>
    private string? _echoedNonce;

    private const string Subject = "google-sub-1";
    private int _codeExchanges;

    // ---------------------------------------------------------------------
    // Authorization URL
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("Google")]
    [InlineData("Apple")]
    [InlineData("OIDC")]
    public async Task AuthorizationUrl_CarriesAFreshNonce(string provider)
    {
        var sut = BuildService();

        var a = await sut.GetAuthorizationUrlAsync(provider, Callback);
        var b = await sut.GetLinkAuthorizationUrlAsync(Guid.NewGuid(), provider, Callback);

        NonceOf(a.AuthorizationUrl).Should().NotBeNullOrEmpty();
        NonceOf(b.AuthorizationUrl).Should().NotBeNullOrEmpty();
        NonceOf(a.AuthorizationUrl).Should().NotBe(NonceOf(b.AuthorizationUrl));
    }

    // ---------------------------------------------------------------------
    // Sign-in callback
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Callback_TokenEchoingTheNonce_SignsIn()
    {
        var user = SeedUser();
        var sut = BuildService();
        var challenge = await sut.GetAuthorizationUrlAsync("google", Callback);
        _echoedNonce = NonceOf(challenge.AuthorizationUrl);

        var response = await CallbackAsync(sut, challenge.State);

        response.User.Id.Should().Be(user.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("some-other-nonce")]
    public async Task Callback_TokenNotEchoingTheNonce_IsRejected(string? echoed)
    {
        SeedUser();
        var sut = BuildService();
        var challenge = await sut.GetAuthorizationUrlAsync("google", Callback);
        _echoedNonce = echoed;

        var act = () => CallbackAsync(sut, challenge.State);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Callback_StateIssuedOnAnotherInstance_IsAccepted()
    {
        // The provider can send the browser back to a different server instance than the one
        // that built the authorization URL. Each instance has its own memory cache; the state
        // store is the only thing they share.
        var user = SeedUser();
        var challenge = await BuildService().GetAuthorizationUrlAsync("google", Callback);
        _echoedNonce = NonceOf(challenge.AuthorizationUrl);

        var response = await CallbackAsync(BuildService(), challenge.State);

        response.User.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task Callback_StateCannotBeUsedTwice()
    {
        SeedUser();
        var sut = BuildService();
        var challenge = await sut.GetAuthorizationUrlAsync("google", Callback);
        _echoedNonce = NonceOf(challenge.AuthorizationUrl);
        await CallbackAsync(sut, challenge.State);

        var replay = () => CallbackAsync(sut, challenge.State);

        await replay.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Callback_WithALinkState_IsRejectedWithoutExchangingTheCode()
    {
        var user = SeedUser();
        var sut = BuildService();
        var challenge = await sut.GetLinkAuthorizationUrlAsync(user.Id, "google", Callback);
        _echoedNonce = NonceOf(challenge.AuthorizationUrl);

        var act = () => CallbackAsync(sut, challenge.State);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _codeExchanges.Should().Be(0);
    }

    // ---------------------------------------------------------------------
    // Link
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Link_StateIssuedToTheSameUser_LinksTheProvider()
    {
        var user = SeedUser();
        var sut = BuildService();
        var challenge = await sut.GetLinkAuthorizationUrlAsync(user.Id, "google", Callback);
        _echoedNonce = NonceOf(challenge.AuthorizationUrl);

        var linked = await LinkAsync(sut, user.Id, "google", challenge.State);

        linked.Provider.Should().Be("Google");
        using var db = NewDb();
        db.UserExternalLogins.IgnoreQueryFilters().Single().UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Link_StateIssuedToAnotherUser_IsRejectedAndLinksNothing()
    {
        // One user starts a link and hands the callback to another, signed-in user. The provider
        // identity belongs to the first user and must not end up on the second user's account.
        var starter = SeedUser("starter@example.com");
        var victim = SeedUser();
        var sut = BuildService();
        var challenge = await sut.GetLinkAuthorizationUrlAsync(starter.Id, "google", Callback);
        _echoedNonce = NonceOf(challenge.AuthorizationUrl);

        var act = () => LinkAsync(sut, victim.Id, "google", challenge.State);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _codeExchanges.Should().Be(0);
        using var db = NewDb();
        db.UserExternalLogins.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public async Task Link_WithASignInState_IsRejected()
    {
        var user = SeedUser();
        var sut = BuildService();
        var challenge = await sut.GetAuthorizationUrlAsync("google", Callback);
        _echoedNonce = NonceOf(challenge.AuthorizationUrl);

        var act = () => LinkAsync(sut, user.Id, "google", challenge.State);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _codeExchanges.Should().Be(0);
    }

    [Fact]
    public async Task Link_StateForAnotherProvider_IsRejected()
    {
        var user = SeedUser();
        var sut = BuildService();
        var challenge = await sut.GetLinkAuthorizationUrlAsync(user.Id, "oidc", Callback);
        _echoedNonce = NonceOf(challenge.AuthorizationUrl);

        var act = () => LinkAsync(sut, user.Id, "google", challenge.State);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _codeExchanges.Should().Be(0);
    }

    [Fact]
    public async Task Link_TokenNotEchoingTheNonce_IsRejected()
    {
        var user = SeedUser();
        var sut = BuildService();
        var challenge = await sut.GetLinkAuthorizationUrlAsync(user.Id, "google", Callback);
        _echoedNonce = "some-other-nonce";

        var act = () => LinkAsync(sut, user.Id, "google", challenge.State);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        using var db = NewDb();
        db.UserExternalLogins.IgnoreQueryFilters().Should().BeEmpty();
    }

    // ---------------------------------------------------------------------
    // Harness
    // ---------------------------------------------------------------------

    private Task<Core.DTOs.Authentication.LoginResponse> CallbackAsync(ExternalAuthService sut, string state) =>
        sut.ProcessCallbackAsync(
            "google",
            new ExternalAuthCallbackRequest { Code = "code", State = state },
            Callback, "127.0.0.1", "tests");

    private Task<LinkedAccountDto> LinkAsync(ExternalAuthService sut, Guid userId, string provider, string state) =>
        sut.LinkProviderAsync(
            userId,
            provider,
            new ExternalAuthLinkRequest { Code = "code", State = state },
            Callback);

    private static string? NonceOf(string authorizationUrl) =>
        HttpUtility.ParseQueryString(new Uri(authorizationUrl).Query)["nonce"];

    /// <summary>
    /// A fresh service with its own memory cache, as a separate server instance would have,
    /// sharing only the database and the state store.
    /// </summary>
    private ExternalAuthService BuildService()
    {
        var settings = new ExternalAuthSettings
        {
            Google = new GoogleAuthSettings { Enabled = true, ClientId = "google-client", ClientSecret = "google-secret" },
            Apple = new AppleAuthSettings { Enabled = true, ClientId = "com.famick.web" },
            OpenIdConnect = new OidcAuthSettings
            {
                Enabled = true, Authority = "https://idp.example.com", ClientId = "oidc-client", ClientSecret = "oidc-secret",
            },
        };

        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.GenerateAccessToken(
                It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Role>?>(),
                It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns("access-token");
        tokenService.Setup(t => t.GenerateRefreshToken()).Returns(() => Guid.NewGuid().ToString());
        tokenService.Setup(t => t.GetTokenExpiration()).Returns(DateTime.UtcNow.AddHours(1));

        return new ExternalAuthService(
            NewDb(), tokenService.Object, new ConfigurationBuilder().Build(), Mock.Of<IContactService>(),
            new MemoryCache(new MemoryCacheOptions()), _stateStore, HttpClients(),
            Options.Create(settings), NullLogger<ExternalAuthService>.Instance);
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
                      "token_endpoint": "https://idp.example.com/token"
                    }
                    """);
            }

            if (uri.AbsoluteUri is "https://oauth2.googleapis.com/token" or "https://idp.example.com/token")
            {
                Interlocked.Increment(ref _codeExchanges);
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
        var claims = new List<Claim>
        {
            new("sub", Subject),
            new("email", Email),
            new("email_verified", "true", ClaimValueTypes.Boolean),
        };
        if (_echoedNonce is not null)
        {
            claims.Add(new Claim("nonce", _echoedNonce));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 64)));
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://accounts.google.com",
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        }));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private HomeManagementDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(_dbName).Options);

    private User SeedUser(string email = Email)
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
            // No password or passkey, so a verified email may sign in to it by address.
            PasswordHash = string.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }
}
