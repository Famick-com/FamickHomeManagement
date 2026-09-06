using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.DataPortability;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.DataPortability;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.DataPortability;

/// <summary>
/// Exercises the export against real Postgres.
/// </summary>
/// <remarks>
/// The tenant-isolation test here is the one that matters. Getting the scope wrong on a bulk read
/// of a whole household does not throw — it writes somebody else's home into a file this user
/// downloads. So the two households are seeded with deliberately colliding data: same product
/// names, same contact names, same locations. Anything that leaks shows up as a row that should
/// not be there rather than as a subtle count mismatch.
/// </remarks>
public class HouseholdExportTests(PostgresContainerFixture fixture) : IClassFixture<PostgresContainerFixture>
{
    private const string PlantedPasswordHash = "$2a$11$PLANTEDHASHVALUEFORTHEEXPORTTEST";
    private const string PlantedRefreshToken = "planted-refresh-token-value-do-not-export";
    private const string PlantedOAuthToken = "planted-oauth-access-token-do-not-export";

    [Fact]
    public async Task ExportContainsOnlyTheRequestingHouseholdsRows()
    {
        var (mine, theirs) = await SeedTwoCollidingHouseholdsAsync();

        var archive = await ExportAsync(mine);

        // Every row of every table must carry my tenant id, or belong to a parent that does.
        var foreignIds = new List<string>();

        foreach (var (entity, rows) in ReadAllTables(archive))
        {
            foreach (var row in rows)
            {
                if (!row.TryGetValue("TenantId", out var tenantId)) continue;

                var value = tenantId.ValueKind == JsonValueKind.String ? tenantId.GetString() : null;
                if (value != null && !string.Equals(value, mine.ToString(), StringComparison.OrdinalIgnoreCase))
                    foreignIds.Add($"{entity}: {value}");
            }
        }

        foreignIds.Should().BeEmpty("the archive must contain no other household's rows");

        // And the colliding names prove the filter ran rather than the tables being empty.
        var products = ReadTable(archive, nameof(Product));
        products.Should().NotBeEmpty("the seeded household has products");
        products.Should().OnlyContain(p => p["Name"].GetString() == "Colliding Product Name");
        products.Should().HaveCount(1, "the other household has a product of the same name that must not appear");

        theirs.Should().NotBe(mine);
    }

    [Fact]
    public async Task ExportOmitsCredentialsEvenWhenTheRowsExist()
    {
        var (mine, _) = await SeedTwoCollidingHouseholdsAsync();

        var archive = await ExportAsync(mine);
        var text = Encoding.UTF8.GetString(archive);

        // Crude on purpose: grep the whole archive. A structural assertion would pass while a
        // credential travelled in some table nobody thought to check.
        text.Should().NotContain(PlantedPasswordHash, "a password hash must never be in an archive");
        text.Should().NotContain(PlantedRefreshToken, "a refresh token would be an account-takeover primitive");
        text.Should().NotContain(PlantedOAuthToken, "store-integration credentials must not travel");

        // The user is still there as an identity reference — that is what an access request wants.
        var users = ReadTable(archive, nameof(User));
        users.Should().ContainSingle(u => u["Email"].GetString()!.StartsWith("mine-"));
        users[0].Should().NotContainKey("PasswordHash");
    }

