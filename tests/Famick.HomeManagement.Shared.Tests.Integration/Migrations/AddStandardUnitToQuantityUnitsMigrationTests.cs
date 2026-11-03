using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Famick.HomeManagement.Shared.Tests.Integration.Migrations;

/// <summary>
/// The AddStandardUnitToQuantityUnits migration tags each household's existing units by name. It must
/// tag the seeded defaults whatever their case, and leave anything else alone rather than guess.
/// </summary>
public class AddStandardUnitToQuantityUnitsMigrationTests : IAsyncLifetime
{
    private const string Before = "20261102223610_AddDashAndPinchUnits";

    private readonly Guid _tenant = Guid.NewGuid();

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("famick_standard_unit_migration_test")
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
    public async Task Existing_units_are_tagged_by_their_default_name_in_any_case()
    {
        await using (var db = Context())
            await db.Database.MigrateAsync();

        await using var check = Context();
        var tags = await check.QuantityUnits.IgnoreQueryFilters()
            .ToDictionaryAsync(u => u.Name, u => u.StandardUnit);

        tags.Should().BeEquivalentTo(new Dictionary<string, StandardUnit?>
        {
            ["Teaspoon"] = StandardUnit.Teaspoon,
            ["TABLESPOON"] = StandardUnit.Tablespoon,
            ["cup"] = StandardUnit.Cup,
            ["Gallon"] = StandardUnit.Gallon,
            ["Ounce"] = StandardUnit.Ounce,
            ["Kilogram"] = StandardUnit.Kilogram,
            ["Tbsp"] = null,
            ["Cups"] = null,
            ["Bag"] = null,
            ["Pinch"] = null,
        });
    }

    [Fact]
    public async Task Only_untouched_imperial_descriptions_become_US()
    {
        await using (var db = Context())
            await db.Database.MigrateAsync();

        await using var check = Context();
        var descriptions = await check.QuantityUnits.IgnoreQueryFilters()
            .ToDictionaryAsync(u => u.Name, u => u.Description);

        descriptions["Gallon"].Should().Be("gal - US volume");
        descriptions["Ounce"].Should().Be("oz - imperial weight");
        descriptions["Bag"].Should().Be("my own imperial volume");
    }

    private async Task SeedAsync()
    {
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();

        async Task Unit(string name, string? description = null)
        {
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO quantity_units ("Id", "TenantId", "Name", "NamePlural", "Description", "IsActive", "CreatedAt")
                VALUES (gen_random_uuid(), @tenant, @name, @name, @description, true, now())
                """, conn);
            cmd.Parameters.AddWithValue("tenant", _tenant);
            cmd.Parameters.AddWithValue("name", name);
            cmd.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        await Unit("Teaspoon");
        await Unit("TABLESPOON");
        await Unit("cup");
        await Unit("Gallon", "gal - imperial volume");
        await Unit("Ounce", "oz - imperial weight");
        await Unit("Kilogram");
        await Unit("Tbsp");
        await Unit("Cups");
        await Unit("Bag", "my own imperial volume");
        await Unit("Pinch");
    }
}
