using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Famick.HomeManagement.TestSupport.Containers;

/// <summary>
/// xUnit fixture that spins up a real Postgres 16 container, applies the
/// <see cref="HomeManagementDbContext"/> migrations, and exposes the connection
/// string and a fresh DbContext for tests.
///
/// Use as a class fixture (one container per test class) or a collection fixture
/// (one container shared across a collection — much faster for large suites).
/// </summary>
public class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("famick_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Apply migrations once on startup so every test class gets a schema-current DB.
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var context = new HomeManagementDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Returns a new <see cref="HomeManagementDbContext"/> bound to the running
    /// container. Each call returns a fresh instance — disposal is the caller's
    /// responsibility.
    /// </summary>
    public HomeManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new HomeManagementDbContext(options);
    }
}
