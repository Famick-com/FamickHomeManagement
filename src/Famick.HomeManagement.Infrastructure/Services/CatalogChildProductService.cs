using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Plugin.Abstractions;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.Shared.Barcodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Resolves scans of master catalog child barcodes to the household's copy of the generic product,
/// and creates the scanned product as a child of it. See <see cref="ICatalogChildProductService"/>.
/// </summary>
public class CatalogChildProductService : ICatalogChildProductService
{
    /// <summary>
    /// The most the lookup plugins may take to fill in a new child. It runs in the background, so this
    /// only bounds how long a slow plugin can keep the work alive.
    /// </summary>
    public static readonly TimeSpan LookupLimit = TimeSpan.FromSeconds(30);

    private readonly HomeManagementDbContext _context;
    private readonly IProductLookupService _lookupService;
    private readonly IProductSearchService _searchService;
    private readonly ICatalogChildEnrichmentRunner _enrichmentRunner;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CatalogChildProductService> _logger;

    public CatalogChildProductService(
        HomeManagementDbContext context,
        IProductLookupService lookupService,
        IProductSearchService searchService,
        ICatalogChildEnrichmentRunner enrichmentRunner,
        ITenantProvider tenantProvider,
        ILogger<CatalogChildProductService> logger)
    {
        _context = context;
        _lookupService = lookupService;
        _searchService = searchService;
        _enrichmentRunner = enrichmentRunner;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<CatalogChildMatch?> FindAsync(string barcode, CancellationToken ct = default)
    {
        var scan = ScannedBarcode.From(barcode);
        if (scan == null)
            return null;

        // The household's own barcodes come first. Any match other than a catalog child barcode
        // copied onto a generic product means the household already knows this barcode.
        var householdMatches = await MatchHouseholdBarcodesAsync(scan, ct);
        if (householdMatches.Any(m => !m.IsCatalogChild))
            return null;

        var copied = householdMatches.FirstOrDefault();
        if (copied != null)
        {
            var parent = await _context.Products
                .Where(p => p.Id == copied.ProductId && p.ParentProductId == null)
                .Select(p => new { p.Id, p.Name, p.MasterProductId, MasterName = p.MasterProduct != null ? p.MasterProduct.Name : null })
                .FirstOrDefaultAsync(ct);

            if (parent?.MasterProductId != null)
                return new CatalogChildMatch(barcode, parent.MasterProductId.Value, parent.MasterName ?? parent.Name,
                    copied.Note, parent.Id);

            // A copied child barcode whose product lost its catalog link is just one of its barcodes.
            return null;
        }

        var catalogMatch = await MatchCatalogChildBarcodeAsync(scan, ct);
        if (catalogMatch == null)
            return null;

        // The household's copy of the generic product: the oldest active one that can take children.
        var parentId = await _context.Products
            .Where(p => p.MasterProductId == catalogMatch.MasterProductId && p.IsActive && p.ParentProductId == null)
            .OrderBy(p => p.CreatedAt)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        return catalogMatch with { Barcode = barcode, ParentProductId = parentId };
    }

    public async Task<Product> CreateChildAsync(
        Guid parentProductId,
        string barcode,
        string? label,
        Guid? childProductId = null,
        ProductLookupLocation? location = null,
        CancellationToken ct = default)
    {
        var scan = ScannedBarcode.From(barcode)
            ?? throw new DomainException($"'{barcode}' is not a product barcode");

        // A scan that got here first already created the child.
        var owner = await FindHouseholdOwnerAsync(scan, ct);
        if (owner != null)
            return owner;

        var parent = await _context.Products.FirstOrDefaultAsync(p => p.Id == parentProductId, ct)
            ?? throw new EntityNotFoundException(nameof(Product), parentProductId);
        await ProductHierarchy.EnsureValidParentAsync(_context, null, parent.Id, ct);

        var name = await UniqueNameAsync(
            string.IsNullOrWhiteSpace(label) ? $"{parent.Name} ({barcode})" : label.Trim(), null, barcode, ct);

        // The child is stocked and bought like its parent until someone says otherwise.
        var child = new Product
        {
            Id = childProductId ?? Guid.NewGuid(),
            Name = name,
            ParentProductId = parent.Id,
            LocationId = parent.LocationId,
            QuantityUnitIdPurchase = parent.QuantityUnitIdPurchase,
            QuantityUnitIdStock = parent.QuantityUnitIdStock,
            QuantityUnitFactorPurchaseToStock = parent.QuantityUnitFactorPurchaseToStock,
            TracksBestBeforeDate = parent.TracksBestBeforeDate,
            DefaultBestBeforeDays = parent.DefaultBestBeforeDays,
            ExpiryWarningDays = parent.ExpiryWarningDays,
            ProductGroupId = parent.ProductGroupId,
            SaleType = parent.SaleType,
            IsActive = true
        };
        _context.Products.Add(child);

        // A child barcode copied onto the parent moves to the child; otherwise the scan is attached.
        var copied = (await _context.ProductBarcodes
                .Where(pb => pb.ProductId == parent.Id && pb.IsCatalogChild)
                .ToListAsync(ct))
            .Where(pb => scan.Matches(pb.Barcode))
            .ToList();

        ProductBarcode? added = null;
        if (copied.Count > 0)
        {
            foreach (var row in copied)
            {
                row.ProductId = child.Id;
                row.IsCatalogChild = false;
            }
        }
        else
        {
            added = new ProductBarcode
            {
                Id = Guid.NewGuid(),
                ProductId = child.Id,
                Barcode = barcode,
                Note = label
            };
            _context.ProductBarcodes.Add(added);
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Two scans of the same barcode raced; the barcode is unique per household.
            _context.Entry(child).State = EntityState.Detached;
            if (added != null)
                _context.Entry(added).State = EntityState.Detached;
            foreach (var row in copied)
                await _context.Entry(row).ReloadAsync(ct);

            owner = await FindHouseholdOwnerAsync(scan, ct);
            if (owner == null)
                throw;

            _logger.LogInformation(ex, "Barcode {Barcode} was claimed by product {ProductId} meanwhile", barcode, owner.Id);
            return owner;
        }

        _searchService.InvalidateCache();
        _logger.LogInformation("Created child product {ProductId} '{Name}' under {ParentId} for barcode {Barcode}",
            child.Id, child.Name, parent.Id, barcode);

        if (_tenantProvider.TenantId is { } tenantId)
            _enrichmentRunner.Start(tenantId, child.Id, barcode, child.Name, location);

        return child;
    }

    public async Task EnrichAsync(
        Guid productId,
        string barcode,
        string provisionalName,
        ProductLookupLocation? location = null,
        CancellationToken ct = default)
    {
        List<ProductLookupResult> results;
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(LookupLimit);
            results = await _lookupService.SearchAsync(barcode, 5, ProductSearchMode.ExternalSourcesOnly, location, limit.Token);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Lookup for barcode {Barcode} failed; product {ProductId} keeps its name", barcode, productId);
            return;
        }

        var best = PickResult(results, barcode);
        if (best == null)
        {
            _logger.LogInformation("No lookup plugin knows barcode {Barcode}; product {ProductId} keeps its name", barcode, productId);
            return;
        }

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product == null || product.Name != provisionalName)
            return;

