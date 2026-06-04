using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;

namespace Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel;

/// <summary>
/// Translates an incoming <see cref="HttpRequestFrame"/> into a
/// loopback HTTP call against this home server and builds the
/// matching <see cref="HttpResponseFrame"/>. Stateless, scoped only
/// so it can pick up a fresh <c>HttpClient</c> from the factory.
/// </summary>
public interface ITunnelRequestDispatcher
{
    Task<HttpResponseFrame> DispatchAsync(HttpRequestFrame request, CancellationToken ct);
}
