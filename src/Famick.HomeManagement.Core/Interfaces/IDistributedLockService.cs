namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Distributed lock for preventing duplicate execution in multi-instance deployments.
/// Self-hosted uses a no-op implementation; cloud uses Redis.
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Attempts to acquire a distributed lock with the given key and expiry.
    /// Returns a disposable handle if the lock was acquired, or null if already held.
    /// </summary>
    Task<IAsyncDisposable?> TryAcquireLockAsync(
        string lockKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);
}
