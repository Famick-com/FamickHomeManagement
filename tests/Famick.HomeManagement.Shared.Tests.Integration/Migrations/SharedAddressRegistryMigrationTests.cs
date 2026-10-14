using Famick.HomeManagement.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;

namespace Famick.HomeManagement.Shared.Tests.Integration.Migrations;

/// <summary>
/// Issue #119: the SharedAddressRegistry migration merges addresses that share a hash into one
/// row and moves lines 2-4 onto whoever references it. Seeded at the schema just before it,
/// in SQL, because the current model no longer has the columns being moved.
/// </summary>
public class SharedAddressRegistryMigrationTests : IAsyncLifetime
{
    private const string Before = "20261013164730_RefreshTokenFamilyNotEmpty";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("famick_migration_test")
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
    public async Task Duplicates_merge_into_one_row_and_the_lines_move_to_their_references()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var hand = Guid.NewGuid();
        var laterHand = Guid.NewGuid();
        var verified = Guid.NewGuid();
        var other = Guid.NewGuid();
        var unhashed1 = Guid.NewGuid();
        var unhashed2 = Guid.NewGuid();
        var contactA = Guid.NewGuid();
        var contactB = Guid.NewGuid();
        var contactC = Guid.NewGuid();
        var aOnHand = Guid.NewGuid();
        var aOnVerified = Guid.NewGuid();
        var bOnHand = Guid.NewGuid();
        var cOnHand = Guid.NewGuid();
        var cOnLaterHand = Guid.NewGuid();

        await using (var conn = await OpenAsync())
        {
            await InsertAsync(conn, "addresses", new()
            {
                ["id"] = hand, ["address_line_1"] = "1 Main St", ["address_line_2"] = "Bldg A",
                ["address_line_3"] = "c/o Hand", ["normalized_hash"] = "h1", ["created_at"] = At(2020, 1)
            });
            await InsertAsync(conn, "addresses", new()
            {
                ["id"] = laterHand, ["address_line_1"] = "1 main st", ["normalized_hash"] = "h1",
                ["created_at"] = At(2020, 6)
            });
            // Newest, but the only one a provider vouched for, so it survives.
            await InsertAsync(conn, "addresses", new()
            {
                ["id"] = verified, ["address_line_1"] = "1 MAIN ST", ["provider_source"] = "Geoapify",
                ["provider_place_id"] = "geo-1", ["latitude"] = 39.8, ["normalized_hash"] = "h1",
                ["created_at"] = At(2021, 1)
            });
            await InsertAsync(conn, "addresses", new() { ["id"] = other, ["address_line_1"] = "2 Other St", ["normalized_hash"] = "h2" });
            await InsertAsync(conn, "addresses", new() { ["id"] = unhashed1, ["address_line_1"] = null });
            await InsertAsync(conn, "addresses", new() { ["id"] = unhashed2, ["address_line_1"] = null });

            await InsertAsync(conn, "tenants", new() { ["id"] = tenant, ["name"] = "Household", ["address_id"] = hand });
            await InsertAsync(conn, "users", new() { ["Id"] = user, ["TenantId"] = tenant, ["ContactId"] = null });
            foreach (var contact in new[] { contactA, contactB, contactC })
                await InsertAsync(conn, "contacts", new()
                {
                    ["Id"] = contact, ["TenantId"] = tenant, ["CreatedByUserId"] = user, ["ParentContactId"] = null,
                    ["LinkedUserId"] = null, ["HouseholdTenantId"] = null
                });

            // A links the hand row (primary) and the verified row: after the merge it would link
            // the survivor twice, so the hand link goes and the survivor link becomes primary.
            await LinkAsync(conn, aOnHand, tenant, contactA, hand, primary: true, At(2020, 1));
            await LinkAsync(conn, aOnVerified, tenant, contactA, verified, primary: false, At(2021, 1));
            // B keeps its own line 2 over the row's, and picks up the row's line 3.
            await LinkAsync(conn, bOnHand, tenant, contactB, hand, primary: true, At(2020, 1), line2: "Apt 9");
            // C links two duplicates but not the survivor: its older link stays.
            await LinkAsync(conn, cOnHand, tenant, contactC, hand, primary: true, At(2020, 1));
            await LinkAsync(conn, cOnLaterHand, tenant, contactC, laterHand, primary: false, At(2020, 6));
        }

        await using (var db = Context())
            await db.Database.MigrateAsync();

        await using var check = await OpenAsync();
        (await IdsAsync(check, "SELECT id FROM addresses WHERE normalized_hash = 'h1'")).Should().Equal(verified);
        (await IdsAsync(check, "SELECT id FROM addresses WHERE normalized_hash IS DISTINCT FROM 'h1'"))
            .Should().BeEquivalentTo([other, unhashed1, unhashed2]);

