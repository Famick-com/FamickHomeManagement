using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Famick.HomeManagement.Tests.Unit.Data;

public class DataSeederTests
{
    private static HomeManagementDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task SeedDefaultDataAsync_GivesANewHouseholdDashAndPinch()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        await new DataSeeder(db, NullLogger<DataSeeder>.Instance).SeedDefaultDataAsync(tenantId);

        var units = await db.QuantityUnits.Where(u => u.TenantId == tenantId).ToListAsync();
        units.Should().ContainSingle(u => u.Name == "Dash" && u.NamePlural == "Dashes");
        units.Should().ContainSingle(u => u.Name == "Pinch" && u.NamePlural == "Pinches");
        units.Should().Contain(u => u.Name == "Teaspoon");
    }

    [Fact]
    public async Task SeedDefaultDataAsync_TagsTheStandardMeasures()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        await new DataSeeder(db, NullLogger<DataSeeder>.Instance).SeedDefaultDataAsync(tenantId);

        var tags = await db.QuantityUnits.Where(u => u.TenantId == tenantId)
            .ToDictionaryAsync(u => u.Name, u => u.StandardUnit);
        tags.Should().Contain(new Dictionary<string, StandardUnit?>
        {
            ["Teaspoon"] = StandardUnit.Teaspoon,
            ["Tablespoon"] = StandardUnit.Tablespoon,
            ["Fluid Ounce"] = StandardUnit.FluidOunce,
            ["Cup"] = StandardUnit.Cup,
            ["Pint"] = StandardUnit.Pint,
            ["Quart"] = StandardUnit.Quart,
            ["Gallon"] = StandardUnit.Gallon,
            ["Milliliter"] = StandardUnit.Milliliter,
            ["Liter"] = StandardUnit.Liter,
            ["Gram"] = StandardUnit.Gram,
            ["Kilogram"] = StandardUnit.Kilogram,
            ["Ounce"] = StandardUnit.Ounce,
            ["Pound"] = StandardUnit.Pound,
        });
        foreach (var untagged in new[] { "Piece", "Pack", "Box", "Can", "Bottle", "Jar", "Bag", "Dash", "Pinch" })
            tags[untagged].Should().BeNull(untagged);
    }

    [Fact]
    public async Task SeedDefaultDataAsync_LeavesAHouseholdThatHasUnitsAlone()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var seeder = new DataSeeder(db, NullLogger<DataSeeder>.Instance);
        await seeder.SeedDefaultDataAsync(tenantId);
        var count = await db.QuantityUnits.CountAsync(u => u.TenantId == tenantId);

        await seeder.SeedDefaultDataAsync(tenantId);

        (await db.QuantityUnits.CountAsync(u => u.TenantId == tenantId)).Should().Be(count);
    }
}
