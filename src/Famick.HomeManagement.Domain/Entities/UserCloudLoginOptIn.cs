namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Marks that a user has opted in to signing in via auth.famick.com
/// (the paid cloud relay). Presence of the row = opted in; absence =
/// opted out. The home server pushes <c>USER_REGISTER</c> to AuthProxy
/// on insert and <c>USER_UNREGISTER</c> on delete; on every (re)connect
/// it sends a full <c>USER_SYNC</c> with all opted-in emails so the
/// AuthProxy registry converges with local state.
/// </summary>
public class UserCloudLoginOptIn : BaseTenantEntity
{
    /// <summary>The user this opt-in belongs to. Unique-indexed.</summary>
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTime OptedInAt { get; set; }
}
