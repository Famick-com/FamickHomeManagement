using Famick.HomeManagement.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Famick.HomeManagement.Shared.Tests.Integration.Migrations;

/// <summary>
/// Issue #122: the GlobalUniqueUserEmail migration lower-cases and trims every stored email, and
/// stops with a message naming the address when two users would end up sharing one, rather than
/// merging people it cannot tell apart. Each case gets its own database at the schema before it.
/// </summary>
public class GlobalUniqueUserEmailMigrationTests : IAsyncLifetime
{
    private const string Before = "20261014190333_SharedAddressRegistry";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("famick_email_migration_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Before);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private HomeManagementDbContext Context() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>().UseNpgsql(_container.GetConnectionString()).Options);

    [Fact]
    public async Task Stored_emails_are_normalised()
    {
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        await SeedAsync((tenantA, " Ann@Example.com "), (tenantB, "bob@example.com"));

        await using (var db = Context())
            await db.Database.MigrateAsync();

        (await EmailsAsync()).Should().BeEquivalentTo("ann@example.com", "bob@example.com");
    }

    [Fact]
    public async Task Two_users_that_share_an_address_once_normalised_stop_the_migration()
    {
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        await SeedAsync((tenantA, "Ann@Example.com"), (tenantB, "ann@example.com"), (tenantB, "solo@example.com"));

        await using var db = Context();
        var migrate = () => db.Database.MigrateAsync();

        (await migrate.Should().ThrowAsync<PostgresException>())
            .Which.MessageText.Should().Contain("ann@example.com").And.NotContain("solo@example.com");
        (await EmailsAsync()).Should().Contain("Ann@Example.com", "nothing was changed");
    }

    private async Task SeedAsync(params (Guid Tenant, string Email)[] users)
    {
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        foreach (var tenant in users.Select(u => u.Tenant).Distinct())
        {
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO tenants (id, name, created_at) VALUES (@id, 'Household', now())", conn);
            cmd.Parameters.AddWithValue("id", tenant);
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var (tenant, email) in users)
        {
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO users ("Id", "TenantId", "Username", "Email", "PasswordHash", "FirstName", "LastName",
                                   "IsActive", "MustChangePassword", "CreatedAt")
                VALUES (@id, @tenant, @email, @email, 'x', 'Test', 'User', true, false, now())
                """, conn);
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("tenant", tenant);
            cmd.Parameters.AddWithValue("email", email);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<List<string>> EmailsAsync()
    {
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""SELECT "Email" FROM users""", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var emails = new List<string>();
        while (await reader.ReadAsync()) emails.Add(reader.GetString(0));
        return emails;
    }
}
