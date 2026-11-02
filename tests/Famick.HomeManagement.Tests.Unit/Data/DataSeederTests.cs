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
