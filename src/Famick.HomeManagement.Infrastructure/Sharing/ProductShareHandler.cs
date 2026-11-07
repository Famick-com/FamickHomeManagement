using Famick.HomeManagement.Core.DTOs.Sharing;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Sharing;

/// <summary>
/// Shares a product's description: names, units, barcodes, nutrition, allergens and pictures. Nothing
/// about the sharer's stock, locations or stores goes with it.
/// </summary>
public class ProductShareHandler : IShareableItemHandler
{
    private readonly HomeManagementDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IProductSearchService _searchService;

    public ProductShareHandler(HomeManagementDbContext context, IFileStorageService storage, IProductSearchService searchService)
    {
        _context = context;
        _storage = storage;
        _searchService = searchService;
    }

    public ShareableKind Kind => ShareableKind.Product;

    public async Task<SharedItemSnapshot?> SnapshotAsync(Guid itemId, ShareSnapshotContext context, CancellationToken ct) =>
        await SnapshotProductAsync(itemId, context, ct);

    public async Task<SharedCopy> ImportAsync(SharedItemSnapshot snapshot, ShareImportContext context, CancellationToken ct)
    {
        var copy = await ImportProductAsync((ProductSnapshot)snapshot, context, ct);
        _searchService.InvalidateCache();
        return copy;
    }

    public SharedItemPreview Preview(SharedItemSnapshot snapshot, string sharedByHouseholdName, bool hasImage) => new()
    {
        Kind = Kind,
        Name = snapshot.Name,
        SharedByHouseholdName = sharedByHouseholdName,
        HasImage = hasImage
    };

