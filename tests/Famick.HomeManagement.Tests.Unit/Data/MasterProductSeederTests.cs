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

    private static MasterProductChildBarcodeSeedDto Child(string barcode, string? note = null) =>
        new() { Barcode = barcode, Note = note };

    private void AddBarcode(MasterProduct mp, string barcode, bool isChildCandidate, string? note = null)
    {
        _db.MasterProductBarcodes.Add(new MasterProductBarcode
        {
            Id = Guid.NewGuid(),
            MasterProductId = mp.Id,
            Barcode = barcode,
            Note = note,
            IsChildCandidate = isChildCandidate
        });
        _db.SaveChanges();
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
    public async Task Upsert_assigns_seed_key_to_null_keyed_seeded_row()
    {
        // A seeded row with no key (e.g. added before seed keys existed). It should be
        // self-healed to slug(name) and then matched/updated by the seed entry.
        var orphan = AddSeeded("ignored", "Whole Milk");
        orphan.SeedKey = null;
        await _db.SaveChangesAsync();

        await _seeder.UpsertSeededAsync([Dto("whole-milk", "Whole Milk", healthScore: 5)]);

        var rows = await _db.MasterProducts.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(orphan.Id);
        rows[0].SeedKey.Should().Be("whole-milk");
        rows[0].HealthScore.Should().Be(5);
    }

    [Fact]
    public async Task Upsert_leaves_ghost_duplicate_unkeyed()
    {
        // A properly-keyed row plus a null-keyed ghost of the same name: the ghost's slug
        // collides, so it stays unkeyed (the export pass collapses it) rather than
        // duplicating or violating the unique key.
        AddSeeded("whole-milk", "Whole Milk");
        var ghost = AddSeeded("ignored", "Whole Milk");
        ghost.SeedKey = null;
        await _db.SaveChangesAsync();

        await _seeder.UpsertSeededAsync([Dto("whole-milk", "Whole Milk")]);

        var milk = await _db.MasterProducts.Where(m => m.Name == "Whole Milk").ToListAsync();
        milk.Should().HaveCount(2);
        milk.Count(m => m.SeedKey == "whole-milk").Should().Be(1);
        milk.Count(m => m.SeedKey == null).Should().Be(1);
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

    [Fact]
    public async Task InitialSeed_skips_blank_and_repeated_keys_and_keeps_child_barcodes_on_their_product()
    {
        var repeat = Dto("2-percent-milk", "2% Milk Again");
        repeat.ChildBarcodes = [Child("111111111111")];
        var blank = new MasterProductSeedDto { Name = "!!!", Category = "Dairy" };
        var eggs = Dto("eggs", "Eggs");
        eggs.ChildBarcodes = [Child("036000291452")];

        await _seeder.InitialSeedAsync([Dto("2-percent-milk", "2% Milk"), repeat, blank, eggs]);

        (await _db.MasterProducts.Select(m => m.Name).ToListAsync()).Should().BeEquivalentTo("2% Milk", "Eggs");
        var row = await _db.MasterProductBarcodes.SingleAsync();
        row.Barcode.Should().Be("036000291452");
        row.MasterProductId.Should().Be((await _db.MasterProducts.SingleAsync(m => m.Name == "Eggs")).Id);
    }

    [Fact]
    public async Task Upsert_adds_child_barcodes_as_candidates()
    {
        var mp = AddSeeded("2-percent-milk", "2% Milk");
        var dto = Dto("2-percent-milk", "2% Milk");
        dto.ChildBarcodes = [Child("036000291452", "Brand X, gallon")];

        await _seeder.UpsertSeededAsync([dto]);

        var row = await _db.MasterProductBarcodes.SingleAsync();
        row.MasterProductId.Should().Be(mp.Id);
        row.IsChildCandidate.Should().BeTrue();
        row.Note.Should().Be("Brand X, gallon");
    }

    [Fact]
    public async Task Upsert_adds_child_barcodes_to_a_newly_inserted_product()
    {
        var dto = Dto("2-percent-milk", "2% Milk");
        dto.ChildBarcodes = [Child("036000291452")];

        await _seeder.UpsertSeededAsync([dto]);

        var product = await _db.MasterProducts.SingleAsync();
        var row = await _db.MasterProductBarcodes.SingleAsync();
        row.MasterProductId.Should().Be(product.Id);
        row.IsChildCandidate.Should().BeTrue();
    }

    [Fact]
    public async Task Upsert_relabels_a_child_barcode_and_keeps_ones_missing_from_the_file()
    {
        var mp = AddSeeded("2-percent-milk", "2% Milk");
        AddBarcode(mp, "036000291452", isChildCandidate: true, note: "old label");
        AddBarcode(mp, "012345678905", isChildCandidate: true, note: "added in the catalog admin");
        var dto = Dto("2-percent-milk", "2% Milk");
        dto.ChildBarcodes = [Child("036000291452", "new label")];

        await _seeder.UpsertSeededAsync([dto]);

        var rows = await _db.MasterProductBarcodes.ToDictionaryAsync(b => b.Barcode);
        rows.Should().HaveCount(2);
        rows["036000291452"].Note.Should().Be("new label");
        rows["012345678905"].Note.Should().Be("added in the catalog admin");
    }

    [Fact]
    public async Task Upsert_skips_a_child_barcode_that_belongs_to_another_product()
    {
        var other = AddSeeded("whole-milk", "Whole Milk");
        AddBarcode(other, "036000291452", isChildCandidate: false);
        AddSeeded("2-percent-milk", "2% Milk");
        var dto = Dto("2-percent-milk", "2% Milk");
        dto.ChildBarcodes = [Child("036000291452")];

        await _seeder.UpsertSeededAsync([dto]);

        var row = await _db.MasterProductBarcodes.SingleAsync();
        row.MasterProductId.Should().Be(other.Id);
        row.IsChildCandidate.Should().BeFalse();
    }
}
