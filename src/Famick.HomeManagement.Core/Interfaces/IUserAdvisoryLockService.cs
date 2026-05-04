namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Per-user advisory lock. Wrap the password-change and refresh-token rotation
/// critical sections so concurrent operations on the same user serialize correctly.
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
    Task<IAsyncDisposable> AcquireAsync(
        Guid userId,
        TimeSpan timeout,
        CancellationToken ct = default);
}

/// <summary>
/// Thrown when a per-user advisory lock cannot be acquired within the requested timeout.
/// </summary>
public class LockAcquisitionTimeoutException : Exception
{
    public LockAcquisitionTimeoutException(Guid userId, TimeSpan timeout)
        : base($"Could not acquire user advisory lock for {userId} within {timeout}.")
    {
        UserId = userId;
        Timeout = timeout;
    }

    public Guid UserId { get; }
    public TimeSpan Timeout { get; }
}
