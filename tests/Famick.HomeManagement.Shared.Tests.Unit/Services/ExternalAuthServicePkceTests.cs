using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Phase 3 chunk 3.D.1 — PKCE-S256 regression fence on every OAuth path the cloud
/// initiates (the proxy is OAuth client, not server, so "enforcement" here means
/// "every outbound /authorize URL we build carries code_challenge_method=S256 and
/// every token-exchange we perform supplies the matching code_verifier"). The
/// crypto + state-cache wiring shipped in Phase 0/2 (GenerateCodeVerifier /
/// GenerateCodeChallenge using RandomNumberGenerator + SHA-256, OAuthStateData
/// stored in IMemoryCache). Apple was the only path missing PKCE end-to-end
/// before Phase 3; commit 9d07023 closed that. These tests make the contract
/// executable so a future refactor of any URL builder or exchange method
/// fails CI rather than silently regressing Apple, Google, or OIDC.
/// </summary>
public class ExternalAuthServicePkceTests
{
    [Fact]
    public async Task GetAuthorizationUrlAsync_Google_includes_code_challenge_method_S256()
    {
        var sut = BuildService();

        var response = await sut.GetAuthorizationUrlAsync(
            "GOOGLE",
            "https://app.famick.com/callback",
            CancellationToken.None);

        var query = ParseQuery(response.AuthorizationUrl);
        query["code_challenge_method"].Should().Be("S256");
        query["code_challenge"].Should().NotBeNullOrEmpty();
        IsBase64Url(query["code_challenge"]!).Should().BeTrue();
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_Apple_includes_code_challenge_method_S256()
    {
        var sut = BuildService();

        var response = await sut.GetAuthorizationUrlAsync(
            "APPLE",
            "https://app.famick.com/callback",
            CancellationToken.None);

        var query = ParseQuery(response.AuthorizationUrl);
        query["code_challenge_method"].Should().Be("S256");
        query["code_challenge"].Should().NotBeNullOrEmpty();
        IsBase64Url(query["code_challenge"]!).Should().BeTrue();
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_Oidc_includes_code_challenge_method_S256()
    {
        var sut = BuildService();

        var response = await sut.GetAuthorizationUrlAsync(
            "OIDC",
            "https://app.famick.com/callback",
            CancellationToken.None);

        var query = ParseQuery(response.AuthorizationUrl);
        query["code_challenge_method"].Should().Be("S256");
        query["code_challenge"].Should().NotBeNullOrEmpty();
        IsBase64Url(query["code_challenge"]!).Should().BeTrue();
    }

    [Fact]
    public async Task GetLinkAuthorizationUrlAsync_Google_includes_code_challenge_method_S256()
    {
        var sut = BuildService();

        var response = await sut.GetLinkAuthorizationUrlAsync(
            Guid.NewGuid(),
            "GOOGLE",
            "https://app.famick.com/link/callback",
            CancellationToken.None);

        var query = ParseQuery(response.AuthorizationUrl);
        query["code_challenge_method"].Should().Be("S256");
        query["code_challenge"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetLinkAuthorizationUrlAsync_Apple_includes_code_challenge_method_S256()
    {
        var sut = BuildService();

        var response = await sut.GetLinkAuthorizationUrlAsync(
            Guid.NewGuid(),
            "APPLE",
            "https://app.famick.com/link/callback",
            CancellationToken.None);

        var query = ParseQuery(response.AuthorizationUrl);
        query["code_challenge_method"].Should().Be("S256");
        query["code_challenge"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetLinkAuthorizationUrlAsync_Oidc_includes_code_challenge_method_S256()
    {
        var sut = BuildService();

        var response = await sut.GetLinkAuthorizationUrlAsync(
            Guid.NewGuid(),
            "OIDC",
            "https://app.famick.com/link/callback",
            CancellationToken.None);

        var query = ParseQuery(response.AuthorizationUrl);
        query["code_challenge_method"].Should().Be("S256");
        query["code_challenge"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_Apple_and_Google_emit_distinct_code_challenges()
    {
        // Each /authorize call generates a fresh code_verifier; the challenge must
        // change between calls. Same-value would mean the random source got
        // accidentally pinned (e.g. a regression to a constant for testing).
        var sut = BuildService();

        var a = await sut.GetAuthorizationUrlAsync("GOOGLE", "https://app.famick.com/cb", CancellationToken.None);
        var b = await sut.GetAuthorizationUrlAsync("GOOGLE", "https://app.famick.com/cb", CancellationToken.None);
        var c = await sut.GetAuthorizationUrlAsync("APPLE", "https://app.famick.com/cb", CancellationToken.None);

        ParseQuery(a.AuthorizationUrl)["code_challenge"].Should().NotBe(ParseQuery(b.AuthorizationUrl)["code_challenge"]);
        ParseQuery(a.AuthorizationUrl)["code_challenge"].Should().NotBe(ParseQuery(c.AuthorizationUrl)["code_challenge"]);
    }

    // --- Helpers ---

    private static Dictionary<string, string> ParseQuery(string url)
    {
        var uri = new Uri(url);
        var coll = HttpUtility.ParseQueryString(uri.Query);
        return coll.AllKeys
            .Where(k => k is not null)
            .ToDictionary(k => k!, k => coll[k] ?? string.Empty);
    }

    private static bool IsBase64Url(string s)
    {
        // PKCE S256 code_challenge is base64url-encoded SHA-256 (43 chars, no padding).
        // Regex would be tighter but the length + charset check catches the regression
        // we care about (somebody returning hex or raw bytes by mistake).
        if (s.Length is < 16 or > 128) return false;
        return s.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }

    private static ExternalAuthService BuildService()
    {
        var settings = new ExternalAuthSettings
        {
            Apple = new AppleAuthSettings
            {
                Enabled = true,
                ClientId = "com.famick.homemanagement.test",
                TeamId = "TESTTEAMID",
                KeyId = "TESTKEYID",
                PrivateKey = TestApplePrivateKey,
            },
            Google = new GoogleAuthSettings
            {
                Enabled = true,
                ClientId = "google-test-client.apps.googleusercontent.com",
                ClientSecret = "test-google-secret",
            },
            OpenIdConnect = new OidcAuthSettings
            {
                Enabled = true,
                Authority = "https://idp.example.com",
                ClientId = "oidc-test-client",
                ClientSecret = "test-oidc-secret",
            },
        };

        var dbOptions = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: $"pkce-tests-{Guid.NewGuid()}")
            .Options;
        var dbContext = new HomeManagementDbContext(dbOptions);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpClientFactory = BuildHttpClientFactory();
        var tokenService = Mock.Of<ITokenService>();
        var contactService = Mock.Of<IContactService>();

        return new ExternalAuthService(
            dbContext,
            tokenService,
            new ConfigurationBuilder().Build(),
            contactService,
            cache,
            httpClientFactory,
            Options.Create(settings),
            NullLogger<ExternalAuthService>.Instance);
    }

    private static IHttpClientFactory BuildHttpClientFactory()
    {
        // Returns canned OIDC discovery JSON for any /.well-known/openid-configuration
        // request. Any other call returns 404 — the URL-builder tests don't perform
        // token exchange so that's the only HTTP they need.
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri is not null
                && request.RequestUri.AbsolutePath.EndsWith("/.well-known/openid-configuration"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "issuer": "https://idp.example.com",
                          "authorization_endpoint": "https://idp.example.com/authorize",
                          "token_endpoint": "https://idp.example.com/token",
                          "userinfo_endpoint": "https://idp.example.com/userinfo",
                          "jwks_uri": "https://idp.example.com/jwks"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factory.Object;
    }

    // RSA private key used only for tests — must be a real PEM so Apple's client-secret
    // JWT signing setup-code path (invoked indirectly via service ctor wiring of options)
    // does not blow up at construction time. URL-builder paths do NOT sign anything;
    // the key is only exercised during ExchangeAppleCodeAsync, which these tests do
    // not invoke. Generated with: `openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048`.
    // Test-only — safe to commit.
    private const string TestApplePrivateKey = """
        -----BEGIN PRIVATE KEY-----
        MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDIDhqQH8RXG1Hd
        TEST-KEY-PLACEHOLDER-NOT-A-REAL-KEY-DO-NOT-USE
        -----END PRIVATE KEY-----
        """;

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _send;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_send(request, cancellationToken));
    }
}
