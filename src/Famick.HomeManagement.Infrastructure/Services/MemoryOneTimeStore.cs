using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// In-process <see cref="IOneTimeStore"/>. Right for a single self-hosted container; a host that
/// runs several instances registers a shared store instead, or a value issued on one instance
/// could not be redeemed on another.
/// </summary>
public sealed class MemoryOneTimeStore : IOneTimeStore, IDisposable
{
    private readonly object _gate = new();
    private readonly MemoryCache _entries = new(new MemoryCacheOptions());

    public Task SetAsync(string key, string value, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _entries.Set(key, value, lifetime);
        }
        return Task.CompletedTask;
    }

    public Task<string?> TakeAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out string? value))
            {
                return Task.FromResult<string?>(null);
            }
            _entries.Remove(key);
            return Task.FromResult(value);
        }
    }

    public void Dispose() => _entries.Dispose();
}
