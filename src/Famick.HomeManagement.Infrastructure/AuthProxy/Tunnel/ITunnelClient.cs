using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;

namespace Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel;

/// <summary>
/// Single-connection WebSocket client to an AuthProxy tunnel endpoint.
/// One instance = one connection attempt. <see cref="RunAsync"/>
/// returns when the connection closes or the cancellation token fires;
/// reconnect logic lives in the hosted service that owns the client.
/// </summary>
public interface ITunnelClient
{
    /// <summary>
    /// Opens the WebSocket, drives the handshake, then runs the
    /// send + receive loops until the socket closes, the token
    /// cancels, or a loop throws.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Enqueues an envelope for sending on the active connection.
    /// Throws <see cref="InvalidOperationException"/> if called
    /// before <see cref="RunAsync"/> has handshaken, or after the
    /// connection has closed. Used by the opt-in service to push
    /// USER_REGISTER / USER_UNREGISTER / USER_SYNC frames.
    /// </summary>
    Task SendAsync(TunnelEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>True while the connection is established and the loops are running.</summary>
    bool IsConnected { get; }
}
