namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Records that this home server is paired with an AuthProxy
/// (auth.famick.com) instance. At most one row per tenant — the row
/// exists when paired, is deleted when the admin unpairs.
///
/// AuthProxy hosts the federated-identity registry + (Phase 8) the
/// WebSocket tunnel that lets mobile apps reach this home server
/// when it has no directly-reachable URL. See the auth-proxy repo
/// for the AuthProxy-side <c>HomeServer</c> entity.
/// </summary>
public class AuthProxyPairingConfig : BaseTenantEntity
{
    /// <summary>
    /// The Guid AuthProxy assigned to THIS home server at pairing
    /// time. Used in the WebSocket tunnel handshake (Step 5) to
    /// identify which paired server is connecting.
    /// </summary>
    public Guid AuthProxyHomeServerId { get; set; }

    /// <summary>
    /// Base URL of the AuthProxy instance this home server paired with.
    /// E.g. <c>https://famick-auth.up.railway.app</c>. Different paired
    /// home servers in theory could pair with different AuthProxy
    /// deployments (dev vs prod) — storing it per row keeps that
    /// flexibility instead of hardcoding via config.
    /// </summary>
    public string AuthProxyBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Admin email this home server presented at <c>/pairing/start</c>.
    /// Audit trail — the human authorizing the pairing.
    /// </summary>
    public string PairedAdminEmail { get; set; } = string.Empty;

    /// <summary>
    /// Display name surfaced in the AuthProxy admin UI for this server.
    /// Admin types it in the pairing form.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// When the pairing was completed (server time on this home server).
    /// </summary>
    public DateTime PairedAt { get; set; }
}
