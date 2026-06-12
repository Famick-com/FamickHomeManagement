using Famick.HomeManagement.Core.DTOs.AuthProxy;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Server-side service for pairing THIS home server with an AuthProxy
/// instance (auth.famick.com). Lives on the home server, talks to
/// AuthProxy via HTTP.
/// </summary>
public interface IAuthProxyPairingService
{
    /// <summary>
    /// Returns the current pairing record for the active tenant, or
    /// null when this home server isn't paired.
    /// </summary>
    Task<AuthProxyPairingConfig?> GetCurrentAsync(CancellationToken ct);

    /// <summary>
    /// Calls AuthProxy's <c>/pairing/complete</c> with the supplied
    /// token, the home server's public URL (from <paramref name="request"/>
    /// or current request host), display name, public-key PEM (from the
    /// JWT signing key), and the matching fingerprint. Persists an
    /// <see cref="AuthProxyPairingConfig"/> on success.
    /// </summary>
    Task<AuthProxyPairingResult> CompletePairingAsync(
        CompletePairingRequest request,
        string callerAdminEmail,
        string requestHostUrl,
        CancellationToken ct);

    /// <summary>
    /// Drops the local pairing config. Does NOT call AuthProxy — there's
    /// no AuthProxy-side unpair endpoint in MVP; admin must clean up
    /// the orphan HomeServer row out-of-band if they want it gone from
    /// AuthProxy. The local row removal stops the home server from
    /// participating in the tunnel handshake on next restart.
    /// </summary>
    Task UnpairAsync(CancellationToken ct);

    /// <summary>
    /// Fetches the subscription/trial state for this home server from
    /// AuthProxy's <c>GET /pairing/status/{homeServerId}</c>. Cached in
    /// memory for 5 minutes (matching the upstream <c>Cache-Control</c>
    /// header). Returns null on network/parse failure so the caller can
    /// degrade gracefully — never throws.
    /// </summary>
    Task<AuthProxyBillingStatus?> GetBillingStatusAsync(Guid homeServerId, CancellationToken ct);
}

/// <summary>
/// Outcome of <see cref="IAuthProxyPairingService.CompletePairingAsync"/>.
/// </summary>
public sealed class AuthProxyPairingResult
{
    public bool IsSuccess { get; init; }
    public AuthProxyPairingConfig? Config { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static AuthProxyPairingResult Success(AuthProxyPairingConfig config) =>
        new() { IsSuccess = true, Config = config };

    public static AuthProxyPairingResult Failure(string errorCode, string message) =>
        new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = message };
}
