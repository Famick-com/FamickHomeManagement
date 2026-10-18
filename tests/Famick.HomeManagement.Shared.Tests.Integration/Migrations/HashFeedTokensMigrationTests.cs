using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Famick.HomeManagement.Shared.Tests.Integration.Migrations;

/// <summary>
/// The HashFeedTokens migration replaces each stored feed token with its hash. The SQL has to
/// produce exactly what <see cref="FeedTokens.Hash"/> does, or every feed URL already in someone's
/// calendar or contacts app stops working the moment the migration runs.
/// </summary>
public class HashFeedTokensMigrationTests : IAsyncLifetime
{
    private const string Before = "20261016160053_HouseholdInvitations";
    private const string CalendarToken = "aB3-_xYz0123456789abcdefghijklmnopqrstuvwx";
    private const string ContactToken = "Zz9_-contact-token-0123456789ABCDEFGHIJKLMN";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("famick_feed_token_migration_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Before);
        await SeedAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private HomeManagementDbContext Context() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>().UseNpgsql(_container.GetConnectionString()).Options);

    [Fact]
    public async Task Existing_tokens_are_hashed_the_way_the_feed_looks_them_up()
    {
        await using (var db = Context())
            await db.Database.MigrateAsync();

        await using var check = Context();
        (await check.UserCalendarIcsTokens.IgnoreQueryFilters().SingleAsync()).TokenHash
            .Should().Be(FeedTokens.Hash(CalendarToken));
        (await check.UserContactVcfTokens.IgnoreQueryFilters().SingleAsync()).TokenHash
            .Should().Be(FeedTokens.Hash(ContactToken));
    }

    [Fact]
    public async Task Going_back_down_revokes_every_token()
    {
        await using (var db = Context())
            await db.Database.MigrateAsync();
        await using (var db = Context())
            await db.GetService<IMigrator>().MigrateAsync(Before);

        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        foreach (var table in new[] { "user_calendar_ics_tokens", "user_contact_vcf_tokens" })
        {
            await using var cmd = new NpgsqlCommand($"SELECT bool_and(is_revoked) FROM {table}", conn);
            ((bool)(await cmd.ExecuteScalarAsync())!).Should().BeTrue(table);
        }
    }

    private async Task SeedAsync()
    {
        var (tenant, user) = (Guid.NewGuid(), Guid.NewGuid());
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();

        async Task Exec(string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("tenant", tenant);
            cmd.Parameters.AddWithValue("user", user);
            cmd.Parameters.AddWithValue("ics", CalendarToken);
            cmd.Parameters.AddWithValue("vcf", ContactToken);
            await cmd.ExecuteNonQueryAsync();
        }

        await Exec("INSERT INTO tenants (id, name, created_at) VALUES (@tenant, 'Household', now())");
        await Exec(
            """
            INSERT INTO users ("Id", "TenantId", "Username", "Email", "PasswordHash", "FirstName", "LastName",
                               "IsActive", "MustChangePassword", "CreatedAt")
            VALUES (@user, @tenant, 'a@example.com', 'a@example.com', 'x', 'Test', 'User', true, false, now())
            """);
        await Exec(
            """
            INSERT INTO user_calendar_ics_tokens (id, tenant_id, user_id, token)
            VALUES (gen_random_uuid(), @tenant, @user, @ics)
            """);
        await Exec(
            """
            INSERT INTO user_contact_vcf_tokens (id, tenant_id, user_id, token)
            VALUES (gen_random_uuid(), @tenant, @user, @vcf)
            """);
    }
}
