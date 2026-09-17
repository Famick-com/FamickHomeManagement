namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Advisory lock keyed on a user or a tenant. Wrap a critical section so
/// concurrent operations on the same subject serialize correctly — the
/// password-change and refresh-token rotation paths take the per-user lock,
/// and the seat-limit check takes the per-tenant one.
///
/// The two keyspaces are disjoint: a user lock never blocks a tenant lock.
///
/// Two implementations:
/// <list type="bullet">
///   <item><c>PostgresUserAdvisoryLockService</c> — uses <c>pg_try_advisory_lock(int8)</c>
///         keyed on a 64-bit hash of the user id. Suitable for self-hosted (no Redis).</item>
///   <item><c>RedisUserAdvisoryLockService</c> — wraps the existing
///         <c>RedisDistributedLockService</c> for cross-instance coordination on the cloud.</item>
/// </list>
///
/// Acquisition blocks (with backoff) until either the lock is held or the timeout
/// elapses; on timeout the implementation throws <see cref="LockAcquisitionTimeoutException"/>
/// rather than returning null. Disposal releases the lock.
/// </summary>
public interface IUserAdvisoryLockService
{
    /// <summary>
    /// Takes the lock for a single user.
    /// </summary>
    Task<IAsyncDisposable> AcquireAsync(
        Guid userId,
        TimeSpan timeout,
        CancellationToken ct = default);

    /// <summary>
    /// Takes the lock for a whole tenant, for critical sections that read and
    /// then write a household-wide total — the seat limit being the first.
    /// </summary>
    Task<IAsyncDisposable> AcquireTenantLockAsync(
        Guid tenantId,
        TimeSpan timeout,
        CancellationToken ct = default);
}

/// <summary>
/// Thrown when a per-user advisory lock cannot be acquired within the requested timeout.
/// </summary>
public class LockAcquisitionTimeoutException : Exception
{
    public LockAcquisitionTimeoutException(Guid userId, TimeSpan timeout)
        : base($"Could not acquire advisory lock for {userId} within {timeout}.")
    {
        UserId = userId;
        Timeout = timeout;
    }

    /// <summary>The subject the lock was keyed on — a user id, or a tenant id
    /// when thrown from <see cref="IUserAdvisoryLockService.AcquireTenantLockAsync"/>.</summary>
    public Guid UserId { get; }

    public TimeSpan Timeout { get; }
}
