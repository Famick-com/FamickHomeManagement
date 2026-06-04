using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;

namespace Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel;

/// <summary>
/// Singleton facade other services use to push frames over whatever
/// tunnel happens to be live. Used by Step 6's opt-in service to send
/// <c>USER_REGISTER</c> / <c>USER_UNREGISTER</c>. Returns false (no
/// throw, no queue) when there's no current connection — the next
/// successful (re)connect re-syncs the full state via <c>USER_SYNC</c>,
/// so a dropped event isn't catastrophic.
/// </summary>
public interface ITunnelSender
{
    Task<bool> TrySendAsync(TunnelEnvelope envelope, CancellationToken ct = default);
}
