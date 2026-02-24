using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Services;

public class StockService : IStockService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly ITenantProvider _tenantProvider;

    public StockService(HomeManagementDbContext context, IMapper mapper, ITenantProvider tenantProvider)
    {
        _context = context;
        _mapper = mapper;
        _tenantProvider = tenantProvider;
    }

    public async Task<StockEntryDto> AddStockAsync(AddStockRequest request, CancellationToken cancellationToken = default)
    {
        // Validate product exists
        var product = await _context.Products
            .Include(p => p.QuantityUnitStock)
            .Include(p => p.Barcodes)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product == null)
        {
            throw new EntityNotFoundException(nameof(Product), request.ProductId);
        }

        // Validate location if provided
        if (request.LocationId.HasValue)
        {
            var locationExists = await _context.Locations
                .AnyAsync(l => l.Id == request.LocationId.Value, cancellationToken);
            if (!locationExists)
            {
                throw new EntityNotFoundException(nameof(Location), request.LocationId.Value);
            }
        }

        var entry = new StockEntry
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantProvider.TenantId ?? throw new InvalidOperationException("Tenant ID not set"),
            ProductId = request.ProductId,
            Amount = request.Amount,
            BestBeforeDate = request.BestBeforeDate,
            PurchasedDate = request.PurchasedDate ?? DateTime.UtcNow,
            StockId = Guid.NewGuid().ToString(),
            Price = request.Price,
            Open = false,
            LocationId = request.LocationId ?? product.LocationId,
            ShoppingLocationId = request.ShoppingLocationId,
            Note = request.Note
        };

        _context.Stock.Add(entry);

        // Create stock log entry
        var log = CreateStockLog(entry, "purchase", request.Amount);
        _context.StockLog.Add(log);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entry.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve created stock entry");
    }

    public async Task<List<StockEntryDto>> AddStockBatchAsync(AddStockBatchRequest request, CancellationToken cancellationToken = default)
    {
        // Validate product exists
        var product = await _context.Products
            .Include(p => p.QuantityUnitStock)
            .Include(p => p.Barcodes)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product == null)
        {
            throw new EntityNotFoundException(nameof(Product), request.ProductId);
        }

        // Validate location if provided
        if (request.LocationId.HasValue)
        {
            var locationExists = await _context.Locations
                .AnyAsync(l => l.Id == request.LocationId.Value, cancellationToken);
            if (!locationExists)
            {
                throw new EntityNotFoundException(nameof(Location), request.LocationId.Value);
            }
        }

        var tenantId = _tenantProvider.TenantId ?? throw new InvalidOperationException("Tenant ID not set");
        var purchasedDate = request.PurchasedDate ?? DateTime.UtcNow;
        var locationId = request.LocationId ?? product.LocationId;
        var entries = new List<StockEntry>();

        // Create individual entries (Amount=1 each)
        if (request.IndividualItems?.Count > 0)
        {
            foreach (var item in request.IndividualItems)
            {
                var entry = new StockEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = request.ProductId,
                    Amount = 1,
                    BestBeforeDate = item.BestBeforeDate,
                    PurchasedDate = purchasedDate,
                    StockId = Guid.NewGuid().ToString(),
                    Price = request.Price,
                    Open = false,
                    LocationId = locationId,
                    ShoppingLocationId = request.ShoppingLocationId,
                    Note = item.Note ?? request.Note
                };

                entries.Add(entry);
                _context.Stock.Add(entry);

                var log = CreateStockLog(entry, "purchase", 1);
                _context.StockLog.Add(log);
            }
        }

        // Create bulk entry if specified
        if (request.BulkAmount.HasValue && request.BulkAmount > 0)
        {
            var entry = new StockEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = request.ProductId,
                Amount = request.BulkAmount.Value,
                BestBeforeDate = request.BulkBestBeforeDate,
                PurchasedDate = purchasedDate,
                StockId = Guid.NewGuid().ToString(),
                Price = request.Price,
                Open = false,
                LocationId = locationId,
                ShoppingLocationId = request.ShoppingLocationId,
                Note = request.Note
            };

            entries.Add(entry);
            _context.Stock.Add(entry);

            var log = CreateStockLog(entry, "purchase", request.BulkAmount.Value);
            _context.StockLog.Add(log);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Return the created entries with full product info
        var createdIds = entries.Select(e => e.Id).ToList();
        var result = new List<StockEntryDto>();
        foreach (var id in createdIds)
        {
            var dto = await GetByIdAsync(id, cancellationToken);
            if (dto != null) result.Add(dto);
        }

        return result;
    }

    public async Task<StockEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Stock
            .Include(s => s.Product)
                .ThenInclude(p => p!.QuantityUnitStock)
            .Include(s => s.Product)
                .ThenInclude(p => p!.Barcodes)
            .Include(s => s.Location)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (entry == null) return null;

        return MapToDto(entry);
    }

    public async Task<List<StockEntryDto>> ListAsync(StockFilterRequest? filter = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Stock
            .Include(s => s.Product)
                .ThenInclude(p => p!.QuantityUnitStock)
            .Include(s => s.Product)
                .ThenInclude(p => p!.Barcodes)
            .Include(s => s.Location)
            .AsQueryable();

        if (filter != null)
        {
            if (filter.ProductId.HasValue)
                query = query.Where(s => s.ProductId == filter.ProductId.Value);

            if (filter.LocationId.HasValue)
                query = query.Where(s => s.LocationId == filter.LocationId.Value);

            if (filter.Open.HasValue)
                query = query.Where(s => s.Open == filter.Open.Value);

            if (filter.ExpiredOnly == true)
                query = query.Where(s => s.BestBeforeDate.HasValue && s.BestBeforeDate.Value < DateTime.UtcNow);

            if (filter.ExpiringWithinDays.HasValue)
            {
                var expiryDate = DateTime.UtcNow.AddDays(filter.ExpiringWithinDays.Value);
                query = query.Where(s => s.BestBeforeDate.HasValue && s.BestBeforeDate.Value <= expiryDate);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.ToLower();
                query = query.Where(s => s.Product != null && s.Product.Name.ToLower().Contains(term));
            }

            // Sorting
            query = filter.SortBy?.ToLower() switch
            {
                "amount" => filter.Descending ? query.OrderByDescending(s => s.Amount) : query.OrderBy(s => s.Amount),
                "bestbeforedate" => filter.Descending ? query.OrderByDescending(s => s.BestBeforeDate) : query.OrderBy(s => s.BestBeforeDate),
                "purchaseddate" => filter.Descending ? query.OrderByDescending(s => s.PurchasedDate) : query.OrderBy(s => s.PurchasedDate),
                "productname" => filter.Descending ? query.OrderByDescending(s => s.Product!.Name) : query.OrderBy(s => s.Product!.Name),
                _ => query.OrderBy(s => s.BestBeforeDate).ThenBy(s => s.PurchasedDate) // Default: FEFO then FIFO
            };
        }
        else
        {
            query = query.OrderBy(s => s.BestBeforeDate).ThenBy(s => s.PurchasedDate);
        }

        var entries = await query.ToListAsync(cancellationToken);
        return entries.Select(MapToDto).ToList();
    }

    public async Task<List<StockEntryDto>> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await ListAsync(new StockFilterRequest { ProductId = productId }, cancellationToken);
    }

    public async Task<List<StockEntryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        return await ListAsync(new StockFilterRequest { LocationId = locationId }, cancellationToken);
    }

    public async Task<List<StockEntryDto>> GetByProductAndLocationAsync(Guid productId, Guid locationId, CancellationToken cancellationToken = default)
    {
        return await ListAsync(new StockFilterRequest { ProductId = productId, LocationId = locationId }, cancellationToken);
    }

    public async Task<StockEntryDto> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Stock
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (entry == null)
        {
            throw new EntityNotFoundException(nameof(StockEntry), id);
        }

        // Log old values
        var oldLog = CreateStockLog(entry, "stock-edit-old", entry.Amount);
        _context.StockLog.Add(oldLog);

        // Update entry
        var oldAmount = entry.Amount;
        entry.Amount = request.Amount;

        if (request.BestBeforeDate.HasValue)
            entry.BestBeforeDate = request.BestBeforeDate;

        if (request.LocationId.HasValue)
            entry.LocationId = request.LocationId;

        if (request.Note != null)
            entry.Note = request.Note;

        entry.UpdatedAt = DateTime.UtcNow;

        // Log new values
        var newLog = CreateStockLog(entry, "stock-edit-new", entry.Amount);
        newLog.CorrelationId = oldLog.CorrelationId;
        _context.StockLog.Add(newLog);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve updated stock entry");
    }

    public async Task<StockEntryDto> OpenProductAsync(Guid id, OpenProductRequest request, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Stock
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (entry == null)
        {
            throw new EntityNotFoundException(nameof(StockEntry), id);
        }

        if (entry.Open)
        {
            throw new InvalidOperationException("Stock entry is already open");
        }

        // Store original amount before opening
        entry.OriginalAmount = entry.Amount;
        entry.Open = true;
        entry.OpenedDate = DateTime.UtcNow;
        entry.OpenTrackingMode = request.TrackingMode;
        entry.Amount = request.RemainingAmount;
        entry.UpdatedAt = DateTime.UtcNow;

        // Create log entry
        var log = CreateStockLog(entry, "product-opened", request.RemainingAmount);
        _context.StockLog.Add(log);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve updated stock entry");
    }

    public async Task ConsumeStockAsync(Guid id, ConsumeStockRequest request, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Stock
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (entry == null)
        {
            throw new EntityNotFoundException(nameof(StockEntry), id);
        }

        var consumeAmount = request.Amount ?? entry.Amount;

        if (consumeAmount > entry.Amount)
        {
            throw new InsufficientStockException(entry.ProductId, consumeAmount, entry.Amount);
        }

        // Create log entry
        var log = CreateStockLog(entry, "consume", -consumeAmount);
        log.Spoiled = request.Spoiled ? 1 : 0;
        log.RecipeId = request.RecipeId;
        log.UsedDate = DateTime.UtcNow;
        _context.StockLog.Add(log);

        if (consumeAmount >= entry.Amount)
        {
            // Remove the entire entry
            _context.Stock.Remove(entry);
        }
        else
        {
            // Reduce the amount
            entry.Amount -= consumeAmount;
            entry.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Stock.FindAsync(new object[] { id }, cancellationToken);

        if (entry == null)
        {
            throw new EntityNotFoundException(nameof(StockEntry), id);
        }

        // Create inventory correction log
        var log = CreateStockLog(entry, "inventory-correction", -entry.Amount);
        _context.StockLog.Add(log);

        _context.Stock.Remove(entry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private StockLog CreateStockLog(StockEntry entry, string transactionType, decimal amount)
    {
        var userId = _tenantProvider.UserId;
        if (!userId.HasValue)
        {
            throw new InvalidOperationException(
                $"User ID not available from authentication context. " +
                $"TenantId={_tenantProvider.TenantId}, TransactionType={transactionType}, ProductId={entry.ProductId}. " +
                $"This may indicate an expired or invalid authentication token - try logging out and back in.");
        }

        return new StockLog
        {
            Id = Guid.NewGuid(),
            TenantId = entry.TenantId,
            ProductId = entry.ProductId,
            Amount = amount,
            BestBeforeDate = entry.BestBeforeDate,
            PurchasedDate = entry.PurchasedDate,
            StockId = entry.StockId,
            TransactionType = transactionType,
            Price = entry.Price,
            LocationId = entry.LocationId,
            ShoppingLocationId = entry.ShoppingLocationId,
            StockRowId = entry.Id,
            OpenedDate = entry.OpenedDate,
            CorrelationId = Guid.NewGuid().ToString(),
            Note = entry.Note,
            UserId = userId.Value
        };
    }

    private StockEntryDto MapToDto(StockEntry entry)
    {
        return new StockEntryDto
        {
            Id = entry.Id,
            ProductId = entry.ProductId,
            ProductName = entry.Product?.Name ?? string.Empty,
            ProductBarcode = entry.Product?.Barcodes?.FirstOrDefault()?.Barcode,
            Amount = entry.Amount,
            BestBeforeDate = entry.BestBeforeDate,
            PurchasedDate = entry.PurchasedDate,
            StockId = entry.StockId,
            Price = entry.Price,
            Open = entry.Open,
            OpenedDate = entry.OpenedDate,
            OpenTrackingMode = entry.OpenTrackingMode,
            OriginalAmount = entry.OriginalAmount,
            LocationId = entry.LocationId,
            LocationName = entry.Location?.Name,
            ShoppingLocationId = entry.ShoppingLocationId,
            Note = entry.Note,
            QuantityUnitName = entry.Product?.QuantityUnitStock?.Name ?? string.Empty,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt ?? entry.CreatedAt
        };
    }

    public async Task<StockStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var dueSoonDate = today.AddDays(5);

        // Get all stock entries with product info for statistics
        var stockEntries = await _context.Stock
            .Include(s => s.Product)
                .ThenInclude(p => p!.ParentProduct)
            .ToListAsync(cancellationToken);

        // Get all parent products (products with children)
        var parentProducts = await _context.Products
            .Include(p => p.ChildProducts)
            .Where(p => p.ChildProducts.Any())
            .ToListAsync(cancellationToken);

        var parentProductIds = parentProducts.Select(p => p.Id).ToHashSet();
        var childProductIds = stockEntries
            .Where(s => s.Product?.ParentProductId != null)
            .Select(s => s.ProductId)
            .Distinct()
            .ToHashSet();

        // Count for parent products (aggregate child stock)
        var parentStats = parentProducts.Select(parent =>
        {
            var childIds = parent.ChildProducts.Select(c => c.Id).ToList();
            var childStock = stockEntries.Where(s => childIds.Contains(s.ProductId)).ToList();

            if (childStock.Count == 0)
                return (Counted: false, HasExpired: false, IsDueSoon: false, IsBelowMin: false, TotalValue: 0m);

            var totalAmount = childStock.Sum(s => s.Amount);
            var minStock = parent.MinStockAmount;

            return (
                Counted: true,
                HasExpired: childStock.Any(s => s.BestBeforeDate.HasValue && s.BestBeforeDate.Value < today),
                IsDueSoon: childStock.Any(s => s.BestBeforeDate.HasValue && s.BestBeforeDate.Value >= today && s.BestBeforeDate.Value <= dueSoonDate),
                IsBelowMin: minStock > 0 && totalAmount < minStock,
                TotalValue: childStock.Sum(s => (s.Price ?? 0) * s.Amount)
            );
        }).Where(x => x.Counted).ToList();

        // Count for standalone products (not a child, not a parent)
        var standaloneGroups = stockEntries
            .Where(s => !childProductIds.Contains(s.ProductId) && !parentProductIds.Contains(s.ProductId))
            .GroupBy(s => s.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                TotalAmount = g.Sum(s => s.Amount),
                TotalValue = g.Sum(s => (s.Price ?? 0) * s.Amount),
                MinStockAmount = g.First().Product?.MinStockAmount ?? 0,
                HasExpired = g.Any(s => s.BestBeforeDate.HasValue && s.BestBeforeDate.Value < today),
                IsDueSoon = g.Any(s => s.BestBeforeDate.HasValue && s.BestBeforeDate.Value >= today && s.BestBeforeDate.Value <= dueSoonDate)
            })
            .ToList();

        // Combine counts
        var totalProductCount = parentStats.Count + standaloneGroups.Count;
        var totalStockValue = parentStats.Sum(p => p.TotalValue) + standaloneGroups.Sum(p => p.TotalValue);
        var expiredCount = parentStats.Count(p => p.HasExpired) + standaloneGroups.Count(p => p.HasExpired);
        var dueSoonCount = parentStats.Count(p => p.IsDueSoon && !p.HasExpired) + standaloneGroups.Count(p => p.IsDueSoon && !p.HasExpired);
        var belowMinCount = parentStats.Count(p => p.IsBelowMin) + standaloneGroups.Count(p => p.MinStockAmount > 0 && p.TotalAmount < p.MinStockAmount);

        return new StockStatisticsDto
        {
            TotalProductCount = totalProductCount,
            TotalStockValue = totalStockValue,
            ExpiredCount = expiredCount,
            OverdueCount = 0, // TODO: Implement based on product's DefaultConsumeWithinDays
            DueSoonCount = dueSoonCount,
            BelowMinStockCount = belowMinCount
        };
    }

    public async Task<List<StockOverviewItemDto>> GetOverviewAsync(StockOverviewFilterRequest? filter = null, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var dueSoonDate = today.AddDays(5);

        var query = _context.Stock
            .Include(s => s.Product)
                .ThenInclude(p => p!.QuantityUnitStock)
            .Include(s => s.Product)
                .ThenInclude(p => p!.ProductGroup)
            .Include(s => s.Product)
                .ThenInclude(p => p!.ParentProduct)
            .Include(s => s.Product)
                .ThenInclude(p => p!.ChildProducts)
            .Include(s => s.Product)
                .ThenInclude(p => p!.Images)
            .Include(s => s.Location)
            .AsQueryable();

        // Apply location filter
        if (filter?.LocationId.HasValue == true)
            query = query.Where(s => s.LocationId == filter.LocationId.Value);

        // Apply product group filter
        if (filter?.ProductGroupId.HasValue == true)
            query = query.Where(s => s.Product != null && s.Product.ProductGroupId == filter.ProductGroupId.Value);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(filter?.SearchTerm))
        {
            var term = filter.SearchTerm.ToLower();
            query = query.Where(s => s.Product != null && s.Product.Name.ToLower().Contains(term));
        }

        var entries = await query.ToListAsync(cancellationToken);

        // Get all products that are parents (have children with stock)
        var productsWithChildren = await _context.Products
            .Include(p => p.ChildProducts)
            .Include(p => p.QuantityUnitStock)
            .Include(p => p.ProductGroup)
            .Include(p => p.Images)
            .Where(p => p.ChildProducts.Any())
            .ToListAsync(cancellationToken);

        // Get child product IDs that have a parent
        var childProductIds = entries
            .Where(s => s.Product?.ParentProductId != null)
            .Select(s => s.ProductId)
            .Distinct()
            .ToHashSet();

        // Group stock entries by product
        var stockByProduct = entries
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<StockOverviewItemDto>();

        // Process parent products (aggregate stock from children)
        foreach (var parent in productsWithChildren)
        {
            // Get all child product IDs for this parent
            var childIds = parent.ChildProducts.Select(c => c.Id).ToList();

            // Get all stock entries for children
            var childStockEntries = entries.Where(s => childIds.Contains(s.ProductId)).ToList();

            if (childStockEntries.Count == 0)
                continue; // No stock in children, skip

            // Aggregate child stock
            var totalAmount = childStockEntries.Sum(s => s.Amount);
            var earliestExpiry = childStockEntries.Min(s => s.BestBeforeDate);
            var hasExpired = childStockEntries.Any(s => s.BestBeforeDate.HasValue && s.BestBeforeDate.Value < today);
            var isDueSoon = earliestExpiry.HasValue && earliestExpiry.Value >= today && earliestExpiry.Value <= dueSoonDate;
            var minStock = parent.MinStockAmount;

            // Build child product list for expandable view
            var childProducts = childStockEntries
                .GroupBy(s => s.ProductId)
                .Select(cg =>
                {
                    var childProduct = cg.First().Product;
                    var childEarliestExpiry = cg.Min(s => s.BestBeforeDate);
                    var childHasExpired = cg.Any(s => s.BestBeforeDate.HasValue && s.BestBeforeDate.Value < today);
                    var childIsDueSoon = childEarliestExpiry.HasValue && childEarliestExpiry.Value >= today && childEarliestExpiry.Value <= dueSoonDate;

                    return new StockOverviewChildDto
                    {
                        ProductId = cg.Key,
                        ProductName = childProduct?.Name ?? string.Empty,
                        TotalAmount = cg.Sum(s => s.Amount),
                        QuantityUnitName = childProduct?.QuantityUnitStock?.Name ?? string.Empty,
                        NextDueDate = childEarliestExpiry,
                        DaysUntilDue = childEarliestExpiry.HasValue ? (int)(childEarliestExpiry.Value - today).TotalDays : null,
                        IsExpired = childHasExpired,
                        IsDueSoon = childIsDueSoon && !childHasExpired,
                        PrimaryImageUrl = GetPrimaryImageUrl(childProduct?.Images, cg.Key)
                    };
                })
                .OrderBy(c => c.NextDueDate ?? DateTime.MaxValue)
                .ThenBy(c => c.ProductName)
                .ToList();

            result.Add(new StockOverviewItemDto
            {
                ProductId = parent.Id,
                ProductName = parent.Name,
                ProductGroupId = parent.ProductGroupId,
                ProductGroupName = parent.ProductGroup?.Name,
                TotalAmount = totalAmount,
                QuantityUnitName = parent.QuantityUnitStock?.Name ?? string.Empty,
                NextDueDate = earliestExpiry,
                DaysUntilDue = earliestExpiry.HasValue ? (int)(earliestExpiry.Value - today).TotalDays : null,
                TotalValue = childStockEntries.Sum(s => (s.Price ?? 0) * s.Amount),
                MinStockAmount = minStock,
                IsBelowMinStock = minStock > 0 && totalAmount < minStock,
                IsExpired = hasExpired,
                IsDueSoon = isDueSoon && !hasExpired,
                StockEntryCount = childStockEntries.Count,
                IsParentProduct = true,
                ChildProductCount = childProducts.Count,
                ChildProducts = childProducts,
                PrimaryImageUrl = GetPrimaryImageUrl(parent.Images, parent.Id)
            });
        }

        // Process standalone products (not a child of any parent)
        var standaloneProductGroups = entries
            .Where(s => !childProductIds.Contains(s.ProductId))
            .GroupBy(s => s.ProductId)
            .Select(g =>
            {
                var product = g.First().Product;

                // Skip if this product is a parent (already processed above)
                if (productsWithChildren.Any(p => p.Id == g.Key))
                    return null;

                var totalAmount = g.Sum(s => s.Amount);
                var minStock = product?.MinStockAmount ?? 0;
                var earliestExpiry = g.Min(s => s.BestBeforeDate);
                var hasExpired = g.Any(s => s.BestBeforeDate.HasValue && s.BestBeforeDate.Value < today);
                var isDueSoon = earliestExpiry.HasValue && earliestExpiry.Value >= today && earliestExpiry.Value <= dueSoonDate;

                return new StockOverviewItemDto
                {
                    ProductId = g.Key,
                    ProductName = product?.Name ?? string.Empty,
                    ProductGroupId = product?.ProductGroupId,
                    ProductGroupName = product?.ProductGroup?.Name,
                    TotalAmount = totalAmount,
                    QuantityUnitName = product?.QuantityUnitStock?.Name ?? string.Empty,
                    NextDueDate = earliestExpiry,
                    DaysUntilDue = earliestExpiry.HasValue ? (int)(earliestExpiry.Value - today).TotalDays : null,
                    TotalValue = g.Sum(s => (s.Price ?? 0) * s.Amount),
                    MinStockAmount = minStock,
                    IsBelowMinStock = minStock > 0 && totalAmount < minStock,
                    IsExpired = hasExpired,
                    IsDueSoon = isDueSoon && !hasExpired,
                    StockEntryCount = g.Count(),
                    IsParentProduct = false,
                    ChildProductCount = 0,
                    ChildProducts = null,
                    PrimaryImageUrl = GetPrimaryImageUrl(product?.Images, g.Key)
                };
            })
            .Where(x => x != null)
            .Cast<StockOverviewItemDto>()
            .ToList();

        result.AddRange(standaloneProductGroups);

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(filter?.Status))
        {
            result = filter.Status.ToLower() switch
            {
                "expired" => result.Where(p => p.IsExpired).ToList(),
                "duesoon" => result.Where(p => p.IsDueSoon).ToList(),
                "belowminstock" => result.Where(p => p.IsBelowMinStock).ToList(),
                _ => result
            };
        }

        // Apply sorting
        result = filter?.SortBy?.ToLower() switch
        {
            "productname" => filter.Descending ? result.OrderByDescending(p => p.ProductName).ToList() : result.OrderBy(p => p.ProductName).ToList(),
            "amount" => filter.Descending ? result.OrderByDescending(p => p.TotalAmount).ToList() : result.OrderBy(p => p.TotalAmount).ToList(),
            "nextduedate" => filter.Descending ? result.OrderByDescending(p => p.NextDueDate).ToList() : result.OrderBy(p => p.NextDueDate).ToList(),
            "value" => filter.Descending ? result.OrderByDescending(p => p.TotalValue).ToList() : result.OrderBy(p => p.TotalValue).ToList(),
            _ => result.OrderBy(p => p.NextDueDate ?? DateTime.MaxValue).ThenBy(p => p.ProductName).ToList()
        };

        return result;
    }

    public async Task<List<StockLogDto>> GetLogAsync(int? limit = 50, CancellationToken cancellationToken = default)
    {
        var query = _context.StockLog
            .Include(l => l.Product)
            .Include(l => l.Location)
            .OrderByDescending(l => l.CreatedAt)
            .AsQueryable();

        if (limit.HasValue)
            query = query.Take(limit.Value);

        var logs = await query.ToListAsync(cancellationToken);

        return logs.Select(l => new StockLogDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ProductName = l.Product?.Name ?? string.Empty,
            Amount = l.Amount,
            TransactionType = l.TransactionType,
            CreatedAt = l.CreatedAt,
            LocationName = l.Location?.Name,
            Note = l.Note,
            Spoiled = l.Spoiled == 1
        }).ToList();
    }

    public async Task QuickConsumeAsync(QuickConsumeRequest request, CancellationToken cancellationToken = default)
    {
        // Get stock entries for product, ordered by FEFO (First Expire First Out)
        var entries = await _context.Stock
            .Include(s => s.Product)
            .Where(s => s.ProductId == request.ProductId)
            .OrderBy(s => s.Open ? 0 : 1) // Opened first
            .ThenBy(s => s.BestBeforeDate ?? DateTime.MaxValue) // Then earliest expiry
            .ThenBy(s => s.PurchasedDate) // Then FIFO
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            throw new EntityNotFoundException(nameof(Product), request.ProductId);
        }

        if (request.ConsumeAll)
        {
            // Consume all entries
            foreach (var entry in entries)
            {
                var log = CreateStockLog(entry, "consume", -entry.Amount);
                if (request.Spoiled) log.Spoiled = 1;
                _context.StockLog.Add(log);
                _context.Stock.Remove(entry);
            }
        }
        else
        {
            // Consume specified amount using FEFO
            var remainingToConsume = request.Amount;
            foreach (var entry in entries)
            {
                if (remainingToConsume <= 0) break;

                var consumeFromThis = Math.Min(remainingToConsume, entry.Amount);
                var log = CreateStockLog(entry, "consume", -consumeFromThis);
                if (request.Spoiled) log.Spoiled = 1;
                _context.StockLog.Add(log);

                if (consumeFromThis >= entry.Amount)
                {
                    _context.Stock.Remove(entry);
                }
                else
                {
                    entry.Amount -= consumeFromThis;
                    entry.UpdatedAt = DateTime.UtcNow;
                }

                remainingToConsume -= consumeFromThis;
            }

            if (remainingToConsume > 0)
            {
                var totalAvailable = entries.Sum(e => e.Amount);
                throw new InsufficientStockException(request.ProductId, request.Amount, totalAvailable);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task QuickAddAsync(Guid productId, decimal amount = 1, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product == null)
        {
            throw new EntityNotFoundException(nameof(Product), productId);
        }

        var request = new AddStockRequest
        {
            ProductId = productId,
            Amount = amount,
            LocationId = product.LocationId,
            PurchasedDate = DateTime.UtcNow,
            BestBeforeDate = product.DefaultBestBeforeDays > 0
                ? DateTime.UtcNow.AddDays(product.DefaultBestBeforeDays)
                : null
        };

        await AddStockAsync(request, cancellationToken);
    }

    private static string? GetPrimaryImageUrl(ICollection<ProductImage>? images, Guid productId)
    {
        if (images == null || images.Count == 0) return null;

        var primary = images.FirstOrDefault(i => i.IsPrimary) ?? images.FirstOrDefault();
        if (primary == null) return null;

        if (!string.IsNullOrEmpty(primary.ExternalThumbnailUrl))
            return primary.ExternalThumbnailUrl;
        if (!string.IsNullOrEmpty(primary.ExternalUrl))
            return primary.ExternalUrl;
        if (!string.IsNullOrEmpty(primary.FileName))
            return $"/api/v1/products/{productId}/images/{primary.Id}/download";

        return null;
    }
}