        var household = await RowAsync(check, $"SELECT address_id, address_line_2, address_line_3 FROM tenants WHERE id = '{tenant}'");
        household.Should().Equal(verified, "Bldg A", "c/o Hand");

        var aLinks = await RowsAsync(check, $@"SELECT ""Id"", ""AddressId"", ""IsPrimary"" FROM contact_addresses WHERE ""ContactId"" = '{contactA}'");
        aLinks.Should().ContainSingle().Which.Should().Equal(aOnVerified, verified, true);

        var bLink = await RowAsync(check, $@"SELECT ""AddressId"", ""AddressLine2"", ""AddressLine3"" FROM contact_addresses WHERE ""Id"" = '{bOnHand}'");
        bLink.Should().Equal(verified, "Apt 9", "c/o Hand");

        var cLinks = await RowsAsync(check, $@"SELECT ""Id"", ""AddressId"", ""IsPrimary"" FROM contact_addresses WHERE ""ContactId"" = '{contactC}'");
        cLinks.Should().ContainSingle().Which.Should().Equal(cOnHand, verified, true);

        var columns = await RowsAsync(check,
            "SELECT column_name FROM information_schema.columns WHERE table_name = 'addresses' AND column_name LIKE 'address_line_%'");
        columns.Select(c => c[0]).Should().Equal("address_line_1");

        var duplicate = () => InsertAsync(check, "addresses", new() { ["id"] = Guid.NewGuid(), ["normalized_hash"] = "h2" });
        (await duplicate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    private static DateTime At(int year, int month) => new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    private static Task LinkAsync(NpgsqlConnection conn, Guid id, Guid tenant, Guid contact, Guid address,
        bool primary, DateTime createdAt, string? line2 = null) =>
        InsertAsync(conn, "contact_addresses", new()
        {
            ["Id"] = id, ["TenantId"] = tenant, ["ContactId"] = contact, ["AddressId"] = address,
            ["Tag"] = "Home", ["IsPrimary"] = primary, ["AddressLine2"] = line2, ["CreatedAt"] = createdAt
        });

    /// <summary>
    /// Inserts a row, filling every other NOT NULL column that has no default with a
    /// placeholder of its type, so the seed does not have to track unrelated columns.
    /// </summary>
    private static async Task InsertAsync(NpgsqlConnection conn, string table, Dictionary<string, object?> values)
    {
        var required = new List<(string Name, string Type)>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns " +
            "WHERE table_schema = 'public' AND table_name = @t AND is_nullable = 'NO' AND column_default IS NULL", conn))
        {
            cmd.Parameters.AddWithValue("t", table);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                required.Add((reader.GetString(0), reader.GetString(1)));
        }

        var columns = values.Keys.ToList();
        var parameters = values.Values.Select(v => new NpgsqlParameter { Value = v ?? DBNull.Value }).ToList();
        foreach (var (name, type) in required.Where(r => !values.ContainsKey(r.Name)))
        {
            columns.Add(name);
            parameters.Add(type switch
            {
                "uuid" => new NpgsqlParameter { Value = Guid.NewGuid() },
                "boolean" => new NpgsqlParameter { Value = false },
                "integer" or "smallint" => new NpgsqlParameter { Value = 0 },
                "bigint" => new NpgsqlParameter { Value = 0L },
                "numeric" => new NpgsqlParameter { Value = 0m },
                "double precision" => new NpgsqlParameter { Value = 0d },
                "timestamp with time zone" => new NpgsqlParameter { Value = DateTime.UtcNow },
                "timestamp without time zone" => new NpgsqlParameter { Value = DateTime.Now, NpgsqlDbType = NpgsqlDbType.Timestamp },
                "jsonb" => new NpgsqlParameter { Value = "{}", NpgsqlDbType = NpgsqlDbType.Jsonb },
                _ => new NpgsqlParameter { Value = $"x-{Guid.NewGuid():N}" }
            });
        }

        var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        var placeholders = string.Join(", ", parameters.Select((_, i) => $"${i + 1}"));
        await using var insert = new NpgsqlCommand($"INSERT INTO {table} ({columnList}) VALUES ({placeholders})", conn);
        insert.Parameters.AddRange(parameters.ToArray());
        await insert.ExecuteNonQueryAsync();
    }

    private static async Task<List<object?[]>> RowsAsync(NpgsqlConnection conn, string sql)
    {
        var rows = new List<object?[]>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    private static async Task<object?[]> RowAsync(NpgsqlConnection conn, string sql) =>
        (await RowsAsync(conn, sql)).Single();

    private static async Task<List<Guid>> IdsAsync(NpgsqlConnection conn, string sql) =>
        (await RowsAsync(conn, sql)).Select(r => (Guid)r[0]!).ToList();
}
