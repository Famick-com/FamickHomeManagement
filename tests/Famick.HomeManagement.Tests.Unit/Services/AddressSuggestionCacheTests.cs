using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class AddressSuggestionCacheTests
{
    private static AddressSuggestionCache CreateCache(out MemoryCache memoryCache)
    {
        memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        return new AddressSuggestionCache(memoryCache);
    }

    [Fact]
    public void Store_Then_TryGet_ReturnsOriginal()
    {
        var cache = CreateCache(out _);
        var suggestion = new ExternalAddressSuggestion
        {
            Line1 = "123 Main St",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701"
        };

        var id = cache.Store(suggestion);
        var result = cache.TryGet(id);

        id.Should().NotBe(Guid.Empty);
        result.Should().BeSameAs(suggestion);
    }

    [Fact]
    public void TryGet_ReturnsNull_ForUnknownId()
    {
        var cache = CreateCache(out _);

        cache.TryGet(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void Store_GeneratesDistinctIds()
    {
        var cache = CreateCache(out _);
        var a = cache.Store(new ExternalAddressSuggestion { Line1 = "a" });
        var b = cache.Store(new ExternalAddressSuggestion { Line1 = "b" });

        a.Should().NotBe(b);
    }

    [Fact]
    public void Store_ThrowsOnNull()
    {
        var cache = CreateCache(out _);

        Action act = () => cache.Store(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Entry_IsEvicted_WhenUnderlyingMemoryCacheCompacts()
    {
        var cache = CreateCache(out var memoryCache);
        var id = cache.Store(new ExternalAddressSuggestion { Line1 = "123" });

        memoryCache.Clear();

        cache.TryGet(id).Should().BeNull();
    }

    [Fact]
    public void Store_UsesSlidingExpiration_NotAbsolute()
    {
        // Sliding is what protects users who dawdle between picking a parent
        // suggestion and selecting an apt/suite — every TryGet hit should
        // refresh the TTL. This guards the configuration choice; the actual
        // sliding behavior is exercised by IMemoryCache itself.
        var spy = new EntryOptionsSpyMemoryCache();
        var cache = new AddressSuggestionCache(spy);

        cache.Store(new ExternalAddressSuggestion { Line1 = "a" });

        spy.LastEntry.Should().NotBeNull();
        spy.LastEntry!.SlidingExpiration.Should().Be(IAddressSuggestionCache.DefaultTtl);
        spy.LastEntry.AbsoluteExpiration.Should().BeNull();
        spy.LastEntry.AbsoluteExpirationRelativeToNow.Should().BeNull();
    }

    /// <summary>
    /// Minimal spy that captures the cache entry's expiration policy without
    /// needing to control the system clock.
    /// </summary>
    private sealed class EntryOptionsSpyMemoryCache : IMemoryCache
    {
        public CapturedEntry? LastEntry { get; private set; }

        public ICacheEntry CreateEntry(object key)
        {
            var entry = new CapturedEntry(key);
            LastEntry = entry;
            return entry;
        }

        public void Remove(object key) { }
        public bool TryGetValue(object key, out object? value) { value = null; return false; }
        public void Dispose() { }

        public sealed class CapturedEntry : ICacheEntry
        {
            public CapturedEntry(object key) { Key = key; }

            public object Key { get; }
            public object? Value { get; set; }
            public DateTimeOffset? AbsoluteExpiration { get; set; }
            public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
            public TimeSpan? SlidingExpiration { get; set; }
            public IList<IChangeToken> ExpirationTokens { get; } = new List<IChangeToken>();
            public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = new List<PostEvictionCallbackRegistration>();
            public CacheItemPriority Priority { get; set; }
            public long? Size { get; set; }

            public void Dispose() { }
        }
    }
}
