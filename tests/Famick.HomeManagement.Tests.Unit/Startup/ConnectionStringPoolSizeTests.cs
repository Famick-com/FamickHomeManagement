using Famick.HomeManagement.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Famick.HomeManagement.Tests.Unit.Startup;

/// <summary>
/// Npgsql defaults to a 100-connection pool per process. A burst of parallel
/// requests can claim all 100 and hold them idle, and several replicas doing that
/// at once can exhaust the database's own connection ceiling — which fails as
/// "FATAL: sorry, too many clients already" rather than as a slow request
/// (issue #80). These cover the ceiling that keeps that from happening.
/// </summary>
public class ConnectionStringPoolSizeTests
{
    private const string BaseConnectionString =
        "Host=localhost;Port=5432;Database=homemanagement;Username=postgres;Password=secret";

    private static IConfiguration Config(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s =>
                new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    private static int PoolSizeOf(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString).MaxPoolSize;

    [Fact]
    public void A_connection_string_without_a_pool_size_gets_the_capped_default()
    {
        var resolved = InfrastructureStartup.ResolveConnectionString(
            Config(("ConnectionStrings:DefaultConnection", BaseConnectionString)));

        PoolSizeOf(resolved).Should().Be(30);
    }

    [Fact]
    public void The_default_stays_well_under_the_Npgsql_default_of_100()
    {
        // The whole point of the cap: several replicas at Npgsql's default would
        // together ask for more connections than the server allows.
        var resolved = InfrastructureStartup.ResolveConnectionString(
            Config(("ConnectionStrings:DefaultConnection", BaseConnectionString)));

        PoolSizeOf(resolved).Should().BeLessThan(100);
    }

    [Fact]
    public void A_deployment_can_raise_the_ceiling_through_configuration()
    {
        var resolved = InfrastructureStartup.ResolveConnectionString(Config(
            ("ConnectionStrings:DefaultConnection", BaseConnectionString),
            ("Database:MaxPoolSize", "75")));

        PoolSizeOf(resolved).Should().Be(75);
    }

    [Theory]
    // Both spellings Npgsql accepts for this keyword.
    [InlineData("Maximum Pool Size")]
    [InlineData("MaxPoolSize")]
    public void An_explicit_pool_size_in_the_connection_string_is_left_alone(string keyword)
    {
        // Operators who have already tuned this must not have it silently overridden.
        var resolved = InfrastructureStartup.ResolveConnectionString(Config(
            ("ConnectionStrings:DefaultConnection", $"{BaseConnectionString};{keyword}=12"),
            ("Database:MaxPoolSize", "75")));

        PoolSizeOf(resolved).Should().Be(12);
    }

    [Fact]
    public void The_rest_of_the_connection_string_survives_the_rewrite()
    {
        var resolved = InfrastructureStartup.ResolveConnectionString(
            Config(("ConnectionStrings:DefaultConnection", BaseConnectionString)));

        var builder = new NpgsqlConnectionStringBuilder(resolved);
        builder.Host.Should().Be("localhost");
        builder.Port.Should().Be(5432);
        builder.Database.Should().Be("homemanagement");
        builder.Username.Should().Be("postgres");
        builder.Password.Should().Be("secret");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_connection_string_is_passed_through_untouched(string? connectionString)
    {
        // Configuration validation elsewhere owns this failure; parsing an empty
        // string here would just turn it into a confusing Npgsql error.
        var resolved = InfrastructureStartup.ResolveConnectionString(
            Config(("ConnectionStrings:DefaultConnection", connectionString)));

        resolved.Should().BeNullOrEmpty();
    }
}
