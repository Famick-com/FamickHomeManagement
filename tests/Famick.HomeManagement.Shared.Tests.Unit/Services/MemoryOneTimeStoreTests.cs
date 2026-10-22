using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class MemoryOneTimeStoreTests
{
    [Fact]
    public async Task Take_ReturnsTheValueOnce()
    {
        using var sut = new MemoryOneTimeStore();
        await sut.SetAsync("k", "v", TimeSpan.FromMinutes(1));

        (await sut.TakeAsync("k")).Should().Be("v");
        (await sut.TakeAsync("k")).Should().BeNull();
    }

    [Fact]
    public async Task Take_UnknownKey_ReturnsNull()
    {
        using var sut = new MemoryOneTimeStore();

        (await sut.TakeAsync("never-set")).Should().BeNull();
    }

    [Fact]
    public async Task Take_AfterTheLifetime_ReturnsNull()
    {
        using var sut = new MemoryOneTimeStore();
        await sut.SetAsync("k", "v", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        (await sut.TakeAsync("k")).Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentTakes_OnlyOneGetsTheValue()
    {
        using var sut = new MemoryOneTimeStore();
        for (var round = 0; round < 200; round++)
        {
            var key = $"k{round}";
            await sut.SetAsync(key, "v", TimeSpan.FromMinutes(1));
            using var start = new ManualResetEventSlim();

            var takes = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() =>
                {
                    start.Wait();
                    return sut.TakeAsync(key);
                }))
                .ToArray();
            start.Set();

            (await Task.WhenAll(takes)).Count(v => v is not null).Should().Be(1);
        }
    }
}
