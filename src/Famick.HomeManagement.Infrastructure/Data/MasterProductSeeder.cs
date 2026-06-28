using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Data;

/// <summary>
/// Keeps the global master product catalog in sync with an embedded JSON seed
/// file (<c>product-templates.json</c>).
///
/// The seed file is authoritative for rows it owns — those with
/// <see cref="MasterProductSource.Seeded"/>. On startup the seeder hashes the
/// embedded file and compares it to the hash stored in <see cref="AppMetadata"/>
/// (key <c>MasterCatalogSeedHash</c>); it only does work when the file changed:
/// <list type="bullet">
///   <item>empty catalog → bulk insert + one-time tenant auto-link;</item>
///   <item>existing catalog → upsert matched by <see cref="MasterProduct.SeedKey"/>.</item>
/// </list>
/// Tenant-contributed and admin-created rows are never modified or deleted by the
/// seeder, and entries removed from the file are kept (logged, not deleted) to
/// avoid breaking tenant <c>Product.MasterProductId</c> references.
/// </summary>
public class MasterProductSeeder
{
    private const string SeedHashKey = "MasterCatalogSeedHash";

    private readonly HomeManagementDbContext _dbContext;
    private readonly ILogger<MasterProductSeeder> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public MasterProductSeeder(HomeManagementDbContext dbContext, ILogger<MasterProductSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var json = ReadEmbeddedResource();
        if (json == null)
        {
            _logger.LogWarning("Master products embedded resource not found, skipping seed");
            return;
        }

        var currentHash = ComputeSeedHash(json);
        var storedHash = await _dbContext.AppMetadata
            .Where(m => m.Key == SeedHashKey)
            .Select(m => m.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedHash == currentHash)
        {
            _logger.LogDebug("Master catalog seed unchanged (hash {Hash}), skipping", currentHash[..12]);
            return;
        }

        var seedDtos = JsonSerializer.Deserialize<List<MasterProductSeedDto>>(json, JsonOptions);
        if (seedDtos == null || seedDtos.Count == 0)
        {
            _logger.LogWarning("Master products JSON was empty or invalid, skipping seed");
            return;
        }

        var hasProducts = await _dbContext.MasterProducts
            .IgnoreQueryFilters()
            .AnyAsync(cancellationToken);

        if (!hasProducts)
        {
            await InitialSeedAsync(seedDtos, cancellationToken);
        }
        else
        {
            await UpsertSeededAsync(seedDtos, cancellationToken);
        }

        await SetSeedHashAsync(currentHash, cancellationToken);
    }

    /// <summary>
    /// First-time seed of an empty catalog: bulk insert every entry, then run the
    /// one-time auto-link pass that matches existing tenant products to master.
    /// </summary>
    private async Task InitialSeedAsync(List<MasterProductSeedDto> seedDtos, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Seeding master products from embedded resource...");

        var masterProducts = seedDtos.Select(MapToNewMasterProduct).ToList();

        _dbContext.MasterProducts.AddRange(masterProducts);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} master products", masterProducts.Count);

