using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel;

/// <summary>
/// One-connection-at-a-time WebSocket client. The outer
/// <see cref="TunnelHostedService"/> owns the reconnect loop;
/// <see cref="RunAsync"/> here just runs one connection from
/// handshake-success to disconnect.
/// </summary>
public sealed class TunnelClient : ITunnelClient
{
    private const int ReceiveBufferSize = 16 * 1024;
    private const int MaxFrameBytes = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Uri _tunnelUrl;
    private readonly Guid _homeServerId;
    private readonly string _publicKeyPem;
    private readonly RSA _rsa;
    private readonly ITunnelRequestDispatcher _dispatcher;
    private readonly ILogger<TunnelClient> _logger;
    private readonly string _agentVersion;
    private readonly Func<ITunnelClient, CancellationToken, Task>? _onConnected;

    private readonly Channel<TunnelEnvelope> _outbound = Channel.CreateUnbounded<TunnelEnvelope>(
        new UnboundedChannelOptions { SingleReader = true });

    private ClientWebSocket? _socket;
    private volatile bool _isConnected;

    public bool IsConnected => _isConnected;

    public TunnelClient(
        Uri tunnelUrl,
        Guid homeServerId,
        string publicKeyPem,
        RSA rsa,
        ITunnelRequestDispatcher dispatcher,
        ILogger<TunnelClient> logger,
        string? agentVersion = null,
        Func<ITunnelClient, CancellationToken, Task>? onConnected = null)
    {
        _tunnelUrl = tunnelUrl;
        _homeServerId = homeServerId;
        _publicKeyPem = publicKeyPem;
        _rsa = rsa;
        _dispatcher = dispatcher;
        _logger = logger;
        _agentVersion = agentVersion ?? "famick-home-server/1.0";
        _onConnected = onConnected;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        _socket = socket;

        await socket.ConnectAsync(_tunnelUrl, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Tunnel WebSocket connected to {Url}; awaiting challenge",
            _tunnelUrl);

        // Handshake: read challenge → sign nonce → send handshake → expect handshake_ok.
        var challengeEnv = await ReadFrameAsync(socket, cancellationToken).ConfigureAwait(false);
        if (challengeEnv is not Challenge challenge)
        {
            throw new InvalidOperationException(
                $"Expected challenge frame, got {challengeEnv?.GetType().Name ?? "(closed)"}");
        }

        var nonceBytes = Base64UrlEncoder.DecodeBytes(challenge.NonceBase64Url);
        var signature = _rsa.SignData(nonceBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var handshake = new Handshake(
            _homeServerId,
            _publicKeyPem,
            Base64UrlEncoder.Encode(signature),
            _agentVersion);
        await WriteFrameAsync(socket, handshake, cancellationToken).ConfigureAwait(false);

        var ackEnv = await ReadFrameAsync(socket, cancellationToken).ConfigureAwait(false);
        if (ackEnv is HandshakeFail fail)
        {
            throw new InvalidOperationException($"Tunnel handshake refused: {fail.Reason}");
        }
        if (ackEnv is not HandshakeOk)
        {
            throw new InvalidOperationException(
                $"Expected handshake_ok, got {ackEnv?.GetType().Name ?? "(closed)"}");
        }

        _logger.LogInformation(
            "Tunnel handshake complete for home_server_id={HomeServerId}",
            _homeServerId);
        _isConnected = true;

        // Run send + receive concurrently. Either returning means the
        // connection is over.
        try
        {
            var sendTask = SendLoopAsync(socket, cancellationToken);
            var receiveTask = ReceiveLoopAsync(socket, cancellationToken);

            // Fire-and-forget the post-handshake hook (Step 6's USER_SYNC).
            // Errors don't take the tunnel down — they're logged.
            if (_onConnected is not null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _onConnected(this, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { /* shutdown */ }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Tunnel post-handshake hook threw");
                    }
                }, cancellationToken);
            }

            await Task.WhenAny(sendTask, receiveTask).ConfigureAwait(false);

            // Cancel siblings + drain.
            _outbound.Writer.TryComplete();
            try
            {
                await Task.WhenAll(sendTask, receiveTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tunnel loop terminated with exception");
            }
        }
        finally
        {
            _isConnected = false;
            _socket = null;
        }
    }

    public async Task SendAsync(TunnelEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException(
                "Tunnel is not connected; cannot send. Wait for IsConnected==true or retry after reconnect.");
        }
        await _outbound.Writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendLoopAsync(WebSocket socket, CancellationToken ct)
    {
        try
        {
            await foreach (var envelope in _outbound.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await WriteFrameAsync(socket, envelope, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* expected */ }
    }

    private async Task ReceiveLoopAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[ReceiveBufferSize];
        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var frame = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    if (frame.Length + result.Count > MaxFrameBytes)
                    {
                        _logger.LogWarning("Tunnel frame exceeded MaxFrameBytes; aborting");
                        return;
                    }
                    frame.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                TunnelEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<TunnelEnvelope>(frame.ToArray(), JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Tunnel received malformed JSON; skipping frame");
                    continue;
                }
                if (envelope is null) continue;

                await HandleAsync(envelope, ct).ConfigureAwait(false);
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogInformation("Tunnel WebSocket closed prematurely; reconnect logic will retry");
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private async Task HandleAsync(TunnelEnvelope envelope, CancellationToken ct)
    {
        switch (envelope)
        {
            case HttpRequestFrame req:
            {
                HttpResponseFrame response;
                try
                {
                    response = await _dispatcher.DispatchAsync(req, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Tunnel dispatcher threw for request_id={RequestId}; returning 500",
                        req.RequestId);
                    response = new HttpResponseFrame(req.RequestId, 500, new(), null);
                }
                await _outbound.Writer.WriteAsync(response, ct).ConfigureAwait(false);
                break;
            }

            case Ping ping:
                await _outbound.Writer.WriteAsync(new Pong(ping.Ts), ct).ConfigureAwait(false);
                break;

            case HandshakeFail fail:
                _logger.LogWarning(
                    "Tunnel received unexpected handshake_fail post-handshake: {Reason}",
                    fail.Reason);
                break;

            default:
                _logger.LogDebug(
                    "Tunnel received unhandled envelope {Type}; ignoring",
                    envelope.GetType().Name);
                break;
        }
    }

    private static async Task WriteFrameAsync(WebSocket socket, TunnelEnvelope envelope, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ct)
            .ConfigureAwait(false);
    }

    private static async Task<TunnelEnvelope?> ReadFrameAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var ms = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            if (ms.Length + result.Count > MaxFrameBytes)
            {
                throw new InvalidOperationException("Frame exceeded max size during handshake read.");
            }
            ms.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        return JsonSerializer.Deserialize<TunnelEnvelope>(ms.ToArray(), JsonOptions);
    }
}
