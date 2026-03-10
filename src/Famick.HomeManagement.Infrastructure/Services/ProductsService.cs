using System.Text.Json;
using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.DTOs.TodoItems;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ProductsService : IProductsService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileAccessTokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMasterProductImageResolver _imageResolver;

    public ProductsService(
        HomeManagementDbContext context,
        IMapper mapper,
        IFileStorageService fileStorage,
        IFileAccessTokenService tokenService,
        IHttpClientFactory httpClientFactory,
        IMasterProductImageResolver imageResolver)
    {
        _context = context;
        _mapper = mapper;
        _fileStorage = fileStorage;
        _tokenService = tokenService;
        _httpClientFactory = httpClientFactory;
        _imageResolver = imageResolver;
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        // Check for duplicate name among active products only
        // Inactive products don't block new product creation with the same name
        var exists = await _context.Products
            .AnyAsync(p => p.Name == request.Name && p.IsActive, cancellationToken);
        if (exists)
        {
            throw new DuplicateEntityException(nameof(Product), "Name", request.Name);
        }

        // Validate foreign keys
        await ValidateForeignKeysAsync(
            request.LocationId,
            request.QuantityUnitIdPurchase,
            request.QuantityUnitIdStock,
            request.ProductGroupId,
            request.ShoppingLocationId,
            cancellationToken);

        var product = _mapper.Map<Product>(request);
        product.Id = Guid.NewGuid();

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve created product");
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
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
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product == null) return null;

        var dto = _mapper.Map<ProductDto>(product);

        // Set computed URLs for images with access tokens
        SetImageUrls(dto.Images, product.Images.ToList(), product.Id);

        // Set master product image URL as fallback when tenant product has no images
        if (dto.Images.Count == 0 && product.MasterProduct != null)
        {
            var mp = product.MasterProduct;
            var primaryImage = mp.Images?.FirstOrDefault(i => i.IsPrimary);
            dto.MasterProductImageUrl = _imageResolver.GetImageUrl(
                mp.ImageSlug,
                primaryImage != null,
                mp.Id,
                primaryImage?.Id);
        }

        return dto;
    }

    public async Task<List<ProductDto>> ListAsync(ProductFilterRequest? filter = null, CancellationToken cancellationToken = default)
    {
        var query = await BuildProductQueryAsync(filter, cancellationToken);

        var products = await query.ToListAsync(cancellationToken);
        var dtos = _mapper.Map<List<ProductDto>>(products);

        var stockByProduct = await GetStockByProductAndLocationAsync(cancellationToken);
        EnrichProductDtos(dtos, products, stockByProduct);

        return dtos;
    }

    public async Task<PagedResult<ProductDto>> ListPagedAsync(ProductFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var page = filter.Page ?? 1;
        var pageSize = filter.PageSize;

        var query = await BuildProductQueryAsync(filter, cancellationToken);

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<ProductDto>>(products);

        var productIds = products.Select(p => p.Id).ToHashSet();
        var stockByProduct = await GetStockByProductAndLocationAsync(productIds, cancellationToken);
        EnrichProductDtos(dtos, products, stockByProduct);

        return new PagedResult<ProductDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<IQueryable<Product>> BuildProductQueryAsync(ProductFilterRequest? filter, CancellationToken cancellationToken)
    {
        var query = _context.Products
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

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchTerm = filter.SearchTerm.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(searchTerm) ||
                    (p.Description != null && p.Description.ToLower().Contains(searchTerm)));
            }

            if (filter.LocationId.HasValue)
            {
                query = query.Where(p => p.LocationId == filter.LocationId.Value);
            }

            if (filter.ProductGroupId.HasValue)
            {
                query = query.Where(p => p.ProductGroupId == filter.ProductGroupId.Value);
            }

            if (filter.ShoppingLocationId.HasValue)
            {
                query = query.Where(p => p.ShoppingLocationId == filter.ShoppingLocationId.Value);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(p => p.IsActive == filter.IsActive.Value);
            }

            if (filter.LowStock == true)
            {
                var stockLevels = await GetCurrentStockLevelsAsync(cancellationToken);
                var lowStockProductIds = stockLevels
                    .Where(s => s.CurrentStock < s.MinStockAmount && s.MinStockAmount > 0)
                    .Select(s => s.ProductId)
                    .ToHashSet();

                query = query.Where(p => lowStockProductIds.Contains(p.Id));
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

    private void EnrichProductDtos(List<ProductDto> dtos, List<Product> products, Dictionary<Guid, List<ProductStockLocationDto>> stockByProduct)
    {
        var productLookup = products.ToDictionary(p => p.Id);

        foreach (var dto in dtos)
        {
            if (productLookup.TryGetValue(dto.Id, out var product))
            {
                if (dto.Images != null)
                {
                    SetImageUrls(dto.Images, product.Images.ToList(), dto.Id);
                }

                // Set master product image URL as fallback when tenant product has no images
                if ((dto.Images == null || dto.Images.Count == 0) && product.MasterProduct != null)
                {
                    var mp = product.MasterProduct;
                    var primaryImage = mp.Images?.FirstOrDefault(i => i.IsPrimary);
                    dto.MasterProductImageUrl = _imageResolver.GetImageUrl(
                        mp.ImageSlug,
                        primaryImage != null,
                        mp.Id,
                        primaryImage?.Id);
                }
            }

            if (stockByProduct.TryGetValue(dto.Id, out var stockLocations))
            {
                dto.StockByLocation = stockLocations;
                dto.TotalStockAmount = stockLocations.Sum(s => s.Amount);
            }
        }
    }

    private Task<Dictionary<Guid, List<ProductStockLocationDto>>> GetStockByProductAndLocationAsync(CancellationToken cancellationToken)
    {
        return GetStockByProductAndLocationAsync(null, cancellationToken);
    }

    private async Task<Dictionary<Guid, List<ProductStockLocationDto>>> GetStockByProductAndLocationAsync(IReadOnlyCollection<Guid>? productIds, CancellationToken cancellationToken)
    {
        var stockQuery = _context.Stock
            .Include(s => s.Location)
            .AsQueryable();

        if (productIds != null)
        {
            stockQuery = stockQuery.Where(s => productIds.Contains(s.ProductId));
        }

        var stockData = await stockQuery
            .GroupBy(s => new { s.ProductId, s.LocationId, LocationName = s.Location != null ? s.Location.Name : "Unknown" })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.LocationId,
                g.Key.LocationName,
                Amount = g.Sum(s => s.Amount),
                EntryCount = g.Count()
            })
            .ToListAsync(cancellationToken);

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
                }).ToList()
            );
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .Include(p => p.MasterProduct)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        // Check for duplicate name among active products (excluding current product)
        // Inactive products don't block renaming to that name
        var duplicateExists = await _context.Products
            .AnyAsync(p => p.Name == request.Name && p.Id != id && p.IsActive, cancellationToken);
        if (duplicateExists)
        {
            throw new DuplicateEntityException(nameof(Product), "Name", request.Name);
        }

        // Validate foreign keys
        await ValidateForeignKeysAsync(
            request.LocationId,
            request.QuantityUnitIdPurchase,
            request.QuantityUnitIdStock,
            request.ProductGroupId,
            request.ShoppingLocationId,
            cancellationToken);

        _mapper.Map(request, product);

        // Track overridden fields when linked to a master product
        if (product.MasterProductId.HasValue && product.MasterProduct != null)
        {
            product.OverriddenFields = BuildOverriddenFields(product, product.MasterProduct);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve updated product");
    }

    /// <summary>
    /// Compares tenant product values against the linked master product to determine
    /// which shared fields the tenant has overridden. Fields matching master values
    /// are NOT considered overridden.
    /// </summary>
    private static string BuildOverriddenFields(Product product, MasterProduct master)
    {
        var overridden = new List<string>();

        if (!string.Equals(product.Name, master.Name, StringComparison.Ordinal))
            overridden.Add("Name");
        if (!string.Equals(product.Description ?? "", master.Description ?? "", StringComparison.Ordinal))
            overridden.Add("Description");
        if (product.DefaultBestBeforeDays != master.DefaultBestBeforeDays)
            overridden.Add("DefaultBestBeforeDays");
        if (product.TracksBestBeforeDate != master.TracksBestBeforeDate)
            overridden.Add("TracksBestBeforeDate");
        if (product.ServingSize != master.ServingSize)
            overridden.Add("ServingSize");
        if (!string.Equals(product.ServingUnit ?? "", master.ServingUnit ?? "", StringComparison.Ordinal))
            overridden.Add("ServingUnit");
        if (product.ServingsPerContainer != master.ServingsPerContainer)
            overridden.Add("ServingsPerContainer");
        if (!string.Equals(product.DataSourceAttribution ?? "", master.DataSourceAttribution ?? "", StringComparison.Ordinal))
            overridden.Add("DataSourceAttribution");

        return JsonSerializer.Serialize(overridden);
    }

    public async Task<ProductDependenciesDto> GetDependenciesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product == null)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        var stockCount = await _context.Stock.CountAsync(s => s.ProductId == id, cancellationToken);
        var recipeCount = await _context.RecipePositions.CountAsync(rp => rp.ProductId == id, cancellationToken);

        var shoppingListItems = await _context.ShoppingListItems
            .Where(sli => sli.ProductId == id)
            .Select(sli => sli.ShoppingList.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new ProductDependenciesDto
        {
            StockEntryCount = stockCount,
            ShoppingListItemCount = shoppingListItems.Count > 0
                ? await _context.ShoppingListItems.CountAsync(sli => sli.ProductId == id, cancellationToken)
                : 0,
            RecipeCount = recipeCount,
            ShoppingListNames = shoppingListItems
        };
    }

    public async Task DeleteAsync(Guid id, bool force = false, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product == null)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        // Recipes always block deletion — cannot auto-remove recipe ingredients
        var usedInRecipes = await _context.RecipePositions.AnyAsync(rp => rp.ProductId == id, cancellationToken);
        if (usedInRecipes)
        {
            throw new BusinessRuleViolationException(
                "ProductInUse",
                $"Cannot delete product '{product.Name}' because it is used in recipes");
        }

        var hasStock = await _context.Stock.AnyAsync(s => s.ProductId == id, cancellationToken);
        var hasShoppingListItems = await _context.ShoppingListItems.AnyAsync(sli => sli.ProductId == id, cancellationToken);

        if ((hasStock || hasShoppingListItems) && !force)
        {
            throw new BusinessRuleViolationException(
                "ProductHasDependencies",
                $"Product '{product.Name}' has dependencies. Use force delete to remove stock entries and shopping list items.");
        }

        // Force delete: remove stock entries
        if (hasStock)
        {
            var stockEntries = await _context.Stock
                .Where(s => s.ProductId == id)
                .ToListAsync(cancellationToken);
            _context.Stock.RemoveRange(stockEntries);
        }

        // Force delete: remove shopping list items
        if (hasShoppingListItems)
        {
            var shoppingListItems = await _context.ShoppingListItems
                .Where(sli => sli.ProductId == id)
                .ToListAsync(cancellationToken);
            _context.ShoppingListItems.RemoveRange(shoppingListItems);
        }

        // Delete associated stock log records
        var stockLogs = await _context.StockLog
            .Where(sl => sl.ProductId == id)
            .ToListAsync(cancellationToken);
        if (stockLogs.Count > 0)
        {
            _context.StockLog.RemoveRange(stockLogs);
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Barcode management
    public async Task<ProductBarcodeDto> AddBarcodeAsync(Guid productId, string barcode, string? note = null, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        if (product == null)
        {
            throw new EntityNotFoundException(nameof(Product), productId);
        }

        // Check for duplicate barcode
        var barcodeExists = await _context.ProductBarcodes
            .AnyAsync(pb => pb.Barcode == barcode, cancellationToken);
        if (barcodeExists)
        {
            throw new DuplicateEntityException(nameof(ProductBarcode), "Barcode", barcode);
        }

        var productBarcode = new ProductBarcode
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Barcode = barcode,
            Note = note
        };

        _context.ProductBarcodes.Add(productBarcode);
        await _context.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ProductBarcodeDto>(productBarcode);
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        // Phase 1: Exact match (fast path, covers most cases)
        var productBarcode = await ProductBarcodesWithIncludes()
            .FirstOrDefaultAsync(pb => pb.Barcode == barcode, cancellationToken);

        // Phase 2: Variant match (handles EAN-13 <-> UPC-A format mismatches)
        if (productBarcode == null)
        {
            var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);
            if (variants.Count > 0)
            {
                var variantBarcodes = variants.Select(v => v.Barcode).ToList();
                productBarcode = await ProductBarcodesWithIncludes()
                    .FirstOrDefaultAsync(pb => variantBarcodes.Contains(pb.Barcode), cancellationToken);
            }
        }

        // Phase 3: Normalized fallback (handles non-standard formats like Kroger padding)
        if (productBarcode == null)
        {
            var normalizedInput = ProductLookupPipelineContext.NormalizeBarcode(barcode);
            if (!string.IsNullOrEmpty(normalizedInput) && normalizedInput != "0")
            {
                // Query candidates that could plausibly match, then filter in-memory
                var candidates = await ProductBarcodesWithIncludes()
                    .Where(pb => pb.Barcode.Contains(normalizedInput))
                    .ToListAsync(cancellationToken);

                productBarcode = candidates.FirstOrDefault(pb =>
                    ProductLookupPipelineContext.NormalizeBarcode(pb.Barcode) == normalizedInput);
            }
        }

        if (productBarcode?.Product == null) return null;

        var dto = _mapper.Map<ProductDto>(productBarcode.Product);

        // Set computed URLs for images with access tokens
        SetImageUrls(dto.Images, productBarcode.Product.Images.ToList(), dto.Id);

        // Populate stock summary
        var stockByProduct = await GetStockByProductAndLocationAsync(cancellationToken);
        if (stockByProduct.TryGetValue(dto.Id, out var stockLocations))
        {
            dto.StockByLocation = stockLocations;
            dto.TotalStockAmount = stockLocations.Sum(s => s.Amount);
        }

        return dto;
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

    public async Task DeleteBarcodeAsync(Guid barcodeId, CancellationToken cancellationToken = default)
    {
        var barcode = await _context.ProductBarcodes.FindAsync(new object[] { barcodeId }, cancellationToken);
        if (barcode == null)
        {
            throw new EntityNotFoundException(nameof(ProductBarcode), barcodeId);
        }

        _context.ProductBarcodes.Remove(barcode);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Image management
    public async Task<ProductImageDto> AddImageAsync(
        Guid productId,
        Stream imageStream,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        if (product == null)
        {
            throw new EntityNotFoundException(nameof(Product), productId);
        }

        // Save file to storage
        var storedFileName = await _fileStorage.SaveProductImageAsync(productId, imageStream, fileName, cancellationToken);

        // Determine sort order (add at end)
        var maxSortOrder = await _context.ProductImages
            .Where(pi => pi.ProductId == productId)
            .MaxAsync(pi => (int?)pi.SortOrder, cancellationToken) ?? -1;

        // Check if this should be primary (first image)
        var isPrimary = !await _context.ProductImages
            .AnyAsync(pi => pi.ProductId == productId, cancellationToken);

        var productImage = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            FileName = storedFileName,
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSize = fileSize,
            SortOrder = maxSortOrder + 1,
            IsPrimary = isPrimary
        };

        _context.ProductImages.Add(productImage);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<ProductImageDto>(productImage);
        var token = _tokenService.GenerateToken("product-image", productImage.Id, productImage.TenantId);
        dto.Url = _fileStorage.GetProductImageUrl(productId, productImage.Id, token);
        return dto;
    }

    public async Task<ProductImageDto?> AddImageFromUrlAsync(
        Guid productId,
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            using var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

            // Only allow image content types
            if (!contentType.StartsWith("image/"))
                return null;

            // Extract filename from URL or use default
            var uri = new Uri(imageUrl);
            var fileName = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrEmpty(fileName) || !fileName.Contains('.'))
            {
                var extension = contentType switch
                {
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".jpg"
                };
                fileName = $"product-image{extension}";
            }

            // Buffer the network stream to memory so we can determine size and read it fully
            await using var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var memoryStream = new MemoryStream();
            await networkStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            var fileSize = memoryStream.Length;
            if (fileSize == 0)
                return null;

            return await AddImageAsync(productId, memoryStream, fileName, contentType, fileSize, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            Console.WriteLine($"Failed to add image from URL {imageUrl} for product {productId}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ProductImageDto>> GetImagesAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var images = await _context.ProductImages
            .Where(pi => pi.ProductId == productId)
            .OrderBy(pi => pi.SortOrder)
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<ProductImageDto>>(images);

        // Add URLs with access tokens
        SetImageUrls(dtos, images, productId);

        return dtos;
    }

    public async Task<ProductImageDto?> GetImageByIdAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default)
    {
        // Uses IgnoreQueryFilters since this is used for download endpoints where
        // access is validated via token or authenticated user context
        var image = await _context.ProductImages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pi => pi.ProductId == productId && pi.Id == imageId, cancellationToken);

        if (image == null) return null;

        var dto = _mapper.Map<ProductImageDto>(image);
        var token = _tokenService.GenerateToken("product-image", image.Id, image.TenantId);
        dto.Url = _fileStorage.GetProductImageUrl(productId, imageId, token);
        return dto;
    }

    public async Task DeleteImageAsync(Guid imageId, CancellationToken cancellationToken = default)
    {
        var image = await _context.ProductImages.FindAsync(new object[] { imageId }, cancellationToken);
        if (image == null)
        {
            throw new EntityNotFoundException(nameof(ProductImage), imageId);
        }

        // Delete file from storage
        await _fileStorage.DeleteProductImageAsync(image.ProductId, image.FileName, cancellationToken);

        // If this was primary, make the next image primary
        if (image.IsPrimary)
        {
            var nextImage = await _context.ProductImages
                .Where(pi => pi.ProductId == image.ProductId && pi.Id != imageId)
                .OrderBy(pi => pi.SortOrder)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextImage != null)
            {
                nextImage.IsPrimary = true;
            }
        }

        _context.ProductImages.Remove(image);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPrimaryImageAsync(Guid imageId, CancellationToken cancellationToken = default)
    {
        var image = await _context.ProductImages.FindAsync(new object[] { imageId }, cancellationToken);
        if (image == null)
        {
            throw new EntityNotFoundException(nameof(ProductImage), imageId);
        }

        // Clear existing primary
        var existingPrimary = await _context.ProductImages
            .Where(pi => pi.ProductId == image.ProductId && pi.IsPrimary)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingPrimary)
        {
            existing.IsPrimary = false;
        }

        // Set new primary
        image.IsPrimary = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderImagesAsync(Guid productId, List<Guid> imageIds, CancellationToken cancellationToken = default)
    {
        var images = await _context.ProductImages
            .Where(pi => pi.ProductId == productId)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < imageIds.Count; i++)
        {
            var image = images.FirstOrDefault(img => img.Id == imageIds[i]);
            if (image != null)
            {
                image.SortOrder = i;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Stock level indicators (Phase 2)
    public async Task<List<ProductStockLevelDto>> GetStockLevelsAsync(ProductFilterRequest? filter = null, CancellationToken cancellationToken = default)
    {
        var products = await ListAsync(filter, cancellationToken);
        var stockLevels = await GetCurrentStockLevelsAsync(cancellationToken);

        var result = new List<ProductStockLevelDto>();

        foreach (var product in products)
        {
            var stockLevel = stockLevels.FirstOrDefault(s => s.ProductId == product.Id);
            var currentStock = stockLevel.ProductId != Guid.Empty ? stockLevel.CurrentStock : 0;

            var status = DetermineStockStatus(currentStock, product.MinStockAmount);

            result.Add(new ProductStockLevelDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                CurrentStock = currentStock,
                MinStockAmount = product.MinStockAmount,
                QuantityUnitName = product.QuantityUnitStockName,
                ProductGroupName = product.ProductGroupName,
                ShoppingLocationName = product.ShoppingLocationName,
                Status = status,
                DaysUntilEmpty = null  // Future: Calculate based on consumption patterns
            });
        }

        return result.OrderBy(r => r.Status).ThenBy(r => r.ProductName).ToList();
    }

    public async Task<List<ProductDto>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
    {
        var stockLevels = await GetCurrentStockLevelsAsync(cancellationToken);
        var lowStockProductIds = stockLevels
            .Where(s => s.CurrentStock < s.MinStockAmount && s.MinStockAmount > 0)
            .Select(s => s.ProductId)
            .ToHashSet();

        var products = await _context.Products
            .Include(p => p.Location)
            .Include(p => p.QuantityUnitPurchase)
            .Include(p => p.QuantityUnitStock)
            .Include(p => p.ProductGroup)
            .Include(p => p.ShoppingLocation)
            .Include(p => p.ParentProduct)
            .Include(p => p.ChildProducts)
            .Include(p => p.Barcodes)
            .Where(p => lowStockProductIds.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<ProductDto>>(products);
    }

    // Search enhancement (Phase 2)
    public async Task<List<ProductDto>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<ProductDto>();
        }

        var normalizedSearchTerm = searchTerm.ToLower();

        var products = await _context.Products
            .Include(p => p.Location)
            .Include(p => p.QuantityUnitPurchase)
            .Include(p => p.QuantityUnitStock)
            .Include(p => p.ProductGroup)
            .Include(p => p.ShoppingLocation)
            .Include(p => p.ParentProduct)
            .Include(p => p.ChildProducts)
            .Include(p => p.Barcodes)
            .Include(p => p.Images)
            .Where(p =>
                p.Name.ToLower().Contains(normalizedSearchTerm) ||
                (p.Description != null && p.Description.ToLower().Contains(normalizedSearchTerm)) ||
                (p.ProductGroup != null && p.ProductGroup.Name.ToLower().Contains(normalizedSearchTerm)) ||
                (p.ShoppingLocation != null && p.ShoppingLocation.Name.ToLower().Contains(normalizedSearchTerm)) ||
                p.Barcodes.Any(b => b.Barcode.Contains(normalizedSearchTerm)))
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<ProductDto>>(products);

        // Build lookup for image entities (to get TenantId for token generation)
        var productLookup = products.ToDictionary(p => p.Id);

        // Set computed URLs for images with access tokens
        foreach (var dto in dtos)
        {
            if (dto.Images != null && productLookup.TryGetValue(dto.Id, out var product))
            {
                SetImageUrls(dto.Images, product.Images.ToList(), dto.Id);
            }
        }

        return dtos;
    }

    // Autocomplete for inline add
    public async Task<List<ProductAutocompleteDto>> AutocompleteAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<ProductAutocompleteDto>();

        var normalizedSearch = searchTerm.ToLower();

        var products = await _context.Products
            .Where(p => p.IsActive && p.Name.ToLower().Contains(normalizedSearch))
            .Include(p => p.ProductGroup)
            .Include(p => p.Images.Where(i => i.IsPrimary).Take(1))
            .Include(p => p.MasterProduct)
                .ThenInclude(mp => mp!.Images)
            .Include(p => p.StoreMetadata.Take(1))
            .OrderByDescending(p => p.ChildProducts.Count)
            .ThenBy(p => p.Name)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return products.Select(p =>
        {
            var primaryImage = p.Images.FirstOrDefault();
            string? imageUrl = null;
            if (primaryImage != null)
            {
                // Use external URL directly if available, otherwise generate local download URL
                if (!string.IsNullOrEmpty(primaryImage.ExternalThumbnailUrl))
                    imageUrl = primaryImage.ExternalThumbnailUrl;
                else if (!string.IsNullOrEmpty(primaryImage.ExternalUrl))
                    imageUrl = primaryImage.ExternalUrl;
                else
                {
                    var token = _tokenService.GenerateToken("product-image", primaryImage.Id, primaryImage.TenantId);
                    imageUrl = _fileStorage.GetProductImageUrl(p.Id, primaryImage.Id, token);
                }
            }

            // Fallback to master product image
            if (imageUrl == null && p.MasterProduct != null)
            {
                var mp = p.MasterProduct;
                var mpPrimary = mp.Images?.FirstOrDefault(i => i.IsPrimary);
                imageUrl = _imageResolver.GetImageUrl(mp.ImageSlug, mpPrimary != null, mp.Id, mpPrimary?.Id);
            }

            var storeMetadata = p.StoreMetadata.FirstOrDefault();

            return new ProductAutocompleteDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                ProductGroupName = p.ProductGroup?.Name,
                PrimaryImageUrl = imageUrl,
                PreferredStoreAisle = storeMetadata?.Aisle,
                PreferredStoreDepartment = storeMetadata?.Department
            };
        }).ToList();
    }

    // Create product from external lookup data
    public async Task<ProductDto> CreateFromLookupAsync(CreateProductFromLookupRequest request, CancellationToken cancellationToken = default)
    {
        // Generate all barcode variants for duplicate checking and storage
        var allVariants = new HashSet<BarcodeVariant>();
        var inputBarcodes = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Barcode))
            inputBarcodes.Add(request.Barcode);
        if (!string.IsNullOrWhiteSpace(request.OriginalSearchBarcode))
            inputBarcodes.Add(request.OriginalSearchBarcode);

        foreach (var inputBarcode in inputBarcodes)
        {
            var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(inputBarcode);
            foreach (var variant in variants)
                allVariants.Add(variant);
        }

        // Check for duplicate by any barcode variant
        if (allVariants.Count > 0)
        {
            var variantStrings = allVariants.Select(v => v.Barcode).ToList();
            var existing = await _context.ProductBarcodes
                .Include(pb => pb.Product)
                .FirstOrDefaultAsync(pb => variantStrings.Contains(pb.Barcode), cancellationToken);

            if (existing != null)
            {
                return await GetByIdAsync(existing.ProductId, cancellationToken)
                    ?? throw new InvalidOperationException("Failed to retrieve existing product");
            }
        }

        // Look up or create ProductGroup by category name
        Guid? productGroupId = null;
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var productGroup = await _context.ProductGroups
                .FirstOrDefaultAsync(pg => pg.Name.ToLower() == request.Category.ToLower(), cancellationToken);

            if (productGroup == null)
            {
                productGroup = new ProductGroup
                {
                    Id = Guid.NewGuid(),
                    Name = request.Category
                };
                _context.ProductGroups.Add(productGroup);
                await _context.SaveChangesAsync(cancellationToken);
            }

            productGroupId = productGroup.Id;
        }

        // Get tenant defaults for required FK fields
        var defaultLocation = await _context.Locations.FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("No locations configured. Please create at least one location first.");
        var defaultUnit = await _context.QuantityUnits.FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("No quantity units configured. Please create at least one quantity unit first.");

        // Build description with brand
        var description = request.Description;
        if (!string.IsNullOrWhiteSpace(request.Brand) && string.IsNullOrWhiteSpace(description))
        {
            description = request.Brand;
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = description,
            LocationId = defaultLocation.Id,
            QuantityUnitIdPurchase = defaultUnit.Id,
            QuantityUnitIdStock = defaultUnit.Id,
            QuantityUnitFactorPurchaseToStock = 1.0m,
            IsActive = true,
            ProductGroupId = productGroupId,
            ShoppingLocationId = request.ShoppingLocationId
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        // Add all barcode variants for maximum scanning compatibility
        if (allVariants.Count > 0)
        {
            foreach (var variant in allVariants)
            {
                _context.ProductBarcodes.Add(new ProductBarcode
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Barcode = variant.Barcode,
                    Note = variant.Note
                });
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Add image from URL if provided
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            await AddImageFromUrlAsync(product.Id, request.ImageUrl, cancellationToken);
        }

        // Add store metadata if shopping location and store info provided
        if (request.ShoppingLocationId.HasValue &&
            (!string.IsNullOrEmpty(request.Aisle) || !string.IsNullOrEmpty(request.Department) || request.Price.HasValue))
        {
            _context.ProductStoreMetadata.Add(new ProductStoreMetadata
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ShoppingLocationId = request.ShoppingLocationId.Value,
                ExternalProductId = request.ExternalId,
                LastKnownPrice = request.Price,
                PriceUpdatedAt = request.Price.HasValue ? DateTime.UtcNow : null,
                Aisle = request.Aisle,
                Shelf = request.Shelf,
                Department = request.Department
            });
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Create review TodoItem
        var sourceLabel = !string.IsNullOrWhiteSpace(request.SourceType) ? request.SourceType : "external lookup";
        _context.TodoItems.Add(new TodoItem
        {
            Id = Guid.NewGuid(),
            TaskType = TaskType.Product,
            DateEntered = DateTime.UtcNow,
            Reason = $"Review product: {request.Name} (added from {sourceLabel})",
            RelatedEntityId = product.Id,
            RelatedEntityType = "Product"
        });
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve created product");
    }

    // Create product from free-text name
    public async Task<ProductDto> CreateFromFreeTextAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required");

        // Check for existing active product by exact name match
        var existingProduct = await _context.Products
            .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower().Trim() && p.IsActive, cancellationToken);

        if (existingProduct != null)
        {
            return await GetByIdAsync(existingProduct.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to retrieve existing product");
        }

        // Get tenant defaults for required FK fields
        var defaultLocation = await _context.Locations.FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("No locations configured. Please create at least one location first.");
        var defaultUnit = await _context.QuantityUnits.FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("No quantity units configured. Please create at least one quantity unit first.");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            LocationId = defaultLocation.Id,
            QuantityUnitIdPurchase = defaultUnit.Id,
            QuantityUnitIdStock = defaultUnit.Id,
            QuantityUnitFactorPurchaseToStock = 1.0m,
            IsActive = true
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve created product");
    }

    // Private helper methods

    /// <summary>
    /// Sets URLs with access tokens for product images.
    /// Matches DTOs with entities to get TenantId for token generation.
    /// </summary>
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

    private async Task ValidateForeignKeysAsync(
        Guid locationId,
        Guid quantityUnitIdPurchase,
        Guid quantityUnitIdStock,
        Guid? productGroupId,
        Guid? shoppingLocationId,
        CancellationToken cancellationToken)
    {
        var locationExists = await _context.Locations.AnyAsync(l => l.Id == locationId, cancellationToken);
        if (!locationExists)
        {
            throw new EntityNotFoundException(nameof(Location), locationId);
        }

        var purchaseUnitExists = await _context.QuantityUnits.AnyAsync(qu => qu.Id == quantityUnitIdPurchase, cancellationToken);
        if (!purchaseUnitExists)
        {
            throw new EntityNotFoundException(nameof(QuantityUnit), quantityUnitIdPurchase);
        }

        var stockUnitExists = await _context.QuantityUnits.AnyAsync(qu => qu.Id == quantityUnitIdStock, cancellationToken);
        if (!stockUnitExists)
        {
            throw new EntityNotFoundException(nameof(QuantityUnit), quantityUnitIdStock);
        }

        if (productGroupId.HasValue)
        {
            var productGroupExists = await _context.ProductGroups.AnyAsync(pg => pg.Id == productGroupId.Value, cancellationToken);
            if (!productGroupExists)
            {
                throw new EntityNotFoundException(nameof(ProductGroup), productGroupId.Value);
            }
        }

        if (shoppingLocationId.HasValue)
        {
            var shoppingLocationExists = await _context.ShoppingLocations.AnyAsync(sl => sl.Id == shoppingLocationId.Value, cancellationToken);
            if (!shoppingLocationExists)
            {
                throw new EntityNotFoundException(nameof(ShoppingLocation), shoppingLocationId.Value);
            }
        }
    }

    private async Task<List<(Guid ProductId, decimal CurrentStock, decimal MinStockAmount)>> GetCurrentStockLevelsAsync(CancellationToken cancellationToken)
    {
        var stockLevels = await _context.Stock
            .GroupBy(s => s.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                CurrentStock = g.Sum(s => s.Amount)
            })
            .ToListAsync(cancellationToken);

        var products = await _context.Products
            .Select(p => new { p.Id, p.MinStockAmount })
            .ToListAsync(cancellationToken);

        return products
            .Select(p => (
                ProductId: p.Id,
                CurrentStock: stockLevels.FirstOrDefault(s => s.ProductId == p.Id)?.CurrentStock ?? 0,
                MinStockAmount: p.MinStockAmount))
            .ToList();
    }

    public async Task<List<ParentProductSearchResultDto>> SearchParentProductsAsync(
        string searchTerm, CancellationToken cancellationToken = default)
    {
        var results = new List<ParentProductSearchResultDto>();

        // 1. Tenant products matching search term
        var tenantProducts = await _context.Products
            .Where(p => p.IsActive && p.Name.ToLower().Contains(searchTerm.ToLower()))
            .Include(p => p.ProductGroup)
            .Include(p => p.ChildProducts)
            .Include(p => p.MasterProduct)
                .ThenInclude(mp => mp!.Images)
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .OrderBy(p => p.Name)
            .Take(25)
            .ToListAsync(cancellationToken);

        var tenantNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in tenantProducts)
        {
            tenantNames.Add(p.Name);

            // Resolve image: tenant's own primary image > master product image > null
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
                    mp.ImageSlug,
                    masterPrimaryImage != null,
                    mp.Id,
                    masterPrimaryImage?.Id);
            }

            results.Add(new ParentProductSearchResultDto
            {
                Id = p.Id,
                Name = p.Name,
                ProductGroupName = p.ProductGroup?.Name,
                ChildProductCount = p.ChildProducts?.Count(c => c.IsActive) ?? 0,
                Source = "tenant",
                ImageUrl = imageUrl
            });
        }

        // 2. Master catalog products not already in tenant (top-level only)
        var masterProducts = await _context.MasterProducts
            .IgnoreQueryFilters()
            .Include(mp => mp.Images)
            .Where(mp => mp.ParentMasterProductId == null &&
                         mp.Name.ToLower().Contains(searchTerm.ToLower()))
            .OrderBy(mp => mp.Name)
            .Take(25)
            .ToListAsync(cancellationToken);

        foreach (var mp in masterProducts)
        {
            if (tenantNames.Contains(mp.Name)) continue;

            var primaryImage = mp.Images?.FirstOrDefault(i => i.IsPrimary);
            results.Add(new ParentProductSearchResultDto
            {
                Id = mp.Id,
                Name = mp.Name,
                ProductGroupName = mp.Category,
                ChildProductCount = 0,
                Source = "master",
                MasterProductId = mp.Id,
                ImageUrl = _imageResolver.GetImageUrl(
                    mp.ImageSlug,
                    primaryImage != null,
                    mp.Id,
                    primaryImage?.Id)
            });
        }

        return results.OrderBy(r => r.Name).Take(50).ToList();
    }

    public async Task<ProductDto> EnsureProductFromMasterAsync(
        Guid masterProductId, CancellationToken cancellationToken = default)
    {
        // Check if tenant already has a product linked to this master product
        var existing = await _context.Products
            .Include(p => p.Location)
            .Include(p => p.QuantityUnitPurchase)
            .Include(p => p.QuantityUnitStock)
            .Include(p => p.ProductGroup)
            .Include(p => p.ParentProduct)
            .Include(p => p.ChildProducts)
            .Include(p => p.Barcodes)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.MasterProductId == masterProductId && p.IsActive, cancellationToken);

        if (existing != null)
            return _mapper.Map<ProductDto>(existing);

        // Load master product
        var master = await _context.MasterProducts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(mp => mp.Id == masterProductId, cancellationToken)
            ?? throw new KeyNotFoundException($"Master product {masterProductId} not found");

        // Get default location and quantity unit
        var defaultLocation = await _context.Locations
            .Where(l => l.IsActive)
            .FirstOrDefaultAsync(l => l.Name == (master.DefaultLocationHint ?? "Pantry"), cancellationToken)
            ?? await _context.Locations.Where(l => l.IsActive).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No locations configured");

        var defaultUnit = await _context.QuantityUnits
            .Where(u => u.IsActive)
            .FirstOrDefaultAsync(u => u.Name == (master.DefaultQuantityUnitHint ?? "Piece"), cancellationToken)
            ?? await _context.QuantityUnits.Where(u => u.IsActive).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No quantity units configured");

        // Find or create product group
        Guid? productGroupId = null;
        if (!string.IsNullOrEmpty(master.Category))
        {
            var group = await _context.ProductGroups
                .FirstOrDefaultAsync(g => g.Name == master.Category, cancellationToken);
            if (group == null)
            {
                group = new ProductGroup
                {
                    Id = Guid.NewGuid(),
                    Name = master.Category
                };
                _context.ProductGroups.Add(group);
            }
            productGroupId = group.Id;
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = master.Name,
            Description = master.Description,
            MasterProductId = master.Id,
            OverriddenFields = "[]",
            LocationId = defaultLocation.Id,
            QuantityUnitIdPurchase = defaultUnit.Id,
            QuantityUnitIdStock = defaultUnit.Id,
            QuantityUnitFactorPurchaseToStock = 1.0m,
            MinStockAmount = 0,
            DefaultBestBeforeDays = master.DefaultBestBeforeDays,
            TracksBestBeforeDate = master.TracksBestBeforeDate,
            ServingSize = master.ServingSize,
            ServingUnit = master.ServingUnit,
            ServingsPerContainer = master.ServingsPerContainer,
            DataSourceAttribution = master.DataSourceAttribution,
            IsActive = true,
            ProductGroupId = productGroupId
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> ShareAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .Include(p => p.Barcodes)
            .Include(p => p.Nutrition)
            .Include(p => p.ProductGroup)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        // Already shared - return current product
        if (product.MasterProductId.HasValue)
        {
            return await GetByIdAsync(productId, cancellationToken)
                ?? throw new InvalidOperationException("Failed to retrieve product");
        }

        var categoryName = product.ProductGroup?.Name ?? "Uncategorized";

        // Check if a matching MasterProduct already exists
        var masterProduct = await _context.MasterProducts
            .IgnoreQueryFilters()
            .Include(mp => mp.Barcodes)
            .Include(mp => mp.Nutrition)
            .FirstOrDefaultAsync(mp =>
                mp.Name == product.Name &&
                mp.Category == categoryName &&
                mp.Brand == product.Brand,
                cancellationToken);

        if (masterProduct == null)
        {
            // Create a new MasterProduct from the tenant product
            masterProduct = new MasterProduct
            {
                Id = Guid.NewGuid(),
                Name = product.Name,
                Description = product.Description,
                Brand = product.Brand,
                Category = categoryName,
                DefaultBestBeforeDays = product.DefaultBestBeforeDays,
                TracksBestBeforeDate = product.TracksBestBeforeDate,
                ServingSize = product.ServingSize,
                ServingUnit = product.ServingUnit,
                ServingsPerContainer = product.ServingsPerContainer,
                DataSourceAttribution = product.DataSourceAttribution,
                Popularity = 3,
                IsStaple = false,
                LifestyleTags = "[]",
                AllergenFlags = "[]",
                DietaryConflictFlags = "[]",
                OrganicScore = 3,
                ConvenienceScore = 3,
                HealthScore = 3
            };

            _context.MasterProducts.Add(masterProduct);
        }

        // Promote barcodes: add any that don't already exist in master
        var existingMasterBarcodes = masterProduct.Barcodes
            .Select(b => b.Barcode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var barcode in product.Barcodes)
        {
            if (!existingMasterBarcodes.Contains(barcode.Barcode))
            {
                _context.MasterProductBarcodes.Add(new MasterProductBarcode
                {
                    Id = Guid.NewGuid(),
                    MasterProductId = masterProduct.Id,
                    Barcode = barcode.Barcode,
                    Note = barcode.Note
                });
            }
        }

        // Promote nutrition if product has it and master doesn't
        if (product.Nutrition != null && masterProduct.Nutrition == null)
        {
            var nutrition = product.Nutrition;
            _context.MasterProductNutrition.Add(new MasterProductNutrition
            {
                Id = Guid.NewGuid(),
                MasterProductId = masterProduct.Id,
                ExternalId = nutrition.ExternalId,
                DataSource = nutrition.DataSource,
                ServingSize = nutrition.ServingSize,
                ServingUnit = nutrition.ServingUnit,
                ServingsPerContainer = nutrition.ServingsPerContainer,
                Calories = nutrition.Calories,
                TotalFat = nutrition.TotalFat,
                SaturatedFat = nutrition.SaturatedFat,
                TransFat = nutrition.TransFat,
                Cholesterol = nutrition.Cholesterol,
                Sodium = nutrition.Sodium,
                TotalCarbohydrates = nutrition.TotalCarbohydrates,
                DietaryFiber = nutrition.DietaryFiber,
                TotalSugars = nutrition.TotalSugars,
                AddedSugars = nutrition.AddedSugars,
                Protein = nutrition.Protein,
                VitaminA = nutrition.VitaminA,
                VitaminC = nutrition.VitaminC,
                VitaminD = nutrition.VitaminD,
                VitaminE = nutrition.VitaminE,
                VitaminK = nutrition.VitaminK,
                Thiamin = nutrition.Thiamin,
                Riboflavin = nutrition.Riboflavin,
                Niacin = nutrition.Niacin,
                VitaminB6 = nutrition.VitaminB6,
                Folate = nutrition.Folate,
                VitaminB12 = nutrition.VitaminB12,
                Calcium = nutrition.Calcium,
                Iron = nutrition.Iron,
                Magnesium = nutrition.Magnesium,
                Phosphorus = nutrition.Phosphorus,
                Potassium = nutrition.Potassium,
                Zinc = nutrition.Zinc,
                BrandOwner = nutrition.BrandOwner,
                BrandName = nutrition.BrandName,
                Ingredients = nutrition.Ingredients,
                ServingSizeDescription = nutrition.ServingSizeDescription,
                LastUpdatedFromSource = nutrition.LastUpdatedFromSource
            });
        }

        // Link the tenant product to the master product
        product.MasterProductId = masterProduct.Id;
        product.OverriddenFields = "[]";

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(productId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve shared product");
    }

    private static StockStatus DetermineStockStatus(decimal currentStock, decimal minStockAmount)
    {
        if (currentStock == 0)
        {
            return StockStatus.OutOfStock;
        }

        if (minStockAmount > 0 && currentStock < minStockAmount)
        {
            return StockStatus.Low;
        }

        return StockStatus.OK;
    }
}
