using System.Reflection;
using System.Text.Json;
using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Data;

/// <summary>
/// Seeds the global master product catalog from an embedded JSON resource.
/// After seeding, runs a one-time auto-link pass to match existing tenant products
/// to master products by name or barcode.
/// </summary>
public class MasterProductSeeder
{
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
        var hasProducts = await _dbContext.MasterProducts
            .IgnoreQueryFilters()
            .AnyAsync(cancellationToken);

        if (hasProducts)
        {
            _logger.LogDebug("Master products already seeded, skipping");
            return;
        }

        _logger.LogInformation("Seeding master products from embedded resource...");

        var json = ReadEmbeddedResource();
        if (json == null)
        {
            _logger.LogWarning("Master products embedded resource not found, skipping seed");
            return;
        }

        var seedDtos = JsonSerializer.Deserialize<List<MasterProductSeedDto>>(json, JsonOptions);
        if (seedDtos == null || seedDtos.Count == 0)
        {
            _logger.LogWarning("Master products JSON was empty or invalid, skipping seed");
            return;
        }

        var masterProducts = seedDtos.Select(dto => new MasterProduct
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Category = dto.Category,
            ContainerType = dto.ContainerType,
            GramsPerTbsp = dto.GramsPerTbsp,
            IconSvg = dto.IconSvg,
            IsStaple = dto.IsStaple,
            Popularity = dto.Popularity,
            LifestyleTags = JsonSerializer.Serialize(dto.LifestyleTags ?? []),
            AllergenFlags = JsonSerializer.Serialize(dto.AllergenFlags ?? []),
            DietaryConflictFlags = JsonSerializer.Serialize(dto.DietaryConflictFlags ?? []),
            CookingStyleTags = JsonSerializer.Serialize(dto.CookingStyleTags ?? []),
            DefaultLocationHint = dto.DefaultLocationHint,
            DefaultQuantityUnitHint = dto.DefaultQuantityUnitHint
        }).ToList();

        _dbContext.MasterProducts.AddRange(masterProducts);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} master products", masterProducts.Count);

        // Run one-time auto-link of existing tenant products
        await AutoLinkExistingProductsAsync(masterProducts, cancellationToken);
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

    /// <summary>
    /// DTO matching the JSON seed file structure.
    /// </summary>
    private sealed class MasterProductSeedDto
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
        public List<string>? CookingStyleTags { get; set; }
        public string? DefaultLocationHint { get; set; }
        public string? DefaultQuantityUnitHint { get; set; }
    }
}
