using Famick.HomeManagement.Core.DTOs.Authentication;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Issues a normal login session (access + refresh JWT) for a user who has
/// already been authenticated out-of-band — specifically the HA Ingress flow,
/// where <see cref="IHaIngressUserResolver"/> has resolved the trusted
/// <c>X-Remote-User-*</c> headers to a local user. There is no password or
/// other credential check here; the caller is responsible for proving the
/// identity before calling (the HA Ingress auth scheme + trusted-proxy gate).
/// </summary>
public interface IHaIngressSessionIssuer
{
    /// <summary>
    /// Mints the same <see cref="LoginResponse"/> a password login would for the
    /// given user id (access token, refresh token, user + tenant info). Throws
    /// if the user does not exist or is inactive.
    /// </summary>
    Task<LoginResponse> IssueSessionAsync(
        Guid userId,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default);
}
