namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Per-user opt-in flag for cloud login via auth.famick.com. Backed by
/// the <c>user_cloud_login_optins</c> table; presence of a row =
/// opted in. Opt-in / opt-out push a <c>USER_REGISTER</c> /
/// <c>USER_UNREGISTER</c> over the tunnel via <c>ITunnelSender</c>;
/// (re)connect pulls the full list and sends <c>USER_SYNC</c> so the
/// AuthProxy registry converges.
/// </summary>
public interface ICloudLoginOptInService
{
    Task<bool> IsOptedInAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Inserts the opt-in row if it doesn't exist and pushes a
    /// USER_REGISTER over the tunnel. Idempotent — returns the user's
    /// email regardless of whether a row was created.
    /// </summary>
    Task OptInAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Removes the opt-in row if it exists and pushes a USER_UNREGISTER.
    /// Idempotent.
    /// </summary>
    Task OptOutAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Returns the full set of opted-in emails for this tenant —
    /// used by the tunnel client's reconnect hook to send <c>USER_SYNC</c>.
    /// </summary>
    Task<IReadOnlyList<string>> GetOptedInEmailsAsync(CancellationToken ct);
}
