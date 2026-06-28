using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Famick.HomeManagement.Tests.Unit.Data;

public class MasterProductSeederTests : IDisposable
{
    private readonly HomeManagementDbContext _db;
    private readonly MasterProductSeeder _seeder;

    public MasterProductSeederTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"mp-seeder-{Guid.NewGuid()}")
            .Options;
        _db = new HomeManagementDbContext(options);
        _seeder = new MasterProductSeeder(_db, NullLogger<MasterProductSeeder>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static MasterProductSeedDto Dto(string seedKey, string name, string category = "Dairy",
        int healthScore = 3) => new()
    {
        Name = name,
        Category = category,
        SeedKey = seedKey,
        ImageSlug = seedKey,
        HealthScore = healthScore
    };

    private MasterProduct AddSeeded(string seedKey, string name, Action<MasterProduct>? configure = null)
    {
        var mp = new MasterProduct
        {
            Id = Guid.NewGuid(),
            SeedKey = seedKey,
            Name = name,
            Category = "Dairy",
            Source = MasterProductSource.Seeded
        };
        configure?.Invoke(mp);
        _db.MasterProducts.Add(mp);
        _db.SaveChanges();
        return mp;
    }

    [Fact]
    public async Task Upsert_inserts_row_for_new_seed_key()
    {
        await _seeder.UpsertSeededAsync([Dto("whole-milk", "Whole Milk")]);

        var rows = await _db.MasterProducts.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].SeedKey.Should().Be("whole-milk");
        rows[0].Source.Should().Be(MasterProductSource.Seeded);
    }

    [Fact]
    public async Task Upsert_updates_changed_fields_keeping_identity()
    {
        var original = AddSeeded("whole-milk", "Whole Milk", mp => mp.HealthScore = 3);

        await _seeder.UpsertSeededAsync([Dto("whole-milk", "Whole Milk", healthScore: 5)]);

        var row = await _db.MasterProducts.SingleAsync();
        row.Id.Should().Be(original.Id);
        row.HealthScore.Should().Be(5);
    }

    [Fact]
    public async Task Upsert_rename_keeps_same_row()
    {
        var original = AddSeeded("whole-milk", "Whole Milk");

        await _seeder.UpsertSeededAsync([Dto("whole-milk", "Whole Milk (Vitamin D)")]);

        var rows = await _db.MasterProducts.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(original.Id);
        rows[0].Name.Should().Be("Whole Milk (Vitamin D)");
    }

    [Fact]
    public async Task Upsert_never_touches_admin_or_tenant_rows()
    {
        var admin = new MasterProduct
        {
            Id = Guid.NewGuid(), Name = "Whole Milk", Category = "Dairy",
            Source = MasterProductSource.AdminCreated, HealthScore = 1
        };
        var tenant = new MasterProduct
        {
            Id = Guid.NewGuid(), Name = "2% Milk", Category = "Dairy",
            Source = MasterProductSource.TenantContributed, HealthScore = 1
        };
        _db.MasterProducts.AddRange(admin, tenant);
        await _db.SaveChangesAsync();

        // Seed entries share the names but only match Seeded rows by seed key,
        // so a brand-new Seeded row is inserted and the admin/tenant rows are left alone.
        await _seeder.UpsertSeededAsync([Dto("whole-milk", "Whole Milk", healthScore: 5)]);

        var reloadedAdmin = await _db.MasterProducts.FindAsync(admin.Id);
        var reloadedTenant = await _db.MasterProducts.FindAsync(tenant.Id);
        reloadedAdmin!.HealthScore.Should().Be(1);
        reloadedAdmin.Source.Should().Be(MasterProductSource.AdminCreated);
        reloadedTenant!.HealthScore.Should().Be(1);
        reloadedTenant.Source.Should().Be(MasterProductSource.TenantContributed);

        _db.MasterProducts.Count(mp => mp.Source == MasterProductSource.Seeded).Should().Be(1);
    }

    [Fact]
    public async Task Upsert_keeps_rows_removed_from_seed_file()
    {
        var retired = AddSeeded("discontinued-item", "Discontinued Item");

        // Seed file no longer contains the entry.
        await _seeder.UpsertSeededAsync([Dto("whole-milk", "Whole Milk")]);

        (await _db.MasterProducts.FindAsync(retired.Id)).Should().NotBeNull();
        _db.MasterProducts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Upsert_preserves_enrichment_fields()
    {
        var enriched = AddSeeded("whole-milk", "Whole Milk", mp =>
        {
            mp.Description = "Enriched by a tenant";
            mp.ServingSize = 240m;
            mp.ServingUnit = "ml";
        });

        await _seeder.UpsertSeededAsync([Dto("whole-milk", "Whole Milk", healthScore: 5)]);

        var row = await _db.MasterProducts.FindAsync(enriched.Id);
        row!.Description.Should().Be("Enriched by a tenant");
        row.ServingSize.Should().Be(240m);
        row.ServingUnit.Should().Be("ml");
        row.HealthScore.Should().Be(5); // seed-owned field still updated
    }

    [Fact]
    public async Task SeedAsync_is_a_no_op_when_hash_unchanged()
    {
        await _seeder.SeedAsync();
        var countAfterFirst = await _db.MasterProducts.CountAsync();
        countAfterFirst.Should().BeGreaterThan(0);

        await _seeder.SeedAsync();

        (await _db.MasterProducts.CountAsync()).Should().Be(countAfterFirst);
        (await _db.AppMetadata.CountAsync(m => m.Key == "MasterCatalogSeedHash")).Should().Be(1);
    }
}