        if (!string.IsNullOrWhiteSpace(best.Name))
            product.Name = await UniqueNameAsync(best.Name.Trim(), productId, barcode, ct);
        if (!string.IsNullOrWhiteSpace(best.BrandName))
            product.Brand = best.BrandName.Trim();
        if (string.IsNullOrWhiteSpace(product.Description) && !string.IsNullOrWhiteSpace(best.Description))
            product.Description = best.Description.Trim();
        await _context.SaveChangesAsync(ct);

        // Nutrition, image, attribution and the plugin's own form of the barcode.
        try
        {
            await _lookupService.ApplyLookupResultAsync(productId, best, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not apply lookup details to product {ProductId}", productId);
        }

        _searchService.InvalidateCache();
    }

    /// <summary>
    /// The pipeline's top result, preferring one that carries the scanned barcode: a barcode search
    /// can still bring back near matches.
    /// </summary>
    public static ProductLookupResult? PickResult(IReadOnlyList<ProductLookupResult> results, string barcode)
    {
        if (results.Count == 0)
            return null;

        var scan = ScannedBarcode.From(barcode);
        return results.FirstOrDefault(r => scan != null && r.Barcodes.Any(b => scan.Matches(b.Data)))
            ?? results[0];
    }

    /// <summary>A name no other active product has, falling back to one qualified by the barcode.</summary>
    private async Task<string> UniqueNameAsync(string name, Guid? productId, string barcode, CancellationToken ct)
    {
        var taken = await _context.Products
            .AnyAsync(p => p.IsActive && p.Id != productId && p.Name == name, ct);
        return taken && !name.Contains(barcode) ? $"{name} ({barcode})" : name;
    }

