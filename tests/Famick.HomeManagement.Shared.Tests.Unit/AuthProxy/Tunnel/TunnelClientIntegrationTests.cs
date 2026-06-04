using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel;
using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.AuthProxy.Tunnel;

/// <summary>
/// Spins up an in-process fake AuthProxy that exposes <c>/tunnel</c>,
/// then drives <see cref="TunnelClient"/> against it and asserts the
/// handshake completes, a ping round-trips, and an HTTP_REQUEST frame
/// hits the dispatcher and gets answered.
/// </summary>
public class TunnelClientIntegrationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private FakeAuthProxy _fakeProxy = null!;

    public async Task InitializeAsync()
    {
        _fakeProxy = await FakeAuthProxy.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _fakeProxy.DisposeAsync();
    }

    [Fact]
    public async Task RunAsync_completes_handshake_with_valid_signature()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        var homeServerId = Guid.NewGuid();

        var client = new TunnelClient(
            _fakeProxy.TunnelUri,
            homeServerId,
            pem,
            rsa,
            new StubDispatcher((_, _) => Task.FromResult(BuildOk(Guid.Empty))),
            NullLogger<TunnelClient>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = Task.Run(() => client.RunAsync(cts.Token));

        var connection = await _fakeProxy.WaitForConnectionAsync(TimeSpan.FromSeconds(5));
        connection.HomeServerId.Should().Be(homeServerId);
        connection.PresentedPem.Should().Be(pem);

        // Tear down so the test exits.
        cts.Cancel();
        await TaskSwallow(runTask);
    }

    [Fact]
    public async Task Ping_from_server_gets_Pong_with_matching_ts()
    {
        using var rsa = RSA.Create(2048);
        var client = new TunnelClient(
            _fakeProxy.TunnelUri,
            Guid.NewGuid(),
            rsa.ExportSubjectPublicKeyInfoPem(),
            rsa,
            new StubDispatcher((_, _) => Task.FromResult(BuildOk(Guid.Empty))),
            NullLogger<TunnelClient>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = Task.Run(() => client.RunAsync(cts.Token));

        var conn = await _fakeProxy.WaitForConnectionAsync(TimeSpan.FromSeconds(5));
        await conn.SendAsync(new Ping(424242));
        var pong = (await conn.ReadAsync()).Should().BeOfType<Pong>().Subject;
        pong.Ts.Should().Be(424242);

        cts.Cancel();
        await TaskSwallow(runTask);
    }

    [Fact]
    public async Task Server_HttpRequest_is_dispatched_and_response_returned()
    {
        using var rsa = RSA.Create(2048);

        var dispatcherCalls = new List<HttpRequestFrame>();
        var dispatcher = new StubDispatcher((req, _) =>
        {
            dispatcherCalls.Add(req);
            return Task.FromResult(new HttpResponseFrame(
                req.RequestId,
                Status: 201,
                Headers: new Dictionary<string, string[]> { ["X-Test"] = new[] { "yes" } },
                BodyBase64: Convert.ToBase64String("created"u8.ToArray())));
        });

        var client = new TunnelClient(
            _fakeProxy.TunnelUri,
            Guid.NewGuid(),
            rsa.ExportSubjectPublicKeyInfoPem(),
            rsa,
            dispatcher,
            NullLogger<TunnelClient>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = Task.Run(() => client.RunAsync(cts.Token));

        var conn = await _fakeProxy.WaitForConnectionAsync(TimeSpan.FromSeconds(5));

        var requestId = Guid.NewGuid();
        await conn.SendAsync(new HttpRequestFrame(
            requestId,
            Method: "POST",
            Path: "/api/v1/things",
            Headers: new Dictionary<string, string[]>
            {
                ["Authorization"] = new[] { "Bearer test-token" },
            },
            BodyBase64: Convert.ToBase64String("payload"u8.ToArray())));

        var response = (await conn.ReadAsync()).Should().BeOfType<HttpResponseFrame>().Subject;
        response.RequestId.Should().Be(requestId);
        response.Status.Should().Be(201);
        response.Headers.Should().ContainKey("X-Test");
        Convert.FromBase64String(response.BodyBase64!).Should().Equal("created"u8.ToArray());

        dispatcherCalls.Should().HaveCount(1);
        dispatcherCalls[0].Path.Should().Be("/api/v1/things");
        dispatcherCalls[0].Headers["Authorization"].Should().Equal("Bearer test-token");

        cts.Cancel();
        await TaskSwallow(runTask);
    }

    private static HttpResponseFrame BuildOk(Guid requestId) =>
        new(requestId, 200, new Dictionary<string, string[]>(), null);

    private static async Task TaskSwallow(Task t)
    {
        try { await t; }
        catch { }
    }

    // ---- inline test helpers ----

    private sealed class StubDispatcher : ITunnelRequestDispatcher
    {
        private readonly Func<HttpRequestFrame, CancellationToken, Task<HttpResponseFrame>> _impl;
        public StubDispatcher(Func<HttpRequestFrame, CancellationToken, Task<HttpResponseFrame>> impl) => _impl = impl;
        public Task<HttpResponseFrame> DispatchAsync(HttpRequestFrame request, CancellationToken ct) =>
            _impl(request, ct);
    }

    /// <summary>
    /// In-process Kestrel host exposing a minimal /tunnel endpoint that
    /// mimics the AuthProxy handshake (without signature verification —
    /// the AuthProxy-side test suite covers that).
    /// </summary>
    private sealed class FakeAuthProxy : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly TaskCompletionSource<FakeAuthProxyConnection> _connected =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Uri TunnelUri { get; }

        private FakeAuthProxy(WebApplication app, Uri tunnelUri)
        {
            _app = app;
            TunnelUri = tunnelUri;
        }

        public static async Task<FakeAuthProxy> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            app.UseWebSockets();

            var connected = new TaskCompletionSource<FakeAuthProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            app.Map("/tunnel", async (HttpContext context) =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    return;
                }
                var ws = await context.WebSockets.AcceptWebSocketAsync();

                var nonce = RandomNumberGenerator.GetBytes(32);
                await SendAsync(ws, new Challenge(Base64UrlEncoder.Encode(nonce)));

                var firstFrame = await ReadAsync(ws);
                if (firstFrame is not Handshake handshake)
                {
                    return;
                }

                await SendAsync(ws, new HandshakeOk());

                var conn = new FakeAuthProxyConnection(ws, handshake.HomeServerId, handshake.PublicKeyPem);
                connected.TrySetResult(conn);

                await conn.RunUntilCloseAsync(context.RequestAborted);
            });

            await app.StartAsync();
            var bound = app.Urls.First();
            var tunnelUri = new Uri(bound.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase) + "/tunnel");

            var fake = new FakeAuthProxy(app, tunnelUri);
            connected.Task.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully) fake._connected.TrySetResult(t.Result);
            }, TaskScheduler.Default);
            return fake;
        }

        public Task<FakeAuthProxyConnection> WaitForConnectionAsync(TimeSpan timeout)
        {
            return _connected.Task.WaitAsync(timeout);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private static async Task SendAsync(WebSocket ws, TunnelEnvelope env)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(env, JsonOptions);
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task<TunnelEnvelope?> ReadAsync(WebSocket ws)
        {
            var buffer = new byte[16 * 1024];
            using var ms = new MemoryStream();
            while (true)
            {
                var r = await ws.ReceiveAsync(buffer, CancellationToken.None);
                if (r.MessageType == WebSocketMessageType.Close) return null;
                ms.Write(buffer, 0, r.Count);
                if (r.EndOfMessage) return JsonSerializer.Deserialize<TunnelEnvelope>(ms.ToArray(), JsonOptions);
            }
        }
    }

    private sealed class FakeAuthProxyConnection
    {
        private readonly WebSocket _ws;
        private readonly System.Threading.Channels.Channel<TunnelEnvelope> _incoming =
            System.Threading.Channels.Channel.CreateUnbounded<TunnelEnvelope>();

        public Guid HomeServerId { get; }
        public string PresentedPem { get; }

        public FakeAuthProxyConnection(WebSocket ws, Guid homeServerId, string presentedPem)
        {
            _ws = ws;
            HomeServerId = homeServerId;
            PresentedPem = presentedPem;
        }

        public async Task SendAsync(TunnelEnvelope env)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(env, JsonOptions);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task<TunnelEnvelope> ReadAsync()
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await _incoming.Reader.ReadAsync(timeoutCts.Token);
        }

        public async Task RunUntilCloseAsync(CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult r;
                do
                {
                    try
                    {
                        r = await _ws.ReceiveAsync(buffer, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    if (r.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buffer, 0, r.Count);
                } while (!r.EndOfMessage);

                var env = JsonSerializer.Deserialize<TunnelEnvelope>(ms.ToArray(), JsonOptions);
                if (env is not null) await _incoming.Writer.WriteAsync(env, ct);
            }
        }
    }
}
