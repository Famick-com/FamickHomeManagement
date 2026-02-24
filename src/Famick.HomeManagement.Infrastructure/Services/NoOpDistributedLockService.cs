using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Always-succeeds distributed lock for single-instance deployments (self-hosted).
/// Cloud deployments override this with a Redis-based implementation.
/// </summary>
public class NoOpDistributedLockService : IDistributedLockService
{
    public Task<IAsyncDisposable?> TryAcquireLockAsync(
        string lockKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IAsyncDisposable?>(new NoOpLockHandle());
    }

    private sealed class NoOpLockHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
