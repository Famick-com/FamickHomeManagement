namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Per-user JWT revocation timestamp ("minimum acceptable issued-at").
///
/// The middleware <c>JwtMinIatMiddleware</c> reads this on every authenticated request
/// and rejects any access token whose <c>iat</c> claim is earlier than the user's
/// <see cref="GetMinIatAsync"/> value with <c>401</c>. <see cref="BumpAsync"/> is
/// monotonically non-decreasing — calls with smaller values are no-ops.
///
/// Two implementations:
/// <list type="bullet">
///   <item><c>JwtMinIatService</c> — Postgres-only, suitable for self-hosted.</item>
///   <item><c>RedisCachedJwtMinIatService</c> — wraps the Postgres impl with a 5-minute
///         Redis cache keyed on <c>jwt-min-iat:{userId}</c>. Cache invalidated on bump.</item>
/// </list>
/// </summary>
public interface IJwtMinIatService
{
    /// <summary>
    /// Returns the user's current <c>min_iat</c> in Unix seconds. Returns 0 if no row
    /// exists for the user (default — no token has ever been revoked).
    /// </summary>
    Task<long> GetMinIatAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Sets the user's <c>min_iat</c> to <paramref name="newMinIat"/>, but only if the
    /// new value is greater than the current value. Backwards moves are silently ignored.
    /// Cloud implementations also invalidate the Redis cache key on a successful bump.
    /// </summary>
    Task BumpAsync(Guid userId, long newMinIat, CancellationToken ct = default);
}
