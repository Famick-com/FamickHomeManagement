using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Tracks the minimum acceptable <c>iat</c> for a user's access tokens. Any access
/// token whose <c>iat</c> claim is earlier than <see cref="MinIat"/> is rejected by
/// <c>JwtMinIatMiddleware</c>.
///
/// Bumped (set to <c>now_seconds</c>) on:
/// <list type="bullet">
///   <item>Logout-all (the user's "sign me out everywhere" action)</item>
///   <item>Password change (just-issued tokens are issued with <c>iat = now_seconds + 1</c>
///         so they survive the bump)</item>
///   <item>Refresh-token reuse-detection (invalidates every other access token alongside
///         the family-poison)</item>
///   <item>Admin-triggered force sign-out</item>
/// </list>
///
/// Stored separately from the <c>User</c> row to keep the hot User row compact and to
/// allow the cloud-side Redis cache to hold only the small revocation timestamp rather
/// than the whole user record. "No row" cleanly defaults to <c>min_iat = 0</c>, meaning
/// no token is ever rejected on this check until something explicit bumps it.
/// </summary>
public class UserJwtMinIat : BaseEntity, ITenantEntity
{
    /// <summary>
    /// User this revocation timestamp applies to. Indexed unique — at most one row per user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Tenant this row belongs to. Same value as the user's <c>TenantId</c>; carried so
    /// the existing tenant query filter applies and cross-tenant reads of
    /// <c>jwt_min_iat</c> are blocked at the EF layer.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Earliest acceptable <c>iat</c> value (Unix seconds). Tokens with
    /// <c>iat &lt; MinIat</c> are rejected. Monotonically non-decreasing —
    /// <see cref="Famick.HomeManagement.Core.Interfaces.IJwtMinIatService.BumpAsync"/>
    /// ignores backwards moves.
    /// </summary>
    public long MinIat { get; set; }

    /// <summary>The user this row applies to.</summary>
    public User User { get; set; } = null!;
}