    private async Task<Product?> FindHouseholdOwnerAsync(ScannedBarcode scan, CancellationToken ct)
    {
        var ownerId = (await MatchHouseholdBarcodesAsync(scan, ct))
            .Where(m => !m.IsCatalogChild)
            .Select(m => (Guid?)m.ProductId)
            .FirstOrDefault();

        return ownerId == null ? null : await _context.Products.FirstOrDefaultAsync(p => p.Id == ownerId, ct);
    }

    private sealed record HouseholdBarcode(Guid ProductId, string Barcode, bool IsCatalogChild, string? Note);

    /// <summary>
    /// The household's barcode rows for the scan: the scan's own variants, then any row whose parsed
    /// form is the same barcode (a 13-digit EAN stored for a 12-digit UPC, say), as products/by-barcode
    /// matches them.
    /// </summary>
    private async Task<List<HouseholdBarcode>> MatchHouseholdBarcodesAsync(ScannedBarcode scan, CancellationToken ct)
    {
        var rows = await _context.ProductBarcodes
            .Where(pb => scan.Variants.Contains(pb.Barcode))
            .Select(pb => new HouseholdBarcode(pb.ProductId, pb.Barcode, pb.IsCatalogChild, pb.Note))
            .ToListAsync(ct);
        if (rows.Count > 0 || !scan.IsParsed)
            return rows;

        // The substring predicate can't use the barcode index, so it only runs on a miss.
        rows = await _context.ProductBarcodes
            .Where(pb => pb.Barcode.Contains(scan.CoreData))
            .Select(pb => new HouseholdBarcode(pb.ProductId, pb.Barcode, pb.IsCatalogChild, pb.Note))
            .ToListAsync(ct);
        return rows.Where(r => scan.Matches(r.Barcode)).ToList();
    }

    private async Task<CatalogChildMatch?> MatchCatalogChildBarcodeAsync(ScannedBarcode scan, CancellationToken ct)
    {
        var childBarcodes = _context.MasterProductBarcodes.Where(b => b.IsChildCandidate);
        var rows = await childBarcodes
            .Where(b => scan.Variants.Contains(b.Barcode))
            .Select(b => new { b.Barcode, b.MasterProductId, MasterName = b.MasterProduct.Name, b.Note })
            .ToListAsync(ct);
        if (rows.Count == 0 && scan.IsParsed)
        {
            rows = await childBarcodes
                .Where(b => b.Barcode.Contains(scan.CoreData))
                .Select(b => new { b.Barcode, b.MasterProductId, MasterName = b.MasterProduct.Name, b.Note })
                .ToListAsync(ct);
        }

        var row = rows.FirstOrDefault(r => scan.Matches(r.Barcode));
        return row == null ? null : new CatalogChildMatch(row.Barcode, row.MasterProductId, row.MasterName, row.Note, null);
    }

    /// <summary>
    /// A scanned product barcode: what it was read as, the parsed core, and whether a stored barcode
    /// is the same one. Weight- and price-embedded barcodes and produce PLUs never name a catalog
    /// child, so they don't parse here.
    /// </summary>
    private sealed class ScannedBarcode
    {
        private readonly Barcode? _parsed;

        private ScannedBarcode(string raw, Barcode? parsed)
        {
            _parsed = parsed;
            CoreData = parsed?.Data ?? raw;
            Variants = parsed != null && parsed.Data != raw ? [raw, parsed.Data] : [raw];
        }

        public string CoreData { get; }

        public bool IsParsed => _parsed != null;

        public List<string> Variants { get; }

        public static ScannedBarcode? From(string? barcode)
        {
            var raw = barcode?.Trim();
            if (string.IsNullOrEmpty(raw)
                || WeightBarcodeParser.IsType2Barcode(raw)
                || WeightBarcodeParser.IsProducePlu(raw))
                return null;

            BarcodeParser.TryParse(raw, out var parsed);
            return new ScannedBarcode(raw, parsed);
        }

        public bool Matches(string stored) =>
            Variants.Contains(stored)
            || (_parsed != null && BarcodeParser.TryParse(stored, out var other) && other!.Equals(_parsed));
    }
}
