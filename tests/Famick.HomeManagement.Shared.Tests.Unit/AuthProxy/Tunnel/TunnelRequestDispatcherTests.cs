using System.Net;
using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel;
using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.AuthProxy.Tunnel;

/// <summary>
/// Verifies the dispatcher translates a tunnel HTTP_REQUEST envelope
/// into a real loopback HTTP call and folds the response back into an
/// HTTP_RESPONSE envelope. Uses a captured HttpMessageHandler so we
/// don't need a real listening server.
/// </summary>
public class TunnelRequestDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_calls_configured_loopback_url_with_request_method_and_body()
    {
        var captured = new CapturedRequest();
        var sut = BuildSut(captured, respondWith: () =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("ok body"u8.ToArray()),
            });

        var requestId = Guid.NewGuid();
        var envelope = new HttpRequestFrame(
            requestId,
            Method: "POST",
            Path: "/api/v1/profile",
            Headers: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = new[] { "Bearer xyz" },
                ["Accept"] = new[] { "application/json" },
            },
            BodyBase64: Convert.ToBase64String("hello"u8.ToArray()));

        var response = await sut.DispatchAsync(envelope, CancellationToken.None);

        response.RequestId.Should().Be(requestId);
        response.Status.Should().Be(200);
        Convert.FromBase64String(response.BodyBase64!).Should().Equal("ok body"u8.ToArray());

        captured.Method.Should().Be(HttpMethod.Post);
        captured.Uri!.AbsoluteUri.Should().Be("http://localhost:5003/api/v1/profile");
        captured.HeaderAuthorization.Should().Be("Bearer xyz");
        captured.BodyBytes.Should().Equal("hello"u8.ToArray());
    }

    [Fact]
    public async Task DispatchAsync_omits_hop_by_hop_headers_on_both_sides()
    {
        var captured = new CapturedRequest();
        var sut = BuildSut(captured, respondWith: () =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>()),
            };
            resp.Headers.Add("X-Useful", "yes");
            resp.Headers.ConnectionClose = true;  // Connection: close — must be dropped
            return resp;
        });

        var envelope = new HttpRequestFrame(
            Guid.NewGuid(),
            "GET",
            "/api/health",
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = new[] { "famick-auth.up.railway.app" },     // hop-by-hop, drop
                ["Connection"] = new[] { "Upgrade" },                  // hop-by-hop, drop
                ["X-Famick"] = new[] { "keep" },
            },
            BodyBase64: null);

        var response = await sut.DispatchAsync(envelope, CancellationToken.None);

        response.Headers.Should().ContainKey("X-Useful");
        response.Headers.Should().NotContainKey("Connection",
            "Connection is hop-by-hop and must not be mirrored back");

        captured.HasHeader("Host").Should().BeFalse("Host is hop-by-hop and must be dropped before loopback");
        captured.HasHeader("Connection").Should().BeFalse("Connection is hop-by-hop");
        captured.HasHeader("X-Famick").Should().BeTrue("non-hop-by-hop headers must pass through");
    }

    [Fact]
    public async Task DispatchAsync_returns_502_envelope_on_loopback_failure()
    {
        var captured = new CapturedRequest();
        var sut = BuildSut(captured, respondWith: () => throw new HttpRequestException("loopback unreachable"));

        var envelope = new HttpRequestFrame(
            Guid.NewGuid(),
            "GET",
            "/api/health",
            new Dictionary<string, string[]>(),
            BodyBase64: null);

        var response = await sut.DispatchAsync(envelope, CancellationToken.None);

        response.Status.Should().Be(502, "network failures during loopback dispatch surface as Bad Gateway");
        response.RequestId.Should().Be(envelope.RequestId);
    }

    [Fact]
    public async Task DispatchAsync_prefers_explicit_LoopbackBaseUrl_over_default()
    {
        var captured = new CapturedRequest();
        var sut = BuildSut(captured, respondWith: () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>()),
        },
        configOverrides: new Dictionary<string, string?>
        {
            ["AuthProxy:LoopbackBaseUrl"] = "https://overridden.local:9000",
        });

        await sut.DispatchAsync(
            new HttpRequestFrame(Guid.NewGuid(), "GET", "/api/health",
                new Dictionary<string, string[]>(), null),
            CancellationToken.None);

        captured.Uri!.AbsoluteUri.Should().Be("https://overridden.local:9000/api/health");
    }

    // ---- helpers ----

    private static TunnelRequestDispatcher BuildSut(
        CapturedRequest captured,
        Func<HttpResponseMessage> respondWith,
        IDictionary<string, string?>? configOverrides = null)
    {
        var handler = new FakeHttpHandler((req, ct) =>
        {
            captured.Capture(req).GetAwaiter().GetResult();
            return respondWith();
        });
        var httpClient = new HttpClient(handler);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(TunnelRequestDispatcher.HttpClientName)).Returns(httpClient);

        var configValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (configOverrides is not null)
        {
            foreach (var (k, v) in configOverrides) configValues[k] = v;
        }
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return new TunnelRequestDispatcher(
            factory.Object,
            configuration,
            NullLogger<TunnelRequestDispatcher>.Instance);
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;
        public FakeHttpHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request, cancellationToken));
    }

    private sealed class CapturedRequest
    {
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public byte[] BodyBytes { get; private set; } = Array.Empty<byte>();
        public string? HeaderAuthorization { get; private set; }
        private readonly HashSet<string> _headerNames = new(StringComparer.OrdinalIgnoreCase);

        public async Task<CapturedRequest> Capture(HttpRequestMessage req)
        {
            Method = req.Method;
            Uri = req.RequestUri;
            foreach (var (name, _) in req.Headers) _headerNames.Add(name);
            if (req.Content is not null)
            {
                foreach (var (name, _) in req.Content.Headers) _headerNames.Add(name);
                BodyBytes = await req.Content.ReadAsByteArrayAsync();
            }
            HeaderAuthorization = req.Headers.Authorization?.ToString();
            return this;
        }

        public bool HasHeader(string name) => _headerNames.Contains(name);
    }
}
