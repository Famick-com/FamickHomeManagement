using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

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
}
