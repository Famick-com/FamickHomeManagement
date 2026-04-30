using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// In-memory <see cref="IAddressSuggestionCache"/> backed by
/// <see cref="IMemoryCache"/>. Entries expire after
/// <see cref="IAddressSuggestionCache.DefaultTtl"/>.
/// </summary>
public class AddressSuggestionCache : IAddressSuggestionCache
{
    private readonly IMemoryCache _cache;

    public AddressSuggestionCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Guid Store(ExternalAddressSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        var id = Guid.NewGuid();
        // Sliding TTL: every TryGet hit refreshes the entry's expiration so a
        // user who dawdles in a form (typed a parent suggestion, took a
        // phone call, came back to pick a unit) doesn't lose the cached
        // suggestion mid-flow.
        _cache.Set(Key(id), suggestion, new MemoryCacheEntryOptions
        {
            SlidingExpiration = IAddressSuggestionCache.DefaultTtl
        });
        return id;
    }

    public ExternalAddressSuggestion? TryGet(Guid suggestionId) =>
        _cache.TryGetValue(Key(suggestionId), out ExternalAddressSuggestion? value)
            ? value
            : null;

    private static string Key(Guid id) => $"addr-sugg:{id:N}";
}
