using System.Text.Json;
using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ProductSearchService : IProductSearchService
{
    private readonly HomeManagementDbContext _context;
    private readonly IDbContextFactory<HomeManagementDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileAccessTokenService _tokenService;
    private readonly IMasterProductImageResolver _imageResolver;
    private readonly IDistributedCache _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<ProductSearchService> _logger;

    /// <summary>
    /// Data source identifier for local products in lookup results.
    /// </summary>
    public const string LocalProductsDataSource = "Local Database";

    private static readonly DistributedCacheEntryOptions AutocompleteCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
    };

    private static readonly DistributedCacheEntryOptions VersionCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    };

    public ProductSearchService(
        HomeManagementDbContext context,
        IDbContextFactory<HomeManagementDbContext> contextFactory,
        IMapper mapper,
        IFileStorageService fileStorage,
        IFileAccessTokenService tokenService,
        IMasterProductImageResolver imageResolver,
        IDistributedCache cache,
        ITenantProvider tenantProvider,
        ILogger<ProductSearchService> logger)
    {
        _context = context;
        _contextFactory = contextFactory;
        _mapper = mapper;
        _fileStorage = fileStorage;
        _tokenService = tokenService;
        _imageResolver = imageResolver;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    // Public API
    // ═══════════════════════════════════════════════════════════

    public async Task<List<ProductDto>> SearchAsync(string searchTerm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<ProductDto>();

        var normalizedTerm = searchTerm.ToLowerInvariant();

        var products = await ApplyTextSearch(
                ProductsWithFullIncludes(), normalizedTerm, ProductSearchFields.All)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var dtos = _mapper.Map<List<ProductDto>>(products);

        var productLookup = products.ToDictionary(p => p.Id);
        foreach (var dto in dtos)
        {
            if (dto.Images != null && productLookup.TryGetValue(dto.Id, out var product))
            {
                SetImageUrls(dto.Images, product.Images.ToList(), dto.Id);
            }
        }

        return dtos;
    }

    public async Task<List<ProductAutocompleteDto>> AutocompleteAsync(
        string searchTerm, int maxResults = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<ProductAutocompleteDto>();

        var normalizedSearch = searchTerm.ToLowerInvariant();

        // Check cache
        var cacheKey = await GetAutocompleteCacheKeyAsync(normalizedSearch, maxResults);
        if (cacheKey != null)
        {
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (cached != null)
            {
                return JsonSerializer.Deserialize<List<ProductAutocompleteDto>>(cached)
                    ?? new List<ProductAutocompleteDto>();
            }
        }

        var products = await ProductsWithLightIncludes()
            .Where(p => p.IsActive && EF.Functions.ILike(p.Name, $"%{normalizedSearch}%"))
            .OrderByDescending(p => p.ChildProducts.Count)
            .ThenBy(p => p.Name)
            .Take(maxResults)
            .ToListAsync(ct);

        var results = products.Select(p =>
        {
            var storeMetadata = p.StoreMetadata.FirstOrDefault();
            return new ProductAutocompleteDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                ProductGroupName = p.ProductGroup?.Name,
                PrimaryImageUrl = ResolvePrimaryImageUrl(p),
                PreferredStoreAisle = storeMetadata?.Aisle,
                PreferredStoreDepartment = storeMetadata?.Department
            };
        }).ToList();

        // Store in cache
        if (cacheKey != null)
        {
            var json = JsonSerializer.Serialize(results);
            await _cache.SetStringAsync(cacheKey, json, AutocompleteCacheOptions, ct);
        }

        return results;
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var productBarcode = await FindByBarcodeAsync(barcode, ct);

        if (productBarcode?.Product == null) return null;

        var dto = _mapper.Map<ProductDto>(productBarcode.Product);
        SetImageUrls(dto.Images, productBarcode.Product.Images.ToList(), dto.Id);

        // Populate stock summary
        var stockByProduct = await GetStockByProductAndLocationAsync(ct);
        if (stockByProduct.TryGetValue(dto.Id, out var stockLocations))
        {
            dto.StockByLocation = stockLocations;
            dto.TotalStockAmount = stockLocations.Sum(s => s.Amount);
        }

        return dto;
    }

    public async Task<List<ParentProductSearchResultDto>> SearchParentProductsAsync(
        string searchTerm, CancellationToken ct = default)
    {
        var normalizedTerm = searchTerm.ToLowerInvariant();

        // Run tenant and master queries in parallel using separate DbContexts
        var tenantTask = SearchTenantParentProductsAsync(normalizedTerm, ct);
        var masterTask = SearchMasterCatalogParentProductsAsync(normalizedTerm, ct);

        await Task.WhenAll(tenantTask, masterTask);

        var tenantResults = tenantTask.Result;
        var masterResults = masterTask.Result;

        // Deduplicate: tenant results take priority
        var tenantNames = new HashSet<string>(
            tenantResults.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);

        var combined = new List<ParentProductSearchResultDto>(tenantResults);
        combined.AddRange(masterResults.Where(m => !tenantNames.Contains(m.Name)));

        return combined.OrderBy(r => r.Name).Take(50).ToList();
    }

    public async Task<IQueryable<Product>> BuildProductQueryAsync(
        ProductFilterRequest? filter, CancellationToken ct = default)
    {
        var query = ProductsWithFullIncludes();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var normalizedTerm = filter.SearchTerm.ToLowerInvariant();
                query = ApplyTextSearch(query, normalizedTerm, ProductSearchFields.NameAndDescription);
            }

            if (filter.LocationId.HasValue)
                query = query.Where(p => p.LocationId == filter.LocationId.Value);

            if (filter.ProductGroupId.HasValue)
                query = query.Where(p => p.ProductGroupId == filter.ProductGroupId.Value);

            if (filter.ShoppingLocationId.HasValue)
                query = query.Where(p => p.ShoppingLocationId == filter.ShoppingLocationId.Value);

            if (filter.IsActive.HasValue)
                query = query.Where(p => p.IsActive == filter.IsActive.Value);

            if (filter.LowStock == true)
            {
                var lowStockIds = await GetLowStockProductIdsAsync(ct);
                query = query.Where(p => lowStockIds.Contains(p.Id));
            }

            query = filter.SortBy?.ToLower() switch
            {
                "name" => filter.Descending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                "createdat" => filter.Descending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
                "updatedat" => filter.Descending ? query.OrderByDescending(p => p.UpdatedAt) : query.OrderBy(p => p.UpdatedAt),
                _ => query.OrderBy(p => p.Name)
            };
        }
        else
        {
            query = query.OrderBy(p => p.Name);
        }

        return query;
    }

    public async Task<List<ProductLookupResult>> SearchLocalForLookupAsync(
        string query, ProductLookupSearchType searchType, int maxResults, CancellationToken ct = default)
    {
        var normalizedQuery = query.ToLowerInvariant();
        var results = new List<ProductLookupResult>();

        var productsQuery = ProductsWithLookupIncludes()
            .Where(p => p.IsActive);

        if (searchType == ProductLookupSearchType.Barcode)
        {
            var normalizedBarcode = ProductLookupPipelineContext.NormalizeBarcode(query);
            productsQuery = productsQuery.Where(p =>
                p.Barcodes.Any(b => b.Barcode.Contains(query)));

            var products = await productsQuery.Take(maxResults).ToListAsync(ct);

            foreach (var product in products)
            {
                var matchingBarcode = product.Barcodes.FirstOrDefault(b =>
                    ProductLookupPipelineContext.NormalizeBarcode(b.Barcode)
                        .Equals(normalizedBarcode, StringComparison.OrdinalIgnoreCase) ||
                    b.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase));

                if (matchingBarcode != null || products.Count <= maxResults)
                {
                    results.Add(ConvertToLookupResult(product));
                }
            }
        }
        else
        {
            productsQuery = ApplyTextSearch(productsQuery, normalizedQuery,
                ProductSearchFields.Name | ProductSearchFields.Description | ProductSearchFields.ProductGroupName);

            var products = await productsQuery
                .OrderBy(p => p.Name)
                .Take(maxResults)
                .ToListAsync(ct);

            results = products.Select(ConvertToLookupResult).ToList();
        }

        return results;
    }

    public void InvalidateCache()
    {
        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue) return;

        // Increment version to invalidate all autocomplete cache entries for this tenant
        var versionKey = $"product-ac-version:{tenantId.Value}";
        var newVersion = DateTimeOffset.UtcNow.Ticks.ToString();
        _cache.SetString(versionKey, newVersion, VersionCacheOptions);

        _logger.LogDebug("Invalidated product search cache for tenant {TenantId}", tenantId.Value);
    }

    // ═══════════════════════════════════════════════════════════
    // Shared building blocks — text search
    // ═══════════════════════════════════════════════════════════

    private static IQueryable<Product> ApplyTextSearch(
        IQueryable<Product> query, string normalizedTerm, ProductSearchFields fields)
    {
        var pattern = $"%{normalizedTerm}%";
        return query.Where(p =>
            ((fields & ProductSearchFields.Name) != 0 &&
                EF.Functions.ILike(p.Name, pattern)) ||
            ((fields & ProductSearchFields.Description) != 0 &&
                p.Description != null && EF.Functions.ILike(p.Description, pattern)) ||
            ((fields & ProductSearchFields.ProductGroupName) != 0 &&
                p.ProductGroup != null && EF.Functions.ILike(p.ProductGroup.Name, pattern)) ||
            ((fields & ProductSearchFields.ShoppingLocationName) != 0 &&
                p.ShoppingLocation != null && EF.Functions.ILike(p.ShoppingLocation.Name, pattern)) ||
            ((fields & ProductSearchFields.Barcodes) != 0 &&
                p.Barcodes.Any(b => b.Barcode.Contains(normalizedTerm))));
    }

    // ═══════════════════════════════════════════════════════════
    // Shared building blocks — barcode matching
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 3-phase barcode matching: exact → variant (EAN-13/UPC-A) → normalized fallback.
    /// </summary>
    private async Task<ProductBarcode?> FindByBarcodeAsync(string barcode, CancellationToken ct)
    {
        // Phase 1: Exact match (fast path)
        var productBarcode = await ProductBarcodesWithIncludes()
            .FirstOrDefaultAsync(pb => pb.Barcode == barcode, ct);

        // Phase 2: Variant match (EAN-13 <-> UPC-A format mismatches)
        if (productBarcode == null)
        {
            var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);
            if (variants.Count > 0)
            {
                var variantBarcodes = variants.Select(v => v.Barcode).ToList();
                productBarcode = await ProductBarcodesWithIncludes()
                    .FirstOrDefaultAsync(pb => variantBarcodes.Contains(pb.Barcode), ct);
            }
        }

        // Phase 3: Normalized fallback (handles non-standard formats like Kroger padding)
        if (productBarcode == null)
        {
            var normalizedInput = ProductLookupPipelineContext.NormalizeBarcode(barcode);
            if (!string.IsNullOrEmpty(normalizedInput) && normalizedInput != "0")
            {
                var candidates = await ProductBarcodesWithIncludes()
                    .Where(pb => pb.Barcode.Contains(normalizedInput))
                    .ToListAsync(ct);

                productBarcode = candidates.FirstOrDefault(pb =>
                    ProductLookupPipelineContext.NormalizeBarcode(pb.Barcode) == normalizedInput);
            }
        }

        return productBarcode;
    }

    // ═══════════════════════════════════════════════════════════
    // Shared building blocks — include sets
    // ═══════════════════════════════════════════════════════════

    private IQueryable<Product> ProductsWithFullIncludes()
    {
        return _context.Products
            .Include(p => p.Location)
            .Include(p => p.QuantityUnitPurchase)
            .Include(p => p.QuantityUnitStock)
            .Include(p => p.ProductGroup)
            .Include(p => p.ShoppingLocation)
            .Include(p => p.ParentProduct)
            .Include(p => p.ChildProducts)
            .Include(p => p.MasterProduct)
                .ThenInclude(mp => mp!.Images)
            .Include(p => p.Barcodes)
            .Include(p => p.Images)
            .AsQueryable();
    }

    private IQueryable<Product> ProductsWithLightIncludes()
    {
        return _context.Products
            .Include(p => p.ProductGroup)
            .Include(p => p.Images.Where(i => i.IsPrimary).Take(1))
            .Include(p => p.MasterProduct)
                .ThenInclude(mp => mp!.Images)
            .Include(p => p.StoreMetadata.Take(1));
    }

    private IQueryable<Product> ProductsWithLookupIncludes()
    {
        return _context.Products
            .Include(p => p.Barcodes)
            .Include(p => p.Images)
            .Include(p => p.MasterProduct)
                .ThenInclude(mp => mp!.Images)
            .Include(p => p.ProductGroup)
            .Include(p => p.ShoppingLocation)
            .Include(p => p.Nutrition);
    }

    private IQueryable<ProductBarcode> ProductBarcodesWithIncludes()
    {
        return _context.ProductBarcodes
            .Include(pb => pb.Product)
                .ThenInclude(p => p.Location)
            .Include(pb => pb.Product)
                .ThenInclude(p => p.QuantityUnitPurchase)
            .Include(pb => pb.Product)
                .ThenInclude(p => p.QuantityUnitStock)
            .Include(pb => pb.Product)
                .ThenInclude(p => p.ProductGroup)
            .Include(pb => pb.Product)
                .ThenInclude(p => p.ShoppingLocation)
            .Include(pb => pb.Product)
                .ThenInclude(p => p.ParentProduct)
            .Include(pb => pb.Product)
                .ThenInclude(p => p.ChildProducts)
            .Include(pb => pb.Product)
                .ThenInclude(p => p.Barcodes)
            .Include(pb => pb.Product)
                .ThenInclude(p => p.Images);
    }

    // ═══════════════════════════════════════════════════════════
    // Shared building blocks — image URL resolution
    // ═══════════════════════════════════════════════════════════

    private void SetImageUrls(List<ProductImageDto> dtos, List<ProductImage> entities, Guid productId)
    {
        var entityLookup = entities.ToDictionary(e => e.Id);

        foreach (var dto in dtos)
        {
            if (!string.IsNullOrEmpty(dto.FileName) && entityLookup.TryGetValue(dto.Id, out var entity))
            {
                var token = _tokenService.GenerateToken("product-image", dto.Id, entity.TenantId);
                dto.Url = _fileStorage.GetProductImageUrl(productId, dto.Id, token);
            }
        }
    }

    /// <summary>
    /// Resolves the primary image URL for a product with master product fallback.
    /// Used by autocomplete, parent search, and lookup result conversion.
    /// </summary>
    private string? ResolvePrimaryImageUrl(Product product)
    {
        var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary)
            ?? product.Images.FirstOrDefault();

        if (primaryImage != null)
        {
            if (!string.IsNullOrEmpty(primaryImage.ExternalThumbnailUrl))
                return primaryImage.ExternalThumbnailUrl;
            if (!string.IsNullOrEmpty(primaryImage.ExternalUrl))
                return primaryImage.ExternalUrl;

            var token = _tokenService.GenerateToken("product-image", primaryImage.Id, primaryImage.TenantId);
            return _fileStorage.GetProductImageUrl(product.Id, primaryImage.Id, token);
        }

        // Fallback to master product image
        if (product.MasterProduct != null)
        {
            var mp = product.MasterProduct;
            var mpPrimary = mp.Images?.FirstOrDefault(i => i.IsPrimary);
            return _imageResolver.GetImageUrl(mp.ImageSlug, mpPrimary != null, mp.Id, mpPrimary?.Id);
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════
    // Shared building blocks — lookup result conversion
    // ═══════════════════════════════════════════════════════════

    private ProductLookupResult ConvertToLookupResult(Product product)
    {
        var result = new ProductLookupResult
        {
            Name = product.Name,
            Description = product.Description,
            Barcode = product.Barcodes.FirstOrDefault()?.Barcode,
            Categories = product.ProductGroup != null
                ? new List<string> { product.ProductGroup.Name }
                : new List<string>(),
            ShoppingLocationId = product.ShoppingLocationId,
            ShoppingLocationName = product.ShoppingLocation?.Name,
            DataSources = new Dictionary<string, string>
            {
                { LocalProductsDataSource, product.Id.ToString() }
            }
        };

        // Resolve image
        var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary) ?? product.Images.FirstOrDefault();
        if (primaryImage != null)
        {
            if (!string.IsNullOrEmpty(primaryImage.ExternalUrl))
            {
                result.ImageUrl = new ResultImage
                {
                    ImageUrl = primaryImage.ExternalUrl,
                    PluginId = primaryImage.ExternalSource ?? LocalProductsDataSource
                };
                result.ThumbnailUrl = !string.IsNullOrEmpty(primaryImage.ExternalThumbnailUrl)
                    ? new ResultImage
                    {
                        ImageUrl = primaryImage.ExternalThumbnailUrl,
                        PluginId = primaryImage.ExternalSource ?? LocalProductsDataSource
                    }
                    : null;
            }
            else if (!string.IsNullOrEmpty(primaryImage.FileName))
            {
                var token = _tokenService.GenerateToken("product-image", primaryImage.Id, product.TenantId);
                var imageUrl = _fileStorage.GetProductImageUrl(product.Id, primaryImage.Id, token);
                result.ImageUrl = new ResultImage
                {
                    ImageUrl = imageUrl,
                    PluginId = LocalProductsDataSource
                };
            }
        }

        // Fallback to master product image
        if (result.ImageUrl == null && product.MasterProduct != null)
        {
            var mp = product.MasterProduct;
            var mpPrimaryImage = mp.Images?.FirstOrDefault(i => i.IsPrimary);
            var masterImageUrl = _imageResolver.GetImageUrl(
                mp.ImageSlug, mpPrimaryImage != null, mp.Id, mpPrimaryImage?.Id);
            if (!string.IsNullOrEmpty(masterImageUrl))
            {
                result.ImageUrl = new ResultImage
                {
                    ImageUrl = masterImageUrl,
                    PluginId = LocalProductsDataSource
                };
                result.ThumbnailUrl = result.ImageUrl;
            }
        }

        // Add nutrition data if available
        if (product.Nutrition != null)
        {
            result.BrandName = product.Nutrition.BrandName;
            result.BrandOwner = product.Nutrition.BrandOwner;
            result.Ingredients = product.Nutrition.Ingredients;
            result.ServingSizeDescription = product.Nutrition.ServingSizeDescription;
            result.Nutrition = new ProductLookupNutrition
            {
                Source = product.Nutrition.DataSource ?? LocalProductsDataSource,
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
                Zinc = product.Nutrition.Zinc
            };
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // Parent product search helpers (parallel)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Searches tenant products. Uses the scoped DbContext.
    /// </summary>
    private async Task<List<ParentProductSearchResultDto>> SearchTenantParentProductsAsync(
        string normalizedTerm, CancellationToken ct)
    {
        var tenantProducts = await _context.Products
            .Where(p => p.IsActive && EF.Functions.ILike(p.Name, $"%{normalizedTerm}%"))
            .Include(p => p.ProductGroup)
            .Include(p => p.ChildProducts)
            .Include(p => p.MasterProduct)
                .ThenInclude(mp => mp!.Images)
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .OrderBy(p => p.Name)
            .Take(25)
            .ToListAsync(ct);

        return tenantProducts.Select(p =>
        {
            string? imageUrl = null;
            var tenantPrimaryImage = p.Images?.FirstOrDefault(i => i.IsPrimary) ?? p.Images?.FirstOrDefault();
            if (tenantPrimaryImage != null)
            {
                imageUrl = _fileStorage.GetProductImageUrl(p.Id, tenantPrimaryImage.Id);
            }
            else if (p.MasterProduct != null)
            {
                var mp = p.MasterProduct;
                var masterPrimaryImage = mp.Images?.FirstOrDefault(i => i.IsPrimary);
                imageUrl = _imageResolver.GetImageUrl(
                    mp.ImageSlug, masterPrimaryImage != null, mp.Id, masterPrimaryImage?.Id);
            }

            return new ParentProductSearchResultDto
            {
                Id = p.Id,
                Name = p.Name,
                ProductGroupName = p.ProductGroup?.Name,
                ChildProductCount = p.ChildProducts?.Count(c => c.IsActive) ?? 0,
                Source = "tenant",
                ImageUrl = imageUrl
            };
        }).ToList();
    }

    /// <summary>
    /// Searches master catalog. Uses a factory-created DbContext for thread safety.
    /// </summary>
    private async Task<List<ParentProductSearchResultDto>> SearchMasterCatalogParentProductsAsync(
        string normalizedTerm, CancellationToken ct)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(ct);

        var masterProducts = await dbContext.MasterProducts
            .IgnoreQueryFilters()
            .Include(mp => mp.Images)
            .Where(mp => mp.ParentMasterProductId == null &&
                         EF.Functions.ILike(mp.Name, $"%{normalizedTerm}%"))
            .OrderBy(mp => mp.Name)
            .Take(25)
            .ToListAsync(ct);

        return masterProducts.Select(mp =>
        {
            var primaryImage = mp.Images?.FirstOrDefault(i => i.IsPrimary);
            return new ParentProductSearchResultDto
            {
                Id = mp.Id,
                Name = mp.Name,
                ProductGroupName = mp.Category,
                ChildProductCount = 0,
                Source = "master",
                MasterProductId = mp.Id,
                ImageUrl = _imageResolver.GetImageUrl(
                    mp.ImageSlug, primaryImage != null, mp.Id, primaryImage?.Id)
            };
        }).ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // Cache helpers
    // ═══════════════════════════════════════════════════════════

    private async Task<string?> GetAutocompleteCacheKeyAsync(string normalizedTerm, int maxResults)
    {
        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue) return null;

        var versionKey = $"product-ac-version:{tenantId.Value}";
        var version = await _cache.GetStringAsync(versionKey);
        version ??= "0";

        return $"product-ac:{tenantId.Value}:v{version}:{normalizedTerm}:{maxResults}";
    }

    // ═══════════════════════════════════════════════════════════
    // Stock helpers (used by GetByBarcodeAsync and BuildProductQueryAsync)
    // ═══════════════════════════════════════════════════════════

    private async Task<Dictionary<Guid, List<ProductStockLocationDto>>> GetStockByProductAndLocationAsync(
        CancellationToken ct)
    {
        var stockData = await _context.Stock
            .Include(s => s.Location)
            .GroupBy(s => new
            {
                s.ProductId,
                s.LocationId,
                LocationName = s.Location != null ? s.Location.Name : "Unknown"
            })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.LocationId,
                g.Key.LocationName,
                Amount = g.Sum(s => s.Amount),
                EntryCount = g.Count()
            })
            .ToListAsync(ct);

        return stockData
            .GroupBy(s => s.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => new ProductStockLocationDto
                {
                    LocationId = s.LocationId ?? Guid.Empty,
                    LocationName = s.LocationName,
                    Amount = s.Amount,
                    EntryCount = s.EntryCount
                }).ToList());
    }

    private async Task<HashSet<Guid>> GetLowStockProductIdsAsync(CancellationToken ct)
    {
        var stockLevels = await _context.Stock
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, CurrentStock = g.Sum(s => s.Amount) })
            .ToListAsync(ct);

        var products = await _context.Products
            .Select(p => new { p.Id, p.MinStockAmount })
            .ToListAsync(ct);

        return products
            .Where(p =>
            {
                var stock = stockLevels.FirstOrDefault(s => s.ProductId == p.Id);
                var currentStock = stock?.CurrentStock ?? 0;
                return currentStock < p.MinStockAmount && p.MinStockAmount > 0;
            })
            .Select(p => p.Id)
            .ToHashSet();
    }
}
