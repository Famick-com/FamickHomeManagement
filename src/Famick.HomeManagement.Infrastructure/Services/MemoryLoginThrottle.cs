using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// In-process <see cref="ILoginThrottle"/>. Right for a single self-hosted container; a host that
/// runs several instances registers a shared store instead, or each instance would hand out its
/// own five free attempts.
/// </summary>
public sealed class MemoryLoginThrottle : ILoginThrottle
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _time;

    public MemoryLoginThrottle() : this(TimeProvider.System)
    {
    }

    public MemoryLoginThrottle(TimeProvider time)
    {
        _time = time;
    }

    public Task<TimeSpan?> GetLockoutAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            if (_entries.TryGetValue(key, out var entry) && entry.LockedUntil > now)
            {
                return Task.FromResult<TimeSpan?>(entry.LockedUntil - now);
            }

            return Task.FromResult<TimeSpan?>(null);
        }
    }

    public Task<TimeSpan?> RecordFailureAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            Prune(now);

            if (!_entries.TryGetValue(key, out var entry) || entry.ForgetAt <= now)
            {
                entry = new Entry();
            }

            entry.Failures++;
            entry.ForgetAt = now + LoginThrottlePolicy.FailureWindow;

            var lockout = LoginThrottlePolicy.LockoutAfter(entry.Failures);
            if (lockout is { } duration)
            {
                entry.LockedUntil = now + duration;
                if (entry.LockedUntil > entry.ForgetAt)
                {
                    entry.ForgetAt = entry.LockedUntil;
                }
            }

            _entries[key] = entry;
            return Task.FromResult(lockout);
        }
    }

    public Task RecordSuccessAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _entries.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <summary>Drops forgotten entries so a spray of made-up emails cannot grow the map forever.</summary>
    private void Prune(DateTimeOffset now)
    {
        if (_entries.Count < 1024)
        {
            return;
        }

        foreach (var stale in _entries.Where(e => e.Value.ForgetAt <= now).Select(e => e.Key).ToList())
        {
            _entries.Remove(stale);
        }
    }

    private sealed class Entry
    {
        public long Failures;
        public DateTimeOffset ForgetAt;
        public DateTimeOffset LockedUntil;
    }
}
