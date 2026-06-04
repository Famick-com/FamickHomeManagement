namespace Famick.HomeManagement.Core.DTOs.AuthProxy;

/// <summary>
/// Sent by the admin UI to <c>POST /api/auth-proxy/pairing/complete</c>
/// when the admin pastes the token they obtained from auth.famick.com.
/// </summary>
public sealed class CompletePairingRequest
{
    /// <summary>
    /// The opaque token surfaced by <c>POST /pairing/start</c> on
    /// auth.famick.com. Single-use, 15-minute TTL.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for this home server as it'll appear in the
    /// AuthProxy admin UI. e.g. "Therien Family Home".
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional: the public URL the admin wants AuthProxy to record for
    /// this home server. For proxied-mode-only deployments (no public
    /// URL), this is informational — AuthProxy uses
    /// <c>/h/{homeServerId}/</c> as the routing prefix, not this URL.
    /// Defaults to the request's <c>Host</c> header on the server side
    /// if blank.
    /// </summary>
    public string? PublicUrl { get; set; }
}

/// <summary>
/// Returned by both <c>GET status</c> and <c>POST complete</c>. The
/// <see cref="IsPaired"/> flag drives the UI's paired-vs-unpaired
/// branch — when false the remaining fields are defaulted. Always
/// returned as a 200 with a parseable body (no 204 empty responses).
/// </summary>
public sealed class PairingStatusResponse
{
    public bool IsPaired { get; set; }
    public Guid AuthProxyHomeServerId { get; set; }
    public string AuthProxyBaseUrl { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PairedAdminEmail { get; set; } = string.Empty;
    public DateTime PairedAt { get; set; }
}

/// <summary>
/// Returned by <c>POST /api/auth-proxy/pairing/complete</c> on a 4xx
/// outcome so the UI can render a focused error message.
/// </summary>
public sealed class PairingErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Mirror of the AuthProxy-side error codes (kept in lock-step). Only
/// the codes the admin UI handles explicitly are constants here; any
/// other code falls through to a generic error display.
/// </summary>
public static class AuthProxyPairingErrorCodes
{
    public const string MalformedInput = "malformed_input";
    public const string TokenInvalid = "token_invalid";
    public const string TokenExpired = "token_expired";
    public const string TokenAlreadyConsumed = "token_already_consumed";
    public const string UrlAlreadyPaired = "url_already_paired";
    public const string PublicKeyInvalid = "pairing_publickey_invalid";

    /// <summary>Returned when the home server can't reach AuthProxy at all (network failure).</summary>
    public const string NetworkError = "auth_proxy_network_error";

    /// <summary>Returned when the home server is already paired (admin should unpair first).</summary>
    public const string AlreadyPaired = "already_paired";
}
