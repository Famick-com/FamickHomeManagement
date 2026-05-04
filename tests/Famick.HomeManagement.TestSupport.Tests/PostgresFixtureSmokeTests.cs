using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.TestSupport.Tests;

/// <summary>
/// Smoke test that the Postgres Testcontainers fixture boots, applies migrations,
/// and accepts a query. Catches Docker availability regressions before they break
/// Phase 1 onward.
/// </summary>
public class PostgresFixtureSmokeTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public PostgresFixtureSmokeTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Connection_string_resolves_and_database_is_reachable()
    {
        _fixture.ConnectionString.Should().NotBeNullOrEmpty();

        await using var context = _fixture.CreateDbContext();
        var canConnect = await context.Database.CanConnectAsync();
        canConnect.Should().BeTrue();
    }

    [Fact]
    public async Task Migrations_have_been_applied()
    {
        await using var context = _fixture.CreateDbContext();
        var applied = await context.Database.GetAppliedMigrationsAsync();
        applied.Should().NotBeEmpty(
            "the fixture's InitializeAsync should have applied every EF migration");
    }
}
