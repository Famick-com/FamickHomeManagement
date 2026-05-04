using Xunit;

namespace Famick.HomeManagement.TestSupport.Containers;

/// <summary>
/// Combined fixture for tests that need both Postgres and Redis (the typical
/// shape for Phase 1+ integration tests where the cloud-cache layer sits in
/// front of a Postgres-backed service).
///
/// Container lifetimes overlap — both start in parallel, both dispose in parallel.
/// </summary>
public class PostgresAndRedisFixture : IAsyncLifetime
{
    public PostgresContainerFixture Postgres { get; } = new();
    public RedisContainerFixture Redis { get; } = new();

    public Task InitializeAsync()
        => Task.WhenAll(Postgres.InitializeAsync(), Redis.InitializeAsync());

    public Task DisposeAsync()
        => Task.WhenAll(Postgres.DisposeAsync(), Redis.DisposeAsync());
}