    internal async Task<ProductSnapshot?> SnapshotProductAsync(Guid productId, ShareSnapshotContext context, CancellationToken ct)
    {
        var product = await _context.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Barcodes)
            .Include(p => p.Images)
            .Include(p => p.Nutrition)
            .Include(p => p.Allergens)
            .Include(p => p.DietaryConflicts)
            .Include(p => p.ProductGroup)
            .Include(p => p.QuantityUnitPurchase)
            .Include(p => p.QuantityUnitStock)
            .Include(p => p.MasterProduct)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, ct);
        if (product == null)
            return null;

        context.CountEntities(1 + product.Barcodes.Count + product.Images.Count + product.Allergens.Count
                             + product.DietaryConflicts.Count + (product.Nutrition == null ? 0 : 1));

        var images = new List<SharedImageSnapshot>();
        foreach (var image in product.Images.OrderBy(i => i.SortOrder))
        {
            if (string.IsNullOrEmpty(image.FileName))
            {
                // A picture a lookup found and never stored: only its address goes.
                if (image.ExternalUrl != null || image.ExternalThumbnailUrl != null)
                    images.Add(new SharedImageSnapshot(null, image.ExternalUrl, image.ExternalThumbnailUrl, image.IsPrimary));
                continue;
            }

            var key = await context.AddImageAsync(c => _storage.GetProductImageStreamAsync(product.Id, image.FileName, c), ct);
            if (key != null)
                images.Add(new SharedImageSnapshot(key, image.ExternalUrl, image.ExternalThumbnailUrl, image.IsPrimary));
        }

        // Only a published catalog product travels: one still in review is the sharer's own contribution.
        var published = product.MasterProduct is { ReviewStatus: MasterProductReviewStatus.Approved };

        return new ProductSnapshot
        {
            SourceId = product.Id,
            Name = product.Name,
            Description = product.Description,
            Brand = product.Brand,
            SaleType = product.SaleType,
            DefaultBestBeforeDays = product.DefaultBestBeforeDays,
            TracksBestBeforeDate = product.TracksBestBeforeDate,
            ExpiryWarningDays = product.ExpiryWarningDays,
            ServingSize = product.ServingSize,
            ServingUnit = product.ServingUnit,
            ServingsPerContainer = product.ServingsPerContainer,
            DataSourceAttribution = product.DataSourceAttribution,
            MasterProductId = published ? product.MasterProductId : null,
            OverriddenFields = published ? product.OverriddenFields : null,
            GroupName = product.ProductGroup?.Name,
            PurchaseUnit = Unit(product.QuantityUnitPurchase),
            StockUnit = Unit(product.QuantityUnitStock),
            PurchaseToStockFactor = product.QuantityUnitFactorPurchaseToStock,
            Barcodes = product.Barcodes.Select(b => new SharedBarcode(b.Barcode, b.Note, b.Type2Prefix)).ToList(),
            Nutrition = product.Nutrition,
            Allergens = product.Allergens.Select(a => a.AllergenType).ToList(),
            DietaryConflicts = product.DietaryConflicts.Select(d => d.DietaryPreference).ToList(),
            Images = images
        };
    }

    private static SharedUnit Unit(QuantityUnit unit) => new(unit.Name, unit.NamePlural, unit.StandardUnit);

    /// <summary>
    /// The household's copy of the product: the one it already has from this origin or for the same
    /// catalog product, else a new one, saved before it returns.
    /// </summary>
    internal async Task<SharedCopy> ImportProductAsync(ProductSnapshot shared, ShareImportContext context, CancellationToken ct)
    {
        var originKey = context.Origin.KeyFor(ShareableKind.Product, shared.SourceId);
        var existing = await _context.Products
            .Where(p => p.IsActive && p.OriginKey == originKey)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(ct);
        if (existing != null)
            return new SharedCopy(existing.Id, existing.Name, AlreadyHad: true);

        // The catalog product, if this household can see it. One it already follows is the same product.
        var masterId = shared.MasterProductId is { } id
                       && await _context.MasterProducts.AnyAsync(m => m.Id == id && m.ReviewStatus == MasterProductReviewStatus.Approved, ct)
            ? id
            : (Guid?)null;
        if (masterId != null)
        {
            var following = await _context.Products
                .Where(p => p.IsActive && p.MasterProductId == masterId)
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync(ct);
            if (following != null)
                return new SharedCopy(following.Id, following.Name, AlreadyHad: true);
        }

        var tenantId = _context.EffectiveTenantId
            ?? throw new InvalidOperationException("A shared product is imported in a household's scope");

        var locations = await _context.Locations.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync(ct);
        var location = locations.FirstOrDefault(l => l.Name == "Pantry") ?? locations.FirstOrDefault()
            ?? throw new DomainException("Add a location before adding shared products");

        var units = await _context.QuantityUnits.Where(u => u.IsActive).OrderBy(u => u.Name).ToListAsync(ct);
        var purchaseUnit = MatchUnit(shared.PurchaseUnit, units);
        var stockUnit = MatchUnit(shared.StockUnit, units);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = await FreeNameAsync(ShareText.Required(shared.Name, 200, "Shared product"), context.Origin.SharedByHouseholdName, ct),
            Description = ShareText.Clean(shared.Description, 2000),
            Brand = ShareText.Clean(shared.Brand, 200),
            SaleType = shared.SaleType,
            DefaultBestBeforeDays = shared.DefaultBestBeforeDays,
            TracksBestBeforeDate = shared.TracksBestBeforeDate,
            ExpiryWarningDays = shared.ExpiryWarningDays,
            ServingSize = shared.ServingSize,
            ServingUnit = ShareText.Clean(shared.ServingUnit, 50),
            ServingsPerContainer = shared.ServingsPerContainer,
            DataSourceAttribution = ShareText.Clean(shared.DataSourceAttribution, 500),
            MasterProductId = masterId,
            OverriddenFields = masterId != null ? shared.OverriddenFields ?? "[]" : "[]",
            LocationId = location.Id,
            QuantityUnitIdPurchase = purchaseUnit.Id,
            QuantityUnitIdStock = stockUnit.Id,
            // The factor relates the sharer's two units; it holds only if both came across as themselves.
            QuantityUnitFactorPurchaseToStock = purchaseUnit.Id == stockUnit.Id ? 1m : Math.Max(shared.PurchaseToStockFactor, 0.0001m),
            ProductGroupId = shared.GroupName is { } groupName ? (await GroupAsync(groupName, tenantId, ct))?.Id : null,
            OriginKey = originKey,
            SharedByHouseholdName = context.Origin.SharedByHouseholdName
        };

        // A barcode is one product's in a household, so one this household already uses stays with it.
        var codes = shared.Barcodes.Select(b => b.Barcode).ToList();
        var taken = (await _context.ProductBarcodes.Where(b => codes.Contains(b.Barcode)).Select(b => b.Barcode).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var barcode in shared.Barcodes)
        {
            var code = ShareText.Clean(barcode.Barcode, 200);
            if (string.IsNullOrWhiteSpace(code) || !taken.Add(code))
                continue;
            product.Barcodes.Add(new ProductBarcode
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id, Barcode = code,
                Note = ShareText.Clean(barcode.Note, 500), Type2Prefix = ShareText.Clean(barcode.Type2Prefix, 2)
            });
        }

        foreach (var allergen in shared.Allergens.Distinct())
            product.Allergens.Add(new ProductAllergen { Id = Guid.NewGuid(), ProductId = product.Id, AllergenType = allergen });
        foreach (var conflict in shared.DietaryConflicts.Distinct())
            product.DietaryConflicts.Add(new ProductDietaryConflict { Id = Guid.NewGuid(), ProductId = product.Id, DietaryPreference = conflict });

        await AddImagesAsync(product, shared.Images, context, ct);

        _context.Products.Add(product);
        if (shared.Nutrition != null)
            AddNutrition(product, shared.Nutrition, tenantId);

        await _context.SaveChangesAsync(ct);
        return new SharedCopy(product.Id, product.Name, AlreadyHad: false);
    }

    /// <summary>
    /// The product's name, unless the household already has a product by that name: then the copy says
    /// where it came from, "Granola (from The Smiths)", so both stay recognisable.
    /// </summary>
    private async Task<string> FreeNameAsync(string name, string householdName, CancellationToken ct)
    {
        const int maxLength = 200;
        for (var attempt = 1; ; attempt++)
        {
            var suffix = attempt switch
            {
                1 => string.Empty,
                2 => $" (from {householdName})",
                _ => $" (from {householdName}) ({attempt - 1})"
            };
            var candidate = (name.Length + suffix.Length > maxLength ? name[..(maxLength - suffix.Length)].TrimEnd() : name) + suffix;
            var lower = candidate.ToLower();
            if (!await _context.Products.AnyAsync(p => p.IsActive && p.Name.ToLower() == lower, ct))
                return candidate;
        }
    }

    /// <summary>The household's own unit for the sharer's: the same standard measure, else the same name, else Piece.</summary>
    private static QuantityUnit MatchUnit(SharedUnit shared, List<QuantityUnit> units) =>
        (shared.StandardUnit is { } standard ? units.FirstOrDefault(u => u.StandardUnit == standard) : null)
        ?? units.FirstOrDefault(u => string.Equals(u.Name, shared.Name, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(u.NamePlural, shared.NamePlural, StringComparison.OrdinalIgnoreCase))
        ?? units.FirstOrDefault(u => u.Name == "Piece")
        ?? units.FirstOrDefault()
        ?? throw new DomainException("Add a quantity unit before adding shared products");

    private async Task<ProductGroup?> GroupAsync(string name, Guid tenantId, CancellationToken ct)
    {
        var cleaned = ShareText.Clean(name, 100)?.Trim();
        if (string.IsNullOrEmpty(cleaned))
            return null;

        var lower = cleaned.ToLower();
        var group = await _context.ProductGroups.FirstOrDefaultAsync(g => g.Name.ToLower() == lower, ct);
        if (group != null)
            return group;

        group = new ProductGroup { Id = Guid.NewGuid(), TenantId = tenantId, Name = cleaned };
        _context.ProductGroups.Add(group);
        return group;
    }

    private void AddNutrition(Product product, ProductNutrition source, Guid tenantId)
    {
        // Copied while the row is still detached: a tracked row's key can't change.
        var nutrition = new ProductNutrition();
        _context.Entry(nutrition).CurrentValues.SetValues(source);

        // Every value is the sharer's except what makes the row this household's.
        nutrition.Id = Guid.NewGuid();
        nutrition.TenantId = tenantId;
        nutrition.ProductId = product.Id;
        nutrition.CreatedAt = DateTime.UtcNow;
        nutrition.UpdatedAt = null;
        nutrition.ExternalId = ShareText.Clean(nutrition.ExternalId, 100);
        nutrition.DataSource = ShareText.Clean(nutrition.DataSource, 50) ?? string.Empty;
        nutrition.BrandOwner = ShareText.Clean(nutrition.BrandOwner, 200);
        nutrition.BrandName = ShareText.Clean(nutrition.BrandName, 200);
        nutrition.Ingredients = ShareText.Clean(nutrition.Ingredients, 4000);
        nutrition.ServingSizeDescription = ShareText.Clean(nutrition.ServingSizeDescription, 200);
        nutrition.ServingUnit = ShareText.Clean(nutrition.ServingUnit, 50);
        product.Nutrition = nutrition;
    }

    private async Task AddImagesAsync(Product product, List<SharedImageSnapshot> images, ShareImportContext context, CancellationToken ct)
    {
        var hasPrimary = false;
        foreach (var shared in images)
        {
            var externalUrl = ShareText.WebUrl(shared.ExternalUrl);
            var thumbnailUrl = ShareText.WebUrl(shared.ExternalThumbnailUrl);
            var image = new ProductImage
            {
                Id = Guid.NewGuid(),
                TenantId = product.TenantId,
                ProductId = product.Id,
                SortOrder = product.Images.Count,
                IsPrimary = !hasPrimary && shared.IsPrimary,
                ExternalUrl = externalUrl,
                ExternalThumbnailUrl = thumbnailUrl,
                ExternalSource = RecipeShareHandler.ImageExternalSource
            };

            if (context.Image(shared.Key) is { } bytes)
            {
                var fileName = $"product-image{bytes.Extension}";
                using var stream = new MemoryStream(bytes.Bytes, writable: false);
                var stored = await _storage.SaveProductImageAsync(product.Id, stream, fileName, ct);
                context.SavedFile(() => _storage.DeleteProductImageAsync(product.Id, stored));
                image.FileName = stored;
                image.OriginalFileName = fileName;
                image.ContentType = bytes.ContentType;
                image.FileSize = bytes.Bytes.Length;
            }
            else if (externalUrl != null || thumbnailUrl != null)
            {
                image.FileName = string.Empty;
                image.OriginalFileName = "Shared external image";
                image.ContentType = "image/jpeg";
            }
            else
            {
                continue;
            }

            hasPrimary |= image.IsPrimary;
            product.Images.Add(image);
        }

        if (!hasPrimary && product.Images.FirstOrDefault() is { } first)
            first.IsPrimary = true;
    }
}
