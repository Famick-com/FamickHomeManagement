using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Famick.HomeManagement.TestSupport.Containers;

/// <summary>
/// xUnit fixture that spins up a real Redis 7 container and exposes a connection
/// multiplexer for tests. Pairs with <see cref="PostgresContainerFixture"/> for
/// tests that exercise the cloud cache layer.
/// </summary>
public class RedisContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine")
        .Build();

    private ConnectionMultiplexer? _multiplexer;

    public string ConnectionString => _container.GetConnectionString();

    public IConnectionMultiplexer Multiplexer
        => _multiplexer ?? throw new InvalidOperationException(
            "Redis fixture has not been initialized — call InitializeAsync first.");

    public IDatabase Database => Multiplexer.GetDatabase();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (_multiplexer is not null)
        {
            await _multiplexer.DisposeAsync();
        }
        await _container.DisposeAsync();
    }
}
