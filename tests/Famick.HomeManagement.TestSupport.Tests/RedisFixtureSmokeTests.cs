using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using StackExchange.Redis;

namespace Famick.HomeManagement.TestSupport.Tests;

/// <summary>
/// Smoke test that the Redis Testcontainers fixture boots and accepts SET/GET
/// operations. Pairs with PostgresFixtureSmokeTests as the Phase 0 fixture-readiness gate.
/// </summary>
public class RedisFixtureSmokeTests : IClassFixture<RedisContainerFixture>
{
    private readonly RedisContainerFixture _fixture;

    public RedisFixtureSmokeTests(RedisContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SetAndGet_round_trip_succeeds()
    {
        var key = $"smoke-{Guid.NewGuid():N}";
        await _fixture.Database.StringSetAsync(key, "ok", TimeSpan.FromSeconds(30));
        var value = await _fixture.Database.StringGetAsync(key);
        value.ToString().Should().Be("ok");
    }

    [Fact]
    public void Multiplexer_is_connected()
    {
        _fixture.Multiplexer.IsConnected.Should().BeTrue();
    }
}