    [Fact]
    public async Task ManifestDescribesWhatIsInTheArchiveAndWhatWasLeftOut()
    {
        var (mine, _) = await SeedTwoCollidingHouseholdsAsync();

        var archive = await ExportAsync(mine);
        var manifest = ReadManifest(archive);

        manifest.SchemaVersion.Should().Be(1);
        manifest.Source.TenantId.Should().Be(mine);
        manifest.Tables.Should().NotBeEmpty();
        manifest.Counts.Rows.Should().BeGreaterThan(0);

        // Every table entry has to point at a file that is really there, with a checksum.
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        foreach (var table in manifest.Tables)
        {
            zip.GetEntry(table.File).Should().NotBeNull("{0} is listed in the manifest", table.Entity);
            table.Sha256.Should().NotBeNullOrEmpty("{0}", table.Entity);
        }

        // And the exclusions are stated, with reasons, in the archive itself.
        manifest.ExcludedEntities.Should().Contain(e => e.Entity == nameof(RefreshToken));
        manifest.ExcludedEntities.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Reason));
    }

    [Fact]
    public async Task ChildTablesWithoutTheirOwnTenantIdAreStillScopedToTheHousehold()
    {
        var (mine, _) = await SeedTwoCollidingHouseholdsAsync();

        var archive = await ExportAsync(mine);

        // ProductAllergen has no TenantId; it reaches the household through Product. If the join
        // were wrong this would carry the other household's rows, and nothing in the row itself
        // would show it.
        var productIds = ReadTable(archive, nameof(Product))
            .Select(p => p["Id"].GetString())
            .ToHashSet();

        var allergens = ReadTable(archive, nameof(ProductAllergen));
        allergens.Should().NotBeEmpty("the seeded household has one");
        allergens.Should().OnlyContain(a => productIds.Contains(a["ProductId"].GetString()!));
    }

    #region Harness

    private async Task<byte[]> ExportAsync(Guid tenantId)
    {
        await using var db = fixture.CreateDbContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        var writer = new HouseholdArchiveWriter(
            Mock.Of<IFileStorageService>(),
            NullLogger<HouseholdArchiveWriter>.Instance);

        var buffer = new MemoryStream();
        await writer.WriteAsync(
            connection, db.Model, tenantId,
            new ArchiveHousehold { Id = tenantId, Name = "Mine" },
            new ArchiveSource { Mode = "selfHosted", TenantId = tenantId },
            appVersion: "test",
            buffer, includeFiles: false, progress: null, CancellationToken.None);

        return buffer.ToArray();
    }

    /// <summary>
    /// Two households whose data deliberately looks the same, so a scoping mistake shows up as a
    /// row that should not be there rather than as a count that is merely wrong.
    /// </summary>
    private async Task<(Guid Mine, Guid Theirs)> SeedTwoCollidingHouseholdsAsync()
    {
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();

        await using var db = fixture.CreateDbContext();

        foreach (var (tenantId, label) in new[] { (mine, "mine"), (theirs, "theirs") })
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Name = $"Household {label}" });

            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Username = $"{label}-user",
                Email = $"{label}-{Guid.NewGuid():N}@example.com",
                FirstName = "Same", LastName = "Name",
                PasswordHash = PlantedPasswordHash,
                IsActive = true,
            };
            db.Users.Add(user);

            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id,
                TokenHash = PlantedRefreshToken, ExpiresAt = DateTime.UtcNow.AddDays(7),
            });

            db.ShoppingLocations.Add(new ShoppingLocation
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Colliding Store",
                OAuthAccessToken = PlantedOAuthToken,
            });

            // Products need a location and units; same names in both households, again so a
            // scoping mistake shows up as the wrong row rather than a plausible-looking one.
            var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pantry" };
            var unit = new QuantityUnit
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Piece", NamePlural = "Pieces",
            };
            db.Locations.Add(location);
            db.QuantityUnits.Add(unit);

            var product = new Product
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Colliding Product Name",
                LocationId = location.Id,
                QuantityUnitIdPurchase = unit.Id,
                QuantityUnitIdStock = unit.Id,
            };
            db.Products.Add(product);

            db.ProductAllergens.Add(new ProductAllergen
            {
                Id = Guid.NewGuid(), ProductId = product.Id, AllergenType = Famick.HomeManagement.Domain.Enums.AllergenType.Peanuts,
            });
        }

        await db.SaveChangesAsync();
        return (mine, theirs);
    }

    private static ArchiveManifest ReadManifest(byte[] archive)
    {
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("manifest.json")!.Open();
        return JsonSerializer.Deserialize<ArchiveManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static List<Dictionary<string, JsonElement>> ReadTable(byte[] archive, string entityName)
    {
        var manifest = ReadManifest(archive);
        var table = manifest.Tables.SingleOrDefault(t => t.Entity == entityName);
        if (table == null) return [];

        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        return ReadJsonLines(zip, table.File);
    }

    private static IEnumerable<(string Entity, List<Dictionary<string, JsonElement>> Rows)> ReadAllTables(byte[] archive)
    {
        var manifest = ReadManifest(archive);
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);

        foreach (var table in manifest.Tables)
            yield return (table.Entity, ReadJsonLines(zip, table.File));
    }

    private static List<Dictionary<string, JsonElement>> ReadJsonLines(ZipArchive zip, string path)
    {
        var rows = new List<Dictionary<string, JsonElement>>();
        using var reader = new StreamReader(zip.GetEntry(path)!.Open());

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            rows.Add(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)!);
        }

        return rows;
    }

    #endregion
}
