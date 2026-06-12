using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.AuthProxy;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Tests for the home-server-side pairing service. Uses InMemory EF +
/// a mocked HttpMessageHandler to fake the AuthProxy responses.
/// </summary>
public class AuthProxyPairingServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _db;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public AuthProxyPairingServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new HomeManagementDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CompletePairingAsync_happy_path_persists_config_and_posts_expected_payload()
    {
        var capturedRequest = new CapturedRequest();
        var sut = BuildSut(new FakeHttpHandler(req =>
        {
            capturedRequest.Capture(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    homeServerId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    url = "https://home.example.com",
                    displayName = "Therien Family",
                    adminEmail = "mike@therienfamily.net",
                    pairedAt = DateTimeOffset.Parse("2026-06-03T12:00:00Z"),
                }),
            };
        }));

        var result = await sut.CompletePairingAsync(
            new CompletePairingRequest
            {
                Token = "the-token",
                DisplayName = "Therien Family",
                PublicUrl = "https://home.example.com",
            },
            "caller@example.com",
            "https://home.example.com",
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Config.Should().NotBeNull();
        result.Config!.AuthProxyHomeServerId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.Config.DisplayName.Should().Be("Therien Family");
        result.Config.PairedAdminEmail.Should().Be("mike@therienfamily.net",
            "the home server records the email AuthProxy returns (from the token), not the caller's");
        result.Config.AuthProxyBaseUrl.Should().Be("https://famick-auth.up.railway.app");

        var stored = await _db.AuthProxyPairingConfigs.SingleAsync();
        stored.AuthProxyHomeServerId.Should().Be(result.Config.AuthProxyHomeServerId);

        // Verify the wire shape AuthProxy received
        capturedRequest.Path.Should().Be("/pairing/complete");
        capturedRequest.Body.Should().Contain("\"token\":\"the-token\"");
        capturedRequest.Body.Should().Contain("\"displayName\":\"Therien Family\"");
        capturedRequest.Body.Should().Contain("\"url\":\"https://home.example.com\"");
        capturedRequest.Body.Should().Contain("\"publicKeyPem\":", "PEM must be sent on the wire");
        capturedRequest.Body.Should().Contain("\"publicKeyFingerprint\":", "fingerprint must be sent on the wire");
    }

    [Fact]
    public async Task CompletePairingAsync_uses_request_host_url_when_publicUrl_is_blank()
    {
        var capturedRequest = new CapturedRequest();
        var sut = BuildSut(new FakeHttpHandler(req =>
        {
            capturedRequest.Capture(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    homeServerId = Guid.NewGuid(),
                    url = "http://192.168.1.10",
                    displayName = "X",
                    adminEmail = "a@b.c",
                    pairedAt = DateTimeOffset.UtcNow,
                }),
            };
        }));

        await sut.CompletePairingAsync(
            new CompletePairingRequest { Token = "t", DisplayName = "X", PublicUrl = null },
            "a@b.c",
            "http://192.168.1.10",  // fallback host URL
            CancellationToken.None);

        capturedRequest.Body.Should().Contain("\"url\":\"http://192.168.1.10\"");
    }

    [Fact]
    public async Task CompletePairingAsync_refuses_when_already_paired_without_calling_authproxy()
    {
        _db.AuthProxyPairingConfigs.Add(new Domain.Entities.AuthProxyPairingConfig
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuthProxyHomeServerId = Guid.NewGuid(),
            AuthProxyBaseUrl = "https://famick-auth.up.railway.app",
            PairedAdminEmail = "existing@example.com",
            DisplayName = "Already Paired",
            PairedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var called = false;
        var sut = BuildSut(new FakeHttpHandler(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        var result = await sut.CompletePairingAsync(
            new CompletePairingRequest { Token = "t", DisplayName = "Y", PublicUrl = "https://h.example.com" },
            "caller@example.com",
            "https://h.example.com",
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthProxyPairingErrorCodes.AlreadyPaired);
        called.Should().BeFalse("must not call AuthProxy when local config already exists");
    }

    [Theory]
    [InlineData(AuthProxyPairingErrorCodes.TokenInvalid)]
    [InlineData(AuthProxyPairingErrorCodes.TokenExpired)]
    [InlineData(AuthProxyPairingErrorCodes.TokenAlreadyConsumed)]
    [InlineData(AuthProxyPairingErrorCodes.PublicKeyInvalid)]
    public async Task CompletePairingAsync_propagates_authproxy_error_code_from_400_response(string errorCode)
    {
        var sut = BuildSut(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { errorCode, error = "AuthProxy said no." }),
        }));

        var result = await sut.CompletePairingAsync(
            new CompletePairingRequest { Token = "t", DisplayName = "Y", PublicUrl = "https://h.example.com" },
            "caller@example.com",
            "https://h.example.com",
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(errorCode);
        result.ErrorMessage.Should().Be("AuthProxy said no.");

        (await _db.AuthProxyPairingConfigs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CompletePairingAsync_propagates_url_already_paired_from_409()
    {
        var sut = BuildSut(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new
            {
                errorCode = AuthProxyPairingErrorCodes.UrlAlreadyPaired,
                error = "A home server with this URL is already paired.",
            }),
        }));

        var result = await sut.CompletePairingAsync(
            new CompletePairingRequest { Token = "t", DisplayName = "Y", PublicUrl = "https://h.example.com" },
            "caller@example.com",
            "https://h.example.com",
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthProxyPairingErrorCodes.UrlAlreadyPaired);
    }

    [Fact]
    public async Task CompletePairingAsync_returns_NetworkError_when_http_call_throws()
    {
        var sut = BuildSut(new FakeHttpHandler(_ => throw new HttpRequestException("dns failed")));

        var result = await sut.CompletePairingAsync(
            new CompletePairingRequest { Token = "t", DisplayName = "Y", PublicUrl = "https://h.example.com" },
            "caller@example.com",
            "https://h.example.com",
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthProxyPairingErrorCodes.NetworkError);
    }

    [Fact]
    public async Task UnpairAsync_removes_local_config_without_calling_authproxy()
    {
        _db.AuthProxyPairingConfigs.Add(new Domain.Entities.AuthProxyPairingConfig
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuthProxyHomeServerId = Guid.NewGuid(),
            AuthProxyBaseUrl = "https://famick-auth.up.railway.app",
            PairedAdminEmail = "a@b.c",
            DisplayName = "X",
            PairedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var called = false;
        var sut = BuildSut(new FakeHttpHandler(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await sut.UnpairAsync(CancellationToken.None);

        (await _db.AuthProxyPairingConfigs.CountAsync()).Should().Be(0);
        called.Should().BeFalse("unpair is local-only in MVP");
    }

    [Fact]
    public async Task GetCurrentAsync_returns_null_when_not_paired()
    {
        var sut = BuildSut(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await sut.GetCurrentAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    // --- helpers ---

    private AuthProxyPairingService BuildSut(FakeHttpHandler handler)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(_tenantId);

        var signingKey = new JwtSigningKeyService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            NullLogger<JwtSigningKeyService>.Instance);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://famick-auth.up.railway.app"),
        };
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory.Setup(f => f.CreateClient(AuthProxyPairingService.HttpClientName)).Returns(httpClient);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthProxy:BaseUrl"] = "https://famick-auth.up.railway.app",
            })
            .Build();

        return new AuthProxyPairingService(
            _db,
            tenantProvider.Object,
            signingKey,
            clientFactory.Object,
            new MemoryCache(new MemoryCacheOptions()),
            configuration,
            NullLogger<AuthProxyPairingService>.Instance);
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Materialize the body so tests can capture it before we call the responder.
            if (request.Content is not null)
            {
                await request.Content.LoadIntoBufferAsync();
            }
            return _respond(request);
        }
    }

    private sealed class CapturedRequest
    {
        public string Path { get; private set; } = "";
        public string Body { get; private set; } = "";

        public void Capture(HttpRequestMessage req)
        {
            Path = req.RequestUri?.AbsolutePath ?? "";
            Body = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
        }
    }
}