        // Run one-time auto-link of existing tenant products
        await AutoLinkExistingProductsAsync(masterProducts, cancellationToken);
    }

    /// <summary>
    /// Reconciles an already-populated catalog with the seed file. Matches by
    /// <see cref="MasterProduct.SeedKey"/> so renames keep the row's identity (and
    /// all tenant links). Only <see cref="MasterProductSource.Seeded"/> rows are
    /// touched; only seed-owned fields are overwritten (enrichment such as
    /// description / nutrition / barcodes is preserved). Rows whose seed key is no
    /// longer in the file are kept and logged, never deleted.
    /// </summary>
    public async Task UpsertSeededAsync(List<MasterProductSeedDto> seedDtos, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.MasterProducts
            .IgnoreQueryFilters()
            .Where(mp => mp.Source == MasterProductSource.Seeded && mp.SeedKey != null)
            .ToListAsync(cancellationToken);

        var byKey = existing.ToDictionary(mp => mp.SeedKey!, StringComparer.Ordinal);

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var inserted = 0;
        var updated = 0;

        foreach (var dto in seedDtos)
        {
            var key = ResolveSeedKey(dto);
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Seed entry '{Name}' has no resolvable seed key, skipping", dto.Name);
                continue;
            }

            if (!seenKeys.Add(key))
            {
                _logger.LogWarning("Duplicate seed key '{Key}' in seed file, skipping later occurrence", key);
                continue;
            }

            if (byKey.TryGetValue(key, out var existingProduct))
            {
                ApplySeedFields(existingProduct, dto);
                existingProduct.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
            else
            {
                var product = MapToNewMasterProduct(dto);
                product.SeedKey = key;
                _dbContext.MasterProducts.Add(product);
                inserted++;
            }
        }

        var orphaned = byKey.Keys.Count(k => !seenKeys.Contains(k));
        if (orphaned > 0)
        {
            _logger.LogInformation(
                "{Count} seeded master products are no longer in the seed file (kept, not deleted)", orphaned);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Master catalog upsert complete: {Inserted} inserted, {Updated} updated", inserted, updated);
    }

    private static MasterProduct MapToNewMasterProduct(MasterProductSeedDto dto)
    {
        var product = new MasterProduct
        {
            Id = Guid.NewGuid(),
            SeedKey = ResolveSeedKey(dto),
            Source = MasterProductSource.Seeded
        };
        ApplySeedFields(product, dto);
        return product;
    }

    /// <summary>
    /// Copies the seed-owned fields from a seed entry onto a master product.
    /// Deliberately excludes enrichment fields (Description, ServingSize, nutrition,
    /// barcodes) so tenant/admin-contributed data on a seeded row survives an upsert.
    /// </summary>
    private static void ApplySeedFields(MasterProduct product, MasterProductSeedDto dto)
    {
        product.Name = dto.Name;
        product.Category = dto.Category;
        product.ContainerType = dto.ContainerType;
        product.GramsPerTbsp = dto.GramsPerTbsp;
        product.IconSvg = dto.IconSvg;
        product.IsStaple = dto.IsStaple;
        product.Popularity = dto.Popularity;
        product.LifestyleTags = JsonSerializer.Serialize(dto.LifestyleTags ?? []);
        product.AllergenFlags = JsonSerializer.Serialize(dto.AllergenFlags ?? []);
        product.DietaryConflictFlags = JsonSerializer.Serialize(dto.DietaryConflictFlags ?? []);
        product.OrganicScore = dto.OrganicScore;
        product.ConvenienceScore = dto.ConvenienceScore;
        product.HealthScore = dto.HealthScore;
        product.DefaultLocationHint = dto.DefaultLocationHint;
        product.DefaultQuantityUnitHint = dto.DefaultQuantityUnitHint;
        product.ImageSlug = dto.ImageSlug;
    }

    private static string ResolveSeedKey(MasterProductSeedDto dto) =>
        !string.IsNullOrWhiteSpace(dto.SeedKey) ? dto.SeedKey!
        : !string.IsNullOrWhiteSpace(dto.ImageSlug) ? dto.ImageSlug!
        : Slugify(dto.Name);

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-");
        return slug.Trim('-');
    }

    public static string ComputeSeedHash(string json) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    private async Task SetSeedHashAsync(string hash, CancellationToken cancellationToken)
    {
        var row = await _dbContext.AppMetadata
            .FirstOrDefaultAsync(m => m.Key == SeedHashKey, cancellationToken);

        if (row == null)
        {
            _dbContext.AppMetadata.Add(new AppMetadata
            {
                Id = Guid.NewGuid(),
                Key = SeedHashKey,
                Value = hash
            });
        }
        else
        {
            row.Value = hash;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// One-time pass: links existing tenant products to master products by name or barcode.
    /// For matches, copies tenant enrichments back to master (last edited wins).
    /// </summary>
    private async Task AutoLinkExistingProductsAsync(
        List<MasterProduct> masterProducts, CancellationToken ct)
    {
        var tenantProducts = await _dbContext.Products
            .IgnoreQueryFilters()
            .Include(p => p.Barcodes)
            .Include(p => p.Nutrition)
            .Where(p => p.MasterProductId == null && p.IsActive)
            .ToListAsync(ct);

        if (tenantProducts.Count == 0)
        {
            _logger.LogDebug("No existing tenant products to auto-link");
            return;
        }

        _logger.LogInformation("Auto-linking {Count} existing tenant products to master catalog...", tenantProducts.Count);

        // Build lookup indexes
        var masterByName = masterProducts
            .ToDictionary(mp => mp.Name.ToLowerInvariant(), mp => mp);

        // Also index master products by barcode (from any MasterProductBarcodes already in DB)
        var masterBarcodes = await _dbContext.MasterProductBarcodes
            .IgnoreQueryFilters()
            .ToDictionaryAsync(b => b.Barcode, b => b.MasterProductId, ct);

        var linked = 0;
        var enriched = 0;

        foreach (var product in tenantProducts)
        {
            MasterProduct? match = null;

            // Try barcode match first (stronger signal)
            if (product.Barcodes.Count > 0)
            {
                foreach (var barcode in product.Barcodes)
                {
                    if (masterBarcodes.TryGetValue(barcode.Barcode, out var masterProductId))
                    {
                        match = masterProducts.FirstOrDefault(mp => mp.Id == masterProductId);
                        if (match != null) break;
                    }
                }
            }

            // Fall back to name match
            match ??= masterByName.GetValueOrDefault(product.Name.ToLowerInvariant());

            if (match == null) continue;

            // Link the tenant product
            product.MasterProductId = match.Id;
            product.OverriddenFields = "[]";

            // Merge tenant enrichments back to master (last edited wins)
            var tenantUpdated = product.UpdatedAt ?? product.CreatedAt;
            var masterUpdated = match.UpdatedAt ?? match.CreatedAt;

            if (tenantUpdated > masterUpdated)
            {
                // Tenant has more recent data — enrich master
                if (!string.IsNullOrEmpty(product.Description) && string.IsNullOrEmpty(match.Description))
                    match.Description = product.Description;

                if (product.DefaultBestBeforeDays > 0 && match.DefaultBestBeforeDays == 0)
                    match.DefaultBestBeforeDays = product.DefaultBestBeforeDays;

                if (product.ServingSize.HasValue && !match.ServingSize.HasValue)
                    match.ServingSize = product.ServingSize;

                if (!string.IsNullOrEmpty(product.ServingUnit) && string.IsNullOrEmpty(match.ServingUnit))
                    match.ServingUnit = product.ServingUnit;

                if (product.ServingsPerContainer.HasValue && !match.ServingsPerContainer.HasValue)
                    match.ServingsPerContainer = product.ServingsPerContainer;

                if (!string.IsNullOrEmpty(product.DataSourceAttribution) && string.IsNullOrEmpty(match.DataSourceAttribution))
                    match.DataSourceAttribution = product.DataSourceAttribution;

                enriched++;
            }

            // Promote tenant barcodes to master (barcodes are universal)
            foreach (var barcode in product.Barcodes)
            {
                if (!masterBarcodes.ContainsKey(barcode.Barcode))
                {
                    var masterBarcode = new MasterProductBarcode
                    {
                        Id = Guid.NewGuid(),
                        MasterProductId = match.Id,
                        Barcode = barcode.Barcode,
                        Note = barcode.Note
                    };
                    _dbContext.MasterProductBarcodes.Add(masterBarcode);
                    masterBarcodes[barcode.Barcode] = match.Id;
                }
            }

            // Promote nutrition to master if tenant has it and master doesn't
            if (product.Nutrition != null && match.Nutrition == null)
            {
                var masterNutrition = new MasterProductNutrition
                {
                    Id = Guid.NewGuid(),
                    MasterProductId = match.Id,
                    ExternalId = product.Nutrition.ExternalId,
                    DataSource = product.Nutrition.DataSource,
                    ServingSize = product.Nutrition.ServingSize,
                    ServingUnit = product.Nutrition.ServingUnit,
                    ServingsPerContainer = product.Nutrition.ServingsPerContainer,
                    Calories = product.Nutrition.Calories,
                    TotalFat = product.Nutrition.TotalFat,
                    SaturatedFat = product.Nutrition.SaturatedFat,
                    TransFat = product.Nutrition.TransFat,
                    Cholesterol = product.Nutrition.Cholesterol,
                    Sodium = product.Nutrition.Sodium,
                    TotalCarbohydrates = product.Nutrition.TotalCarbohydrates,
                    DietaryFiber = product.Nutrition.DietaryFiber,
                    TotalSugars = product.Nutrition.TotalSugars,
                    AddedSugars = product.Nutrition.AddedSugars,
                    Protein = product.Nutrition.Protein,
                    VitaminA = product.Nutrition.VitaminA,
                    VitaminC = product.Nutrition.VitaminC,
                    VitaminD = product.Nutrition.VitaminD,
                    VitaminE = product.Nutrition.VitaminE,
                    VitaminK = product.Nutrition.VitaminK,
                    Thiamin = product.Nutrition.Thiamin,
                    Riboflavin = product.Nutrition.Riboflavin,
                    Niacin = product.Nutrition.Niacin,
                    VitaminB6 = product.Nutrition.VitaminB6,
                    Folate = product.Nutrition.Folate,
                    VitaminB12 = product.Nutrition.VitaminB12,
                    Calcium = product.Nutrition.Calcium,
                    Iron = product.Nutrition.Iron,
                    Magnesium = product.Nutrition.Magnesium,
                    Phosphorus = product.Nutrition.Phosphorus,
                    Potassium = product.Nutrition.Potassium,
                    Zinc = product.Nutrition.Zinc,
                    BrandOwner = product.Nutrition.BrandOwner,
                    BrandName = product.Nutrition.BrandName,
                    Ingredients = product.Nutrition.Ingredients,
                    ServingSizeDescription = product.Nutrition.ServingSizeDescription,
                    LastUpdatedFromSource = product.Nutrition.LastUpdatedFromSource
                };
                _dbContext.MasterProductNutrition.Add(masterNutrition);
            }

            // If product is generic (no brand) and has a generic parent, flag for master hierarchy
            if (product.Brand == null && product.ParentProductId.HasValue)
            {
                var parent = tenantProducts.FirstOrDefault(p => p.Id == product.ParentProductId);
                if (parent?.Brand == null && parent?.MasterProductId != null)
                {
                    // Both parent and child are generic and linked to master — set hierarchy
                    match.ParentMasterProductId = parent.MasterProductId;
                }
            }

            linked++;
        }

        if (linked > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Auto-link complete: {Linked} products linked, {Enriched} master products enriched from tenant data",
            linked, enriched);
    }

    private static string? ReadEmbeddedResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("product-templates.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// DTO matching the <c>product-templates.json</c> seed file structure.
/// </summary>
public sealed class MasterProductSeedDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? ContainerType { get; set; }
    public decimal? GramsPerTbsp { get; set; }
    public string? IconSvg { get; set; }
    public bool IsStaple { get; set; }
    public int Popularity { get; set; } = 3;
    public List<string>? LifestyleTags { get; set; }
    public List<string>? AllergenFlags { get; set; }
    public List<string>? DietaryConflictFlags { get; set; }
    public string? DefaultLocationHint { get; set; }
    public string? DefaultQuantityUnitHint { get; set; }
    public int OrganicScore { get; set; } = 3;
    public int ConvenienceScore { get; set; } = 3;
    public int HealthScore { get; set; } = 3;
    public string? ImageSlug { get; set; }

    /// <summary>Stable identity used to match this entry to its master product row.</summary>
    public string? SeedKey { get; set; }
}
