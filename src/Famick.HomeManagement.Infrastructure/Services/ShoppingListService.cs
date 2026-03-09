using System.Text.Json;
using System.Text.RegularExpressions;
using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Core.DTOs.StoreIntegrations;
using Famick.HomeManagement.Core.DTOs.TodoItems;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public partial class ShoppingListService : IShoppingListService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<ShoppingListService> _logger;
    private readonly IStoreIntegrationService _storeIntegrationService;
    private readonly IPluginLoader _pluginLoader;
    private readonly IStockService _stockService;
    private readonly ITodoItemService _todoItemService;
    private readonly IProductsService _productsService;
    private readonly IFileAccessTokenService _tokenService;
    private readonly IFileStorageService _fileStorage;

    public ShoppingListService(
        HomeManagementDbContext context,
        IMapper mapper,
        ILogger<ShoppingListService> logger,
        IStoreIntegrationService storeIntegrationService,
        IPluginLoader pluginLoader,
        IStockService stockService,
        ITodoItemService todoItemService,
        IProductsService productsService,
        IFileAccessTokenService tokenService,
        IFileStorageService fileStorage)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
        _storeIntegrationService = storeIntegrationService;
        _pluginLoader = pluginLoader;
        _stockService = stockService;
        _todoItemService = todoItemService;
        _productsService = productsService;
        _tokenService = tokenService;
        _fileStorage = fileStorage;
    }

    [GeneratedRegex(@"aisle\s*[-:]?\s*(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex AisleRegex();

    // List management
    public async Task<ShoppingListDto> CreateListAsync(
        CreateShoppingListRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating shopping list: {Name}", request.Name);

        var shoppingList = _mapper.Map<ShoppingList>(request);
        shoppingList.Id = Guid.NewGuid();

        _context.ShoppingLists.Add(shoppingList);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created shopping list: {Id} - {Name}", shoppingList.Id, shoppingList.Name);

        return _mapper.Map<ShoppingListDto>(shoppingList);
    }

    public async Task<ShoppingListDto?> GetListByIdAsync(
        Guid id,
        bool includeItems = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ShoppingLists
            .Include(sl => sl.ShoppingLocation)
            .AsQueryable();

        if (includeItems)
        {
            query = query
                .Include(sl => sl.Items!)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.QuantityUnitPurchase)
                .Include(sl => sl.Items!)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Images)
                .Include(sl => sl.Items!)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.ChildProducts)
                            .ThenInclude(cp => cp.StoreMetadata);
        }

        var shoppingList = await query.FirstOrDefaultAsync(sl => sl.Id == id, cancellationToken);

        if (shoppingList == null)
            return null;

        var dto = _mapper.Map<ShoppingListDto>(shoppingList);

        // Populate ImageUrl from product images for items that don't already have one
        if (includeItems && dto.Items != null && shoppingList.Items != null)
        {
            var entityLookup = shoppingList.Items.ToDictionary(i => i.Id);
            foreach (var item in dto.Items.Where(i => string.IsNullOrEmpty(i.ImageUrl) && i.ProductId.HasValue))
            {
                if (entityLookup.TryGetValue(item.Id, out var entity) && entity.Product?.Images != null)
                {
                    var primaryImage = entity.Product.Images
                        .OrderByDescending(img => img.IsPrimary)
                        .ThenBy(img => img.SortOrder)
                        .FirstOrDefault();

                    if (primaryImage != null)
                    {
                        // Prefer external URL (OpenFoodFacts, etc.) over local download URL
                        if (!string.IsNullOrEmpty(primaryImage.ExternalUrl))
                        {
                            item.ImageUrl = primaryImage.ExternalUrl;
                        }
                        else
                        {
                            var token = _tokenService.GenerateToken("product-image", primaryImage.Id, primaryImage.TenantId);
                            item.ImageUrl = _fileStorage.GetProductImageUrl(item.ProductId.Value, primaryImage.Id, token);
                        }
                    }
                }
            }
        }

        // Populate child product info using eagerly-loaded navigation properties
        if (includeItems && dto.Items != null && shoppingList.Items != null)
        {
            var storeId = shoppingList.ShoppingLocationId;
            var entityLookup = shoppingList.Items.ToDictionary(i => i.Id);

            // Build a lookup for free-text items: resolve product by exact name match
            // (Items added without a ProductId link need to be matched to products)
            var freeTextNames = dto.Items
                .Where(i => !i.ProductId.HasValue && !string.IsNullOrEmpty(i.ProductName))
                .Select(i => i.ProductName!)
                .Distinct()
                .ToList();

            Dictionary<string, Product> nameToProduct = new();
            if (freeTextNames.Count > 0)
            {
                var matchedProducts = await _context.Products
                    .Include(p => p.ChildProducts)
                        .ThenInclude(cp => cp.StoreMetadata)
                    .Where(p => freeTextNames.Contains(p.Name))
                    .ToListAsync(cancellationToken);

                foreach (var mp in matchedProducts)
                    nameToProduct.TryAdd(mp.Name, mp);
            }

            foreach (var itemDto in dto.Items)
            {
                // Get the product entity: from the eager-loaded item, or from name lookup
                Product? product = null;
                if (entityLookup.TryGetValue(itemDto.Id, out var itemEntity))
                    product = itemEntity.Product;

                if (product == null && itemDto.ProductName != null)
                    nameToProduct.TryGetValue(itemDto.ProductName, out product);

                if (product == null || product.ChildProducts.Count == 0)
                    continue;

                // Link ProductId on the DTO if it was resolved by name
                if (!itemDto.ProductId.HasValue)
                    itemDto.ProductId = product.Id;

                itemDto.IsParentProduct = true;
                itemDto.HasChildren = true;
                itemDto.ChildProductCount = product.ChildProducts.Count;
                itemDto.HasChildrenAtStore = storeId != Guid.Empty
                    && product.ChildProducts.Any(cp =>
                        cp.StoreMetadata.Any(m => m.ShoppingLocationId == storeId));

                // Parse child purchases from entity
                if (itemEntity != null)
                {
                    var purchases = ParseChildPurchases(itemEntity.ChildPurchasesJson);
                    itemDto.ChildPurchases = purchases;
                    itemDto.ChildPurchasedQuantity = purchases.Sum(p => p.Quantity);
                }
            }
        }

        // Apply custom aisle ordering if items exist
        if (dto.Items != null && dto.Items.Count > 0 && shoppingList.ShoppingLocationId != Guid.Empty)
        {
            dto.Items = await SortItemsByAisleOrderAsync(
                dto.Items,
                shoppingList.ShoppingLocationId,
                cancellationToken);
        }

        return dto;
    }

    public async Task<List<ShoppingListSummaryDto>> ListAllAsync(
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[ShoppingListService] ListAllAsync: Starting query...");
        var shoppingLists = await _context.ShoppingLists
            .Include(sl => sl.ShoppingLocation)
            .Include(sl => sl.Items)
            .OrderByDescending(sl => sl.UpdatedAt)
            .ToListAsync(cancellationToken);

        Console.WriteLine($"[ShoppingListService] ListAllAsync: Query returned {shoppingLists.Count} lists, mapping...");
        var result = _mapper.Map<List<ShoppingListSummaryDto>>(shoppingLists);
        Console.WriteLine("[ShoppingListService] ListAllAsync: Mapping complete.");
        return result;
    }

    public async Task<List<ShoppingListSummaryDto>> ListByStoreAsync(
        Guid shoppingLocationId,
        CancellationToken cancellationToken = default)
    {
        var shoppingLists = await _context.ShoppingLists
            .Include(sl => sl.ShoppingLocation)
            .Include(sl => sl.Items)
            .Where(sl => sl.ShoppingLocationId == shoppingLocationId)
            .OrderByDescending(sl => sl.UpdatedAt)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<ShoppingListSummaryDto>>(shoppingLists);
    }

    public async Task<ShoppingListDashboardDto> GetDashboardSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var shoppingLists = await _context.ShoppingLists
            .Include(sl => sl.ShoppingLocation)
            .Include(sl => sl.Items)
            .OrderBy(sl => sl.ShoppingLocation!.Name)
            .ThenByDescending(sl => sl.UpdatedAt)
            .ToListAsync(cancellationToken);

        var grouped = shoppingLists
            .GroupBy(sl => new
            {
                sl.ShoppingLocationId,
                LocationName = sl.ShoppingLocation?.Name ?? "Unknown",
                HasIntegration = sl.ShoppingLocation != null && !string.IsNullOrEmpty(sl.ShoppingLocation.IntegrationType)
            })
            .Select(g => new StoreShoppingListSummary
            {
                ShoppingLocationId = g.Key.ShoppingLocationId,
                ShoppingLocationName = g.Key.LocationName,
                HasIntegration = g.Key.HasIntegration,
                Lists = _mapper.Map<List<ShoppingListSummaryDto>>(g.ToList()),
                TotalItems = g.Sum(sl => sl.Items?.Count ?? 0)
            })
            .ToList();

        var totalUnpurchased = shoppingLists.Sum(sl => sl.Items?.Count(i => !i.IsPurchased) ?? 0);

        return new ShoppingListDashboardDto
        {
            StoresSummary = grouped,
            TotalItems = grouped.Sum(g => g.TotalItems),
            UnpurchasedItems = totalUnpurchased,
            TotalLists = shoppingLists.Count
        };
    }

    public async Task<ShoppingListDto> UpdateListAsync(
        Guid id,
        UpdateShoppingListRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating shopping list: {Id}", id);

        var shoppingList = await _context.ShoppingLists.FindAsync(new object[] { id }, cancellationToken);
        if (shoppingList == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingList), id);
        }

        _mapper.Map(request, shoppingList);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated shopping list: {Id} - {Name}", id, request.Name);

        // Reload with items for DTO mapping
        shoppingList = await _context.ShoppingLists
            .Include(sl => sl.Items!)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p!.ShoppingLocation)
            .FirstAsync(sl => sl.Id == id, cancellationToken);

        return _mapper.Map<ShoppingListDto>(shoppingList);
    }

    public async Task DeleteListAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting shopping list: {Id}", id);

        var shoppingList = await _context.ShoppingLists.FindAsync(new object[] { id }, cancellationToken);
        if (shoppingList == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingList), id);
        }

        _context.ShoppingLists.Remove(shoppingList);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted shopping list: {Id}", id);
    }

    // Item management
    public async Task<ShoppingListItemDto> AddItemAsync(
        Guid listId,
        AddShoppingListItemRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding item to shopping list: {ListId}", listId);

        // Verify list exists
        var list = await _context.ShoppingLists.FindAsync(new object[] { listId }, cancellationToken);
        if (list == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingList), listId);
        }

        // Verify product exists if provided
        if (request.ProductId.HasValue)
        {
            var product = await _context.Products.FindAsync(new object[] { request.ProductId.Value }, cancellationToken);
            if (product == null)
            {
                throw new EntityNotFoundException(nameof(Product), request.ProductId.Value);
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.ProductName))
        {
            // Auto-create product from free-text name
            var createdProduct = await _productsService.CreateFromFreeTextAsync(request.ProductName, cancellationToken);
            request.ProductId = createdProduct.Id;
        }

        var item = _mapper.Map<ShoppingListItem>(request);
        item.Id = Guid.NewGuid();
        item.ShoppingListId = listId;

        _context.ShoppingListItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added item: {Id} to shopping list: {ListId}", item.Id, listId);

        // Reload with navigation properties
        item = await _context.ShoppingListItems
            .Include(i => i.Product)
                .ThenInclude(p => p!.ShoppingLocation)
            .FirstAsync(i => i.Id == item.Id, cancellationToken);

        return _mapper.Map<ShoppingListItemDto>(item);
    }

    public async Task<ShoppingListItemDto> UpdateItemAsync(
        Guid itemId,
        UpdateShoppingListItemRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating shopping list item: {ItemId}", itemId);

        var item = await _context.ShoppingListItems.FindAsync(new object[] { itemId }, cancellationToken);
        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        _mapper.Map(request, item);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated shopping list item: {ItemId}", itemId);

        // Reload with navigation properties
        item = await _context.ShoppingListItems
            .Include(i => i.Product)
                .ThenInclude(p => p!.ShoppingLocation)
            .FirstAsync(i => i.Id == itemId, cancellationToken);

        return _mapper.Map<ShoppingListItemDto>(item);
    }

    public async Task RemoveItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing shopping list item: {ItemId}", itemId);

        var item = await _context.ShoppingListItems.FindAsync(new object[] { itemId }, cancellationToken);
        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        _context.ShoppingListItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Removed shopping list item: {ItemId}", itemId);
    }

    public async Task MarkItemAsPurchasedAsync(
        Guid itemId,
        MarkItemPurchasedRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Marking item as purchased: {ItemId}", itemId);

        var item = await _context.ShoppingListItems.FindAsync(new object[] { itemId }, cancellationToken);
        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        // TODO: Future enhancement - create stock entry when StockService is available
        // For now, just remove the item from the list
        _context.ShoppingListItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Marked item as purchased (removed): {ItemId}", itemId);
    }

    public async Task ClearPurchasedItemsAsync(
        Guid listId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing purchased items from shopping list: {ListId}", listId);

        // Verify list exists
        var list = await _context.ShoppingLists.FindAsync(new object[] { listId }, cancellationToken);
        if (list == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingList), listId);
        }

        // Remove all purchased items from the list
        var purchasedItems = await _context.ShoppingListItems
            .Where(i => i.ShoppingListId == listId && i.IsPurchased)
            .ToListAsync(cancellationToken);

        _context.ShoppingListItems.RemoveRange(purchasedItems);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleared {Count} purchased items from shopping list: {ListId}",
            purchasedItems.Count, listId);
    }

    public async Task<ClearPurchasedPreviewDto> GetClearPurchasedPreviewAsync(
        Guid listId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting clear purchased preview for shopping list: {ListId}", listId);

        // Verify list exists
        var list = await _context.ShoppingLists.FindAsync(new object[] { listId }, cancellationToken);
        if (list == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingList), listId);
        }

        // Get all purchased items with product info
        var purchasedItems = await _context.ShoppingListItems
            .Include(i => i.Product)
                .ThenInclude(p => p!.QuantityUnitPurchase)
            .Include(i => i.Product)
                .ThenInclude(p => p!.Location)
            .Where(i => i.ShoppingListId == listId && i.IsPurchased)
            .ToListAsync(cancellationToken);

        var result = new ClearPurchasedPreviewDto();

        foreach (var item in purchasedItems)
        {
            var previewItem = new ClearPurchasedItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? item.ProductName ?? "Unknown",
                Amount = item.Amount,
                QuantityUnitName = item.Product?.QuantityUnitPurchase?.Name,
                TracksBestBeforeDate = item.Product?.TracksBestBeforeDate ?? false,
                DefaultBestBeforeDays = item.Product?.DefaultBestBeforeDays ?? 0,
                DefaultLocationId = item.Product?.LocationId,
                DefaultLocationName = item.Product?.Location?.Name,
                BestBeforeDate = item.BestBeforeDate,
                ImageUrl = item.ImageUrl,
                Barcode = item.Barcode,
                // Pre-fill UI binding properties
                SelectedLocationId = item.Product?.LocationId,
                SelectedBestBeforeDate = item.BestBeforeDate
            };

            // Calculate default best before date if not already set
            if (previewItem.TracksBestBeforeDate && previewItem.SelectedBestBeforeDate == null && previewItem.DefaultBestBeforeDays > 0)
            {
                previewItem.SelectedBestBeforeDate = DateTime.UtcNow.Date.AddDays(previewItem.DefaultBestBeforeDays);
            }

            if (item.ProductId.HasValue)
            {
                result.ItemsWithProducts.Add(previewItem);
            }
            else
            {
                result.ItemsWithoutProducts.Add(previewItem);
            }
        }

        _logger.LogInformation("Found {WithProducts} items with products and {WithoutProducts} items without products",
            result.InventoryItemCount, result.TodoItemCount);

        return result;
    }

    // Smart features
    public async Task<List<ProductSuggestionDto>> SuggestProductsAsync(
        Guid listId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Suggesting products for shopping list: {ListId}", listId);

        // Verify list exists
        var list = await _context.ShoppingLists.FindAsync(new object[] { listId }, cancellationToken);
        if (list == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingList), listId);
        }

        // Get current stock levels
        var stockLevels = await _context.Stock
            .GroupBy(s => s.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                CurrentStock = g.Sum(s => s.Amount)
            })
            .ToListAsync(cancellationToken);

        // Get products with MinStockAmount threshold
        var products = await _context.Products
            .Where(p => p.MinStockAmount > 0)
            .ToListAsync(cancellationToken);

        var suggestions = new List<ProductSuggestionDto>();

        foreach (var product in products)
        {
            var stock = stockLevels.FirstOrDefault(s => s.ProductId == product.Id);
            var currentStock = stock?.CurrentStock ?? 0;

            // Suggest if below minimum
            if (currentStock < product.MinStockAmount)
            {
                var suggestedAmount = product.MinStockAmount - currentStock;
                var reason = currentStock == 0 ? "Out of stock" : "Below minimum";

                suggestions.Add(new ProductSuggestionDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    CurrentStock = currentStock,
                    MinStockAmount = product.MinStockAmount,
                    SuggestedAmount = suggestedAmount,
                    Reason = reason
                });
            }
        }

        _logger.LogInformation("Found {Count} product suggestions for shopping list: {ListId}",
            suggestions.Count, listId);

        return suggestions.OrderBy(s => s.CurrentStock).ToList();
    }

    public async Task<ShoppingListByLocationDto> GroupItemsByLocationAsync(
        Guid listId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Grouping items by location for shopping list: {ListId}", listId);

        var shoppingList = await _context.ShoppingLists
            .Include(sl => sl.Items!)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p!.ShoppingLocation)
            .FirstOrDefaultAsync(sl => sl.Id == listId, cancellationToken);

        if (shoppingList == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingList), listId);
        }

        var items = shoppingList.Items ?? new List<ShoppingListItem>();

        // Group items by shopping location
        var grouped = items
            .GroupBy(i => new
            {
                LocationId = i.Product?.ShoppingLocationId,
                LocationName = i.Product?.ShoppingLocation?.Name ?? "Unassigned"
            })
            .Select(g => new LocationItemGroup
            {
                ShoppingLocationId = g.Key.LocationId,
                ShoppingLocationName = g.Key.LocationName,
                Items = _mapper.Map<List<ShoppingListItemDto>>(g.ToList())
            })
            .OrderBy(g => g.ShoppingLocationName)
            .ToList();

        var result = new ShoppingListByLocationDto
        {
            ShoppingListId = listId,
            ShoppingListName = shoppingList.Name,
            ItemsByLocation = grouped
        };

        _logger.LogInformation("Grouped {ItemCount} items into {GroupCount} location groups",
            items.Count, grouped.Count);

        return result;
    }

    // Quick add from Stock Overview or barcode scan
    public async Task<ShoppingListItemDto> QuickAddItemAsync(
        AddToShoppingListRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Quick adding item to shopping list: {ListId}", request.ShoppingListId);

        // Verify list exists and get its ShoppingLocation
        var list = await _context.ShoppingLists
            .Include(sl => sl.ShoppingLocation)
            .FirstOrDefaultAsync(sl => sl.Id == request.ShoppingListId, cancellationToken);

        if (list == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingList), request.ShoppingListId);
        }

        Guid? productId = request.ProductId;

        // If barcode provided, look up product
        if (!productId.HasValue && !string.IsNullOrWhiteSpace(request.Barcode))
        {
            var productByBarcode = await _context.ProductBarcodes
                .Include(pb => pb.Product)
                .FirstOrDefaultAsync(pb => pb.Barcode == request.Barcode, cancellationToken);

            if (productByBarcode != null)
            {
                productId = productByBarcode.ProductId;
            }
            else
            {
                _logger.LogWarning("Product not found for barcode: {Barcode}", request.Barcode);
                throw new DomainException($"No product found with barcode: {request.Barcode}");
            }
        }

        // If no product found but ProductName provided, auto-create product
        if (!productId.HasValue && !string.IsNullOrWhiteSpace(request.ProductName))
        {
            var createdProduct = await _productsService.CreateFromFreeTextAsync(request.ProductName, cancellationToken);
            productId = createdProduct.Id;
        }

        if (!productId.HasValue && string.IsNullOrWhiteSpace(request.ProductName))
        {
            throw new DomainException("Either ProductId, valid Barcode, or ProductName must be provided");
        }

        // Create the item
        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = request.ShoppingListId,
            ProductId = productId,
            ProductName = request.ProductName,
            Amount = request.Amount,
            Note = request.Note,
            IsPurchased = request.IsPurchased,
            PurchasedAt = request.IsPurchased ? DateTime.UtcNow : null,
            // Use provided store info if available
            Aisle = request.Aisle,
            Department = request.Department,
            ExternalProductId = request.ExternalProductId,
            ImageUrl = request.ImageUrl,
            Price = request.Price
        };

        // If store info not provided and lookup is requested, look up product in store
        var hasStoreInfo = !string.IsNullOrEmpty(request.Aisle) ||
                           !string.IsNullOrEmpty(request.Department) ||
                           !string.IsNullOrEmpty(request.ExternalProductId);

        if (!hasStoreInfo && request.LookupInStore && list.ShoppingLocation?.IntegrationType != null)
        {
            await TryPopulateStoreInfoAsync(item, list.ShoppingLocation, cancellationToken);
        }

        _context.ShoppingListItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Quick added item: {Id} to shopping list: {ListId}", item.Id, request.ShoppingListId);

        // Reload with navigation properties
        item = await _context.ShoppingListItems
            .Include(i => i.Product)
                .ThenInclude(p => p!.QuantityUnitPurchase)
            .FirstAsync(i => i.Id == item.Id, cancellationToken);

        return _mapper.Map<ShoppingListItemDto>(item);
    }

    public async Task<ShoppingListItemDto> LookupItemInStoreAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Looking up item in store: {ItemId}", itemId);

        var item = await _context.ShoppingListItems
            .Include(i => i.ShoppingList)
                .ThenInclude(sl => sl!.ShoppingLocation)
            .Include(i => i.Product)
                .ThenInclude(p => p!.QuantityUnitPurchase)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        var shoppingLocation = item.ShoppingList?.ShoppingLocation;
        if (shoppingLocation?.IntegrationType == null)
        {
            throw new DomainException("Shopping list is not associated with a store integration");
        }

        await TryPopulateStoreInfoAsync(item, shoppingLocation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ShoppingListItemDto>(item);
    }

    public async Task<SendToCartResult> SendToCartAsync(
        Guid listId,
        SendToCartRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending items to cart for list: {ListId}", listId);

        var list = await _context.ShoppingLists
            .Include(sl => sl.ShoppingLocation)
            .Include(sl => sl.Items!)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(sl => sl.Id == listId, cancellationToken);

        if (list == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingList), listId);
        }

        var shoppingLocation = list.ShoppingLocation;
        if (shoppingLocation?.IntegrationType == null)
        {
            throw new DomainException("Shopping list is not associated with a store integration");
        }

        // Get the plugin
        var plugin = _pluginLoader.GetPlugin<IStoreIntegrationPlugin>(shoppingLocation.IntegrationType);
        if (plugin == null || !plugin.Capabilities.HasShoppingCart)
        {
            throw new DomainException("Store integration does not support shopping cart");
        }

        // Determine which items to send
        var itemsToSend = list.Items?
            .Where(i => !i.IsPurchased && !string.IsNullOrEmpty(i.ExternalProductId))
            .ToList() ?? new List<ShoppingListItem>();

        if (request.ItemIds.Count > 0)
        {
            itemsToSend = itemsToSend.Where(i => request.ItemIds.Contains(i.Id)).ToList();
        }

        var result = new SendToCartResult
        {
            Success = true,
            Details = new List<SendToCartItemResult>()
        };

        // Get access token
        var accessToken = await _storeIntegrationService.GetAccessTokenAsync(
            shoppingLocation.IntegrationType, cancellationToken);

        if (string.IsNullOrEmpty(accessToken))
        {
            return new SendToCartResult
            {
                Success = false,
                ItemsFailed = itemsToSend.Count,
                Details = itemsToSend.Select(i => new SendToCartItemResult
                {
                    ItemId = i.Id,
                    ProductName = i.Product?.Name ?? "Unknown",
                    Success = false,
                    ErrorMessage = "Failed to get access token for store integration"
                }).ToList()
            };
        }

        // Build the cart items list
        var cartItems = itemsToSend.Select(i => new CartItemRequest
        {
            ExternalProductId = i.ExternalProductId!,
            Quantity = (int)Math.Ceiling(i.Amount)
        }).ToList();

        try
        {
            var cartResult = await plugin.AddToCartAsync(
                accessToken,
                shoppingLocation.ExternalLocationId!,
                cartItems,
                cancellationToken);

            if (cartResult != null)
            {
                // All items were sent successfully
                foreach (var item in itemsToSend)
                {
                    result.ItemsSent++;
                    result.Details.Add(new SendToCartItemResult
                    {
                        ItemId = item.Id,
                        ProductName = item.Product?.Name ?? "Unknown",
                        Success = true
                    });
                }
            }
            else
            {
                // Failed to add to cart
                foreach (var item in itemsToSend)
                {
                    result.ItemsFailed++;
                    result.Details.Add(new SendToCartItemResult
                    {
                        ItemId = item.Id,
                        ProductName = item.Product?.Name ?? "Unknown",
                        Success = false,
                        ErrorMessage = "Failed to add to cart"
                    });
                }
                result.Success = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding items to cart");
            foreach (var item in itemsToSend)
            {
                result.ItemsFailed++;
                result.Details.Add(new SendToCartItemResult
                {
                    ItemId = item.Id,
                    ProductName = item.Product?.Name ?? "Unknown",
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
            result.Success = false;
        }

        _logger.LogInformation("Sent {Sent} items to cart, {Failed} failed", result.ItemsSent, result.ItemsFailed);

        return result;
    }

    public async Task<ScanPurchaseResultDto> ScanPurchaseAsync(
        Guid itemId,
        ScanPurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scan-purchase for item: {ItemId}, quantity: {Quantity}", itemId, request.Quantity);

        var item = await _context.ShoppingListItems
            .Include(i => i.Product)
                .ThenInclude(p => p!.QuantityUnitPurchase)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        var quantity = request.Quantity > 0 ? request.Quantity : 1;
        item.PurchasedQuantity += quantity;

        // Treat Amount <= 0 as 1 for completion logic
        var effectiveAmount = item.Amount > 0 ? item.Amount : 1;
        var isCompleted = item.PurchasedQuantity >= effectiveAmount;

        if (isCompleted && !item.IsPurchased)
        {
            item.IsPurchased = true;
            item.PurchasedAt = DateTime.UtcNow;

            // Handle best before date
            if (request.BestBeforeDate != null)
            {
                item.BestBeforeDate = request.BestBeforeDate;
            }
            else if (item.Product?.TracksBestBeforeDate == true && item.Product.DefaultBestBeforeDays > 0)
            {
                item.BestBeforeDate = DateTime.UtcNow.Date.AddDays(item.Product.DefaultBestBeforeDays);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Scan-purchase completed for item: {ItemId}, PurchasedQuantity: {PurchasedQuantity}, IsCompleted: {IsCompleted}",
            itemId, item.PurchasedQuantity, isCompleted);

        var dto = _mapper.Map<ShoppingListItemDto>(item);
        return new ScanPurchaseResultDto
        {
            Item = dto,
            IsCompleted = isCompleted,
            RemainingQuantity = effectiveAmount - item.PurchasedQuantity
        };
    }

    public async Task<ShoppingListItemDto> TogglePurchasedAsync(
        Guid itemId,
        MarkItemPurchasedRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Toggling purchased status for item: {ItemId}", itemId);

        var item = await _context.ShoppingListItems
            .Include(i => i.Product)
                .ThenInclude(p => p!.QuantityUnitPurchase)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        item.IsPurchased = !item.IsPurchased;
        item.PurchasedAt = item.IsPurchased ? DateTime.UtcNow : null;

        // Sync PurchasedQuantity with toggle state
        if (item.IsPurchased)
        {
            // When manually toggling ON, set PurchasedQuantity to match Amount
            item.PurchasedQuantity = Math.Max(item.Amount, 1);

            if (request?.BestBeforeDate != null)
            {
                item.BestBeforeDate = request.BestBeforeDate;
            }
            else if (item.Product?.TracksBestBeforeDate == true && item.Product.DefaultBestBeforeDays > 0)
            {
                item.BestBeforeDate = DateTime.UtcNow.Date.AddDays(item.Product.DefaultBestBeforeDays);
            }
        }
        else
        {
            // When toggling OFF, reset PurchasedQuantity
            item.PurchasedQuantity = 0;
            item.BestBeforeDate = null;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Toggled purchased status for item: {ItemId} to {IsPurchased}",
            itemId, item.IsPurchased);

        return _mapper.Map<ShoppingListItemDto>(item);
    }

    private async Task TryPopulateStoreInfoAsync(
        ShoppingListItem item,
        ShoppingLocation shoppingLocation,
        CancellationToken cancellationToken)
    {
        // Only require store integration, not a local product
        if (shoppingLocation.IntegrationType == null)
        {
            return;
        }

        try
        {
            // First check if we already have metadata for this product/store combo (only if we have a ProductId)
            if (item.ProductId.HasValue)
            {
                var existingMetadata = await _context.Set<ProductStoreMetadata>()
                    .FirstOrDefaultAsync(m =>
                        m.ProductId == item.ProductId &&
                        m.ShoppingLocationId == shoppingLocation.Id,
                        cancellationToken);

                if (existingMetadata != null)
                {
                    item.Aisle = NormalizeAisle(existingMetadata.Aisle);
                    item.Shelf = existingMetadata.Shelf;
                    item.Department = existingMetadata.Department;
                    item.ExternalProductId = existingMetadata.ExternalProductId;
                    return;
                }
            }

            // If item already has an ExternalProductId, use it directly to get updated store info
            if (!string.IsNullOrEmpty(item.ExternalProductId))
            {
                var productInfo = await _storeIntegrationService.GetProductAtStoreAsync(
                    shoppingLocation.Id,
                    item.ExternalProductId,
                    cancellationToken);

                if (productInfo != null)
                {
                    item.Aisle = NormalizeAisle(productInfo.Aisle);
                    item.Shelf = productInfo.Shelf;
                    item.Department = productInfo.Department;
                    item.ImageUrl = productInfo.ImageUrl;
                    item.Barcode = productInfo.Barcode;

                    _logger.LogInformation(
                        "Found store info for item via ExternalProductId {ExternalProductId}: Aisle={Aisle}, Dept={Department}, ImageUrl={ImageUrl}",
                        item.ExternalProductId, item.Aisle, item.Department, item.ImageUrl);
                    return;
                }
            }

            // Fall back to search by barcode/name only if we have a local product
            if (item.ProductId.HasValue)
            {
                var product = await _context.Products
                    .Include(p => p.Barcodes)
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken);

                if (product == null) return;

                var searchQuery = product.Barcodes?.FirstOrDefault()?.Barcode ?? product.Name;

                var searchRequest = new StoreProductSearchRequest
                {
                    Query = searchQuery,
                    MaxResults = 5
                };

                var searchResults = await _storeIntegrationService.SearchProductsAtStoreAsync(
                    shoppingLocation.Id,
                    searchRequest,
                    cancellationToken);

                if (searchResults.Count > 0)
                {
                    var bestMatch = searchResults.First();
                    item.Aisle = NormalizeAisle(bestMatch.Aisle);
                    item.Shelf = bestMatch.Shelf;
                    item.Department = bestMatch.Department;
                    item.ExternalProductId = bestMatch.ExternalProductId;
                    item.ImageUrl = bestMatch.ImageUrl;
                    item.Barcode = bestMatch.Barcode;

                    _logger.LogInformation(
                        "Found store info for product {ProductId}: Aisle={Aisle}, Dept={Department}, ImageUrl={ImageUrl}",
                        item.ProductId, item.Aisle, item.Department, item.ImageUrl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to look up store info for item: ProductId={ProductId}, ExternalProductId={ExternalProductId}",
                item.ProductId, item.ExternalProductId);
        }
    }

    private static string? NormalizeAisle(string? rawAisle)
    {
        if (string.IsNullOrWhiteSpace(rawAisle)) return null;

        // If aisle contains "aisle", extract just the number/identifier
        var match = AisleRegex().Match(rawAisle);
        return match.Success ? match.Groups[1].Value : rawAisle;
    }

    /// <summary>
    /// Sorts shopping list items by the store's custom aisle order, or default order if not configured.
    /// </summary>
    private async Task<List<ShoppingListItemDto>> SortItemsByAisleOrderAsync(
        List<ShoppingListItemDto> items,
        Guid shoppingLocationId,
        CancellationToken cancellationToken)
    {
        var location = await _context.ShoppingLocations
            .FirstOrDefaultAsync(sl => sl.Id == shoppingLocationId, cancellationToken);

        if (location?.AisleOrder == null || location.AisleOrder.Count == 0)
        {
            // Default sorting: numeric then alphabetical, unknown last
            return items
                .OrderBy(i => GetDefaultAisleSortKey(i.Aisle))
                .ThenBy(i => i.Department)
                .ThenBy(i => i.ProductName)
                .ToList();
        }

        // Custom sorting based on AisleOrder
        var aisleOrderMap = location.AisleOrder
            .Select((aisle, index) => new { aisle, index })
            .ToDictionary(x => x.aisle, x => x.index);

        return items
            .OrderBy(i => GetCustomAisleSortKey(i.Aisle, aisleOrderMap))
            .ThenBy(i => i.Department)
            .ThenBy(i => i.ProductName)
            .ToList();
    }

    /// <summary>
    /// Returns a sort key for default aisle ordering: numeric first (by value), then named, then unknown.
    /// </summary>
    private static (int priority, int numericValue, string text) GetDefaultAisleSortKey(string? aisle)
    {
        if (string.IsNullOrEmpty(aisle))
            return (2, 0, ""); // Unknown aisles last

        if (int.TryParse(aisle, out int num))
            return (0, num, ""); // Numeric aisles first

        return (1, 0, aisle); // Named aisles second
    }

    /// <summary>
    /// Returns a sort key for custom aisle ordering based on the configured order map.
    /// </summary>
    private static int GetCustomAisleSortKey(string? aisle, Dictionary<string, int> aisleOrderMap)
    {
        if (string.IsNullOrEmpty(aisle))
            return int.MaxValue; // Unknown aisles last

        if (aisleOrderMap.TryGetValue(aisle, out int order))
            return order;

        // Aisles not in custom order go after ordered ones
        return int.MaxValue - 1;
    }

    public async Task<MoveToInventoryResponse> MoveToInventoryAsync(
        MoveToInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Moving {Count} items to inventory from shopping list {ListId}",
            request.Items.Count, request.ShoppingListId);

        var response = new MoveToInventoryResponse();
        var itemIdsToRemove = new List<Guid>();

        foreach (var item in request.Items)
        {
            try
            {
                if (item.ProductId.HasValue)
                {
                    // Item has a product - add to stock
                    var addStockRequest = new AddStockRequest
                    {
                        ProductId = item.ProductId.Value,
                        Amount = item.Amount,
                        Price = item.Price,
                        PurchasedDate = DateTime.UtcNow,
                        BestBeforeDate = item.BestBeforeDate,
                        LocationId = item.LocationId
                    };

                    var stockEntry = await _stockService.AddStockAsync(addStockRequest, cancellationToken);
                    response.StockEntryIds.Add(stockEntry.Id);
                    response.ItemsAddedToStock++;

                    _logger.LogInformation("Added stock entry {StockEntryId} for product {ProductId}",
                        stockEntry.Id, item.ProductId);

                    // Update existing product with additional metadata if available
                    await UpdateProductWithShoppingMetadataAsync(
                        item.ProductId.Value,
                        item.Barcode,
                        item.ImageUrl,
                        item.ExternalProductId,
                        item.ShoppingLocationId,
                        item.Aisle,
                        item.Shelf,
                        item.Department,
                        item.Price,
                        cancellationToken);
                }
                else
                {
                    // Item has no product - create a basic product and a TODO to review it
                    // Get "Kitchen" location (or first available as fallback)
                    var defaultLocation = await _context.Locations
                        .FirstOrDefaultAsync(l => l.Name == "Kitchen", cancellationToken)
                        ?? await _context.Locations
                            .OrderBy(l => l.Name)
                            .FirstOrDefaultAsync(cancellationToken);

                    var defaultQuantityUnit = await _context.QuantityUnits
                        .OrderBy(q => q.Name)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (defaultLocation == null || defaultQuantityUnit == null)
                    {
                        _logger.LogWarning("Cannot create product - no default location or quantity unit exists");
                        response.Errors.Add($"Cannot create product '{item.ProductName}': No default location or quantity unit configured");
                        continue;
                    }

                    // Create the product with basic info - assume all new products track expiration dates
                    var createProductRequest = new CreateProductRequest
                    {
                        Name = item.ProductName ?? "Unknown product",
                        LocationId = defaultLocation.Id,
                        QuantityUnitIdPurchase = defaultQuantityUnit.Id,
                        QuantityUnitIdStock = defaultQuantityUnit.Id,
                        QuantityUnitFactorPurchaseToStock = 1.0m,
                        TracksBestBeforeDate = true,
                        IsActive = true
                    };

                    var newProduct = await _productsService.CreateAsync(createProductRequest, cancellationToken);

                    _logger.LogInformation("Created product {ProductId} '{ProductName}' from shopping item",
                        newProduct.Id, newProduct.Name);

                    // Add barcode if available
                    if (!string.IsNullOrEmpty(item.Barcode))
                    {
                        await _productsService.AddBarcodeAsync(newProduct.Id, item.Barcode, "Added from shopping", cancellationToken);
                        _logger.LogInformation("Added barcode {Barcode} to product {ProductId}", item.Barcode, newProduct.Id);
                    }

                    // Add image if available (download from URL)
                    if (!string.IsNullOrEmpty(item.ImageUrl))
                    {
                        var imageResult = await _productsService.AddImageFromUrlAsync(newProduct.Id, item.ImageUrl, cancellationToken);
                        if (imageResult != null)
                        {
                            _logger.LogInformation("Added image from URL to product {ProductId}", newProduct.Id);
                        }
                    }

                    // Link product to store if external product ID is available
                    if (!string.IsNullOrEmpty(item.ExternalProductId) && item.ShoppingLocationId.HasValue)
                    {
                        try
                        {
                            var linkRequest = new LinkProductToStoreRequest
                            {
                                ExternalProductId = item.ExternalProductId,
                                Price = item.Price,
                                Aisle = item.Aisle,
                                Shelf = item.Shelf,
                                Department = item.Department
                            };
                            await _storeIntegrationService.LinkProductToStoreAsync(
                                newProduct.Id,
                                item.ShoppingLocationId.Value,
                                linkRequest,
                                cancellationToken);
                            _logger.LogInformation("Linked product {ProductId} to store {StoreId} with external ID {ExternalId}",
                                newProduct.Id, item.ShoppingLocationId.Value, item.ExternalProductId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to link product {ProductId} to store: {Message}",
                                newProduct.Id, ex.Message);
                            // Don't fail the whole operation for store linking failure
                        }
                    }

                    // Add to inventory
                    var addStockRequest = new AddStockRequest
                    {
                        ProductId = newProduct.Id,
                        Amount = item.Amount,
                        Price = item.Price,
                        PurchasedDate = DateTime.UtcNow,
                        BestBeforeDate = item.BestBeforeDate,
                        LocationId = item.LocationId ?? defaultLocation.Id
                    };

                    var stockEntry = await _stockService.AddStockAsync(addStockRequest, cancellationToken);
                    response.StockEntryIds.Add(stockEntry.Id);
                    response.ItemsAddedToStock++;

                    _logger.LogInformation("Added stock entry {StockEntryId} for new product {ProductId}",
                        stockEntry.Id, newProduct.Id);

                    // Create a TODO item to review/complete the product setup
                    var additionalData = JsonSerializer.Serialize(new
                    {
                        Amount = item.Amount,
                        Price = item.Price,
                        Barcode = item.Barcode,
                        ImageUrl = item.ImageUrl,
                        ExternalProductId = item.ExternalProductId,
                        ShoppingLocationId = item.ShoppingLocationId,
                        Aisle = item.Aisle,
                        Shelf = item.Shelf,
                        Department = item.Department,
                        ShoppingListId = request.ShoppingListId,
                        ShoppingListItemId = item.ShoppingListItemId
                    });

                    var todoRequest = new CreateTodoItemRequest
                    {
                        TaskType = TaskType.Product,
                        RelatedEntityId = newProduct.Id,
                        Reason = "New product from shopping - review and complete setup",
                        Description = newProduct.Name,
                        AdditionalData = additionalData
                    };

                    var todoItem = await _todoItemService.CreateAsync(todoRequest, cancellationToken);
                    response.TodoItemIds.Add(todoItem.Id);
                    response.TodoItemsCreated++;

                    _logger.LogInformation("Created TODO item {TodoItemId} for product {ProductId} '{ProductName}'",
                        todoItem.Id, newProduct.Id, newProduct.Name);
                }

                // Mark item for removal from shopping list
                itemIdsToRemove.Add(item.ShoppingListItemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing item {ShoppingListItemId}: {ProductName}",
                    item.ShoppingListItemId, item.ProductName);
                response.Errors.Add($"Failed to process '{item.ProductName}': {ex.Message}");
            }
        }

        // Remove processed items from the shopping list
        if (itemIdsToRemove.Count > 0)
        {
            var itemsToRemove = await _context.ShoppingListItems
                .Where(i => itemIdsToRemove.Contains(i.Id))
                .ToListAsync(cancellationToken);

            _context.ShoppingListItems.RemoveRange(itemsToRemove);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Removed {Count} items from shopping list {ListId}",
                itemsToRemove.Count, request.ShoppingListId);
        }

        _logger.LogInformation("Move to inventory complete: {StockCount} added to stock, {TodoCount} TODO items created, {ErrorCount} errors",
            response.ItemsAddedToStock, response.TodoItemsCreated, response.Errors.Count);

        return response;
    }

    /// <summary>
    /// Updates an existing product with metadata from shopping (barcode, image, store linking)
    /// </summary>
    private async Task UpdateProductWithShoppingMetadataAsync(
        Guid productId,
        string? barcode,
        string? imageUrl,
        string? externalProductId,
        Guid? shoppingLocationId,
        string? aisle,
        string? shelf,
        string? department,
        decimal? price,
        CancellationToken cancellationToken)
    {
        // Add barcode if provided and product doesn't already have this barcode
        if (!string.IsNullOrEmpty(barcode))
        {
            var existingBarcodes = await _context.ProductBarcodes
                .Where(pb => pb.ProductId == productId)
                .Select(pb => pb.Barcode)
                .ToListAsync(cancellationToken);

            if (!existingBarcodes.Contains(barcode, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    await _productsService.AddBarcodeAsync(productId, barcode, "Added from shopping", cancellationToken);
                    _logger.LogInformation("Added barcode {Barcode} to existing product {ProductId}", barcode, productId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add barcode {Barcode} to product {ProductId}: {Message}",
                        barcode, productId, ex.Message);
                }
            }
        }

        // Add image if provided and product doesn't already have images
        if (!string.IsNullOrEmpty(imageUrl))
        {
            var hasImages = await _context.ProductImages
                .AnyAsync(pi => pi.ProductId == productId, cancellationToken);

            if (!hasImages)
            {
                try
                {
                    var imageResult = await _productsService.AddImageFromUrlAsync(productId, imageUrl, cancellationToken);
                    if (imageResult != null)
                    {
                        _logger.LogInformation("Added image from URL to existing product {ProductId}", productId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add image to product {ProductId}: {Message}",
                        productId, ex.Message);
                }
            }
        }

        // Link product to store if external product ID is available
        if (!string.IsNullOrEmpty(externalProductId) && shoppingLocationId.HasValue)
        {
            try
            {
                var linkRequest = new LinkProductToStoreRequest
                {
                    ExternalProductId = externalProductId,
                    Price = price,
                    Aisle = aisle,
                    Shelf = shelf,
                    Department = department
                };
                await _storeIntegrationService.LinkProductToStoreAsync(
                    productId,
                    shoppingLocationId.Value,
                    linkRequest,
                    cancellationToken);
                _logger.LogInformation("Linked existing product {ProductId} to store {StoreId} with external ID {ExternalId}",
                    productId, shoppingLocationId.Value, externalProductId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to link product {ProductId} to store: {Message}",
                    productId, ex.Message);
            }
        }
    }

    #region Barcode Scanning

    public async Task<BarcodeScanResultDto> ScanBarcodeAsync(
        Guid listId,
        string barcode,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning barcode {Barcode} against shopping list {ListId}", barcode, listId);

        // Generate all barcode variants for matching
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);
        var barcodesToMatch = variants.Select(v => v.Barcode).ToList();
        // Also include the raw scanned barcode in case it doesn't generate variants
        if (!barcodesToMatch.Contains(barcode))
            barcodesToMatch.Add(barcode);

        // Find all products that match any barcode variant
        var matchingProductIds = await _context.ProductBarcodes
            .Where(pb => barcodesToMatch.Contains(pb.Barcode))
            .Select(pb => pb.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (matchingProductIds.Count == 0)
        {
            return new BarcodeScanResultDto { Found = false };
        }

        // Check if any matching product is a direct item on the shopping list
        var directItem = await _context.ShoppingListItems
            .Include(sli => sli.Product)
                .ThenInclude(p => p!.ChildProducts)
            .Where(sli => sli.ShoppingListId == listId && sli.ProductId != null
                && matchingProductIds.Contains(sli.ProductId.Value))
            .FirstOrDefaultAsync(cancellationToken);

        if (directItem != null)
        {
            var needsChildSelection = directItem.Product?.ChildProducts.Count > 0
                && directItem.Product.ChildProducts.Any(cp =>
                    _context.ProductStoreMetadata.Any(psm => psm.ProductId == cp.Id));

            return new BarcodeScanResultDto
            {
                Found = true,
                ItemId = directItem.Id,
                ProductName = directItem.ProductName ?? directItem.Product?.Name,
                IsChildProduct = false,
                NeedsChildSelection = needsChildSelection
            };
        }

        // Check if any matching product is a child of an item on the shopping list
        var matchingChildProducts = await _context.Products
            .Where(p => matchingProductIds.Contains(p.Id) && p.ParentProductId != null)
            .Select(p => new { ChildId = p.Id, ChildName = p.Name, ParentId = p.ParentProductId!.Value })
            .ToListAsync(cancellationToken);

        foreach (var child in matchingChildProducts)
        {
            var parentItem = await _context.ShoppingListItems
                .Where(sli => sli.ShoppingListId == listId && sli.ProductId == child.ParentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (parentItem != null)
            {
                return new BarcodeScanResultDto
                {
                    Found = true,
                    ItemId = parentItem.Id,
                    ProductName = parentItem.ProductName,
                    IsChildProduct = true,
                    ChildProductId = child.ChildId,
                    ChildProductName = child.ChildName,
                    NeedsChildSelection = true
                };
            }
        }

        return new BarcodeScanResultDto { Found = false };
    }

    #endregion

    #region Child Product Management

    public async Task<List<ShoppingListItemChildDto>> GetChildProductsForItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting child products for shopping list item: {ItemId}", itemId);

        var item = await _context.ShoppingListItems
            .Include(i => i.ShoppingList)
            .Include(i => i.Product)
                .ThenInclude(p => p!.ChildProducts)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        if (item.Product == null)
        {
            return new List<ShoppingListItemChildDto>();
        }

        // Check if this is a parent product
        if (!item.Product.ChildProducts.Any())
        {
            return new List<ShoppingListItemChildDto>();
        }

        var storeId = item.ShoppingList?.ShoppingLocationId ?? Guid.Empty;
        if (storeId == Guid.Empty)
        {
            // No store linked, return all children without store metadata
            return item.Product.ChildProducts
                .Select(c => new ShoppingListItemChildDto
                {
                    ProductId = c.Id,
                    ProductName = c.Name,
                    Description = c.Description,
                    HasStoreMetadata = false
                })
                .ToList();
        }

        // Get child product IDs
        var childProductIds = item.Product.ChildProducts.Select(c => c.Id).ToList();

        // Get store metadata for children at this store
        var storeMetadata = await _context.Set<ProductStoreMetadata>()
            .Where(m => childProductIds.Contains(m.ProductId) && m.ShoppingLocationId == storeId)
            .ToListAsync(cancellationToken);

        var metadataLookup = storeMetadata.ToDictionary(m => m.ProductId);

        // Parse existing purchases
        var childPurchases = ParseChildPurchases(item.ChildPurchasesJson);
        var purchaseLookup = childPurchases
            .GroupBy(p => p.ChildProductId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Quantity));

        // Build result - only children with store metadata at this store
        var result = new List<ShoppingListItemChildDto>();
        foreach (var child in item.Product.ChildProducts)
        {
            if (!metadataLookup.TryGetValue(child.Id, out var metadata))
            {
                continue; // Skip children without store metadata
            }

            purchaseLookup.TryGetValue(child.Id, out var purchasedQty);

            result.Add(new ShoppingListItemChildDto
            {
                ProductId = child.Id,
                ProductName = child.Name,
                Description = child.Description,
                ExternalProductId = metadata.ExternalProductId,
                LastKnownPrice = metadata.LastKnownPrice,
                PriceUnit = metadata.PriceUnit,
                Aisle = metadata.Aisle,
                Shelf = metadata.Shelf,
                Department = metadata.Department,
                InStock = metadata.InStock,
                ImageUrl = await GetProductImageUrlAsync(child, cancellationToken),
                HasStoreMetadata = true,
                PurchasedQuantity = purchasedQty
            });
        }

        _logger.LogInformation("Found {Count} child products with store metadata for item {ItemId}",
            result.Count, itemId);

        return result;
    }

    public async Task<ShoppingListItemDto> CheckOffChildAsync(
        Guid itemId,
        CheckOffChildRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking off child product {ChildId} for item {ItemId} with quantity {Quantity}",
            request.ChildProductId, itemId, request.Quantity);

        var item = await _context.ShoppingListItems
            .Include(i => i.Product)
                .ThenInclude(p => p!.ChildProducts)
            .Include(i => i.Product)
                .ThenInclude(p => p!.QuantityUnitPurchase)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        if (item.Product == null)
        {
            throw new DomainException("Shopping list item does not have a linked product");
        }

        // Validate that the child is actually a child of this parent
        var childProduct = item.Product.ChildProducts.FirstOrDefault(c => c.Id == request.ChildProductId);
        if (childProduct == null)
        {
            throw new DomainException($"Product {request.ChildProductId} is not a child of product {item.Product.Id}");
        }

        // Parse existing purchases and add new entry
        var purchases = ParseChildPurchases(item.ChildPurchasesJson);

        // Check if there's already an entry for this child - update it
        var existingEntry = purchases.FirstOrDefault(p => p.ChildProductId == request.ChildProductId);
        if (existingEntry != null)
        {
            existingEntry.Quantity += request.Quantity;
            existingEntry.PurchasedAt = DateTime.UtcNow;
        }
        else
        {
            purchases.Add(new ChildPurchaseEntry
            {
                ChildProductId = request.ChildProductId,
                ChildProductName = childProduct.Name,
                Quantity = request.Quantity,
                PurchasedAt = DateTime.UtcNow
            });
        }

        // Serialize back to JSON
        item.ChildPurchasesJson = JsonSerializer.Serialize(purchases);

        // Calculate total purchased quantity
        var totalPurchased = purchases.Sum(p => p.Quantity);

        // Update parent purchased status if we've met or exceeded the amount
        if (totalPurchased >= item.Amount && !item.IsPurchased)
        {
            item.IsPurchased = true;
            item.PurchasedAt = DateTime.UtcNow;
            item.BestBeforeDate = request.BestBeforeDate;
            _logger.LogInformation("Parent item {ItemId} marked as purchased (child total: {Total} >= amount: {Amount})",
                itemId, totalPurchased, item.Amount);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await MapItemToDtoWithChildInfo(item, cancellationToken);
    }

    public async Task<ShoppingListItemDto> UncheckChildAsync(
        Guid itemId,
        Guid childProductId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Unchecking child product {ChildId} from item {ItemId}",
            childProductId, itemId);

        var item = await _context.ShoppingListItems
            .Include(i => i.Product)
                .ThenInclude(p => p!.QuantityUnitPurchase)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        // Parse purchases and remove the child entry
        var purchases = ParseChildPurchases(item.ChildPurchasesJson);
        var removed = purchases.RemoveAll(p => p.ChildProductId == childProductId);

        if (removed == 0)
        {
            _logger.LogWarning("No purchase entry found for child {ChildId} in item {ItemId}",
                childProductId, itemId);
        }

        item.ChildPurchasesJson = purchases.Any() ? JsonSerializer.Serialize(purchases) : null;

        // Recalculate parent purchased status
        var totalPurchased = purchases.Sum(p => p.Quantity);
        if (item.IsPurchased && totalPurchased < item.Amount)
        {
            item.IsPurchased = false;
            item.PurchasedAt = null;
            _logger.LogInformation("Parent item {ItemId} unmarked as purchased (child total: {Total} < amount: {Amount})",
                itemId, totalPurchased, item.Amount);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await MapItemToDtoWithChildInfo(item, cancellationToken);
    }

    public async Task<SendToCartResult> SendChildToCartAsync(
        Guid listId,
        Guid itemId,
        SendChildToCartRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending child product {ChildId} to cart for item {ItemId} in list {ListId}",
            request.ChildProductId, itemId, listId);

        var list = await _context.ShoppingLists
            .Include(l => l.ShoppingLocation)
            .FirstOrDefaultAsync(l => l.Id == listId, cancellationToken);

        if (list == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingList), listId);
        }

        var shoppingLocation = list.ShoppingLocation;
        if (shoppingLocation?.IntegrationType == null)
        {
            throw new DomainException("Shopping list is not associated with a store integration");
        }

        // Get the child product's store metadata
        var childMetadata = await _context.Set<ProductStoreMetadata>()
            .Include(m => m.Product)
            .FirstOrDefaultAsync(m =>
                m.ProductId == request.ChildProductId &&
                m.ShoppingLocationId == shoppingLocation.Id,
                cancellationToken);

        if (childMetadata == null || string.IsNullOrEmpty(childMetadata.ExternalProductId))
        {
            return new SendToCartResult
            {
                Success = false,
                ItemsFailed = 1,
                Details = new List<SendToCartItemResult>
                {
                    new()
                    {
                        ItemId = itemId,
                        ProductName = "Unknown",
                        Success = false,
                        ErrorMessage = "Child product does not have an external product ID for this store"
                    }
                }
            };
        }

        // Get the plugin
        var plugin = _pluginLoader.GetPlugin<IStoreIntegrationPlugin>(shoppingLocation.IntegrationType);
        if (plugin == null || !plugin.Capabilities.HasShoppingCart)
        {
            throw new DomainException("Store integration does not support shopping cart");
        }

        // Get access token
        var accessToken = await _storeIntegrationService.GetAccessTokenAsync(
            shoppingLocation.IntegrationType, cancellationToken);

        if (string.IsNullOrEmpty(accessToken))
        {
            return new SendToCartResult
            {
                Success = false,
                ItemsFailed = 1,
                Details = new List<SendToCartItemResult>
                {
                    new()
                    {
                        ItemId = itemId,
                        ProductName = childMetadata.Product?.Name ?? "Unknown",
                        Success = false,
                        ErrorMessage = "Failed to get access token for store integration"
                    }
                }
            };
        }

        try
        {
            var cartItems = new List<CartItemRequest>
            {
                new()
                {
                    ExternalProductId = childMetadata.ExternalProductId,
                    Quantity = request.Quantity
                }
            };

            var cartResult = await plugin.AddToCartAsync(
                accessToken,
                shoppingLocation.ExternalLocationId!,
                cartItems,
                cancellationToken);

            if (cartResult != null)
            {
                return new SendToCartResult
                {
                    Success = true,
                    ItemsSent = 1,
                    Details = new List<SendToCartItemResult>
                    {
                        new()
                        {
                            ItemId = itemId,
                            ProductName = childMetadata.Product?.Name ?? "Unknown",
                            Success = true
                        }
                    }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding child product to cart");
            return new SendToCartResult
            {
                Success = false,
                ItemsFailed = 1,
                Details = new List<SendToCartItemResult>
                {
                    new()
                    {
                        ItemId = itemId,
                        ProductName = childMetadata.Product?.Name ?? "Unknown",
                        Success = false,
                        ErrorMessage = ex.Message
                    }
                }
            };
        }

        return new SendToCartResult
        {
            Success = false,
            ItemsFailed = 1,
            Details = new List<SendToCartItemResult>
            {
                new()
                {
                    ItemId = itemId,
                    ProductName = childMetadata.Product?.Name ?? "Unknown",
                    Success = false,
                    ErrorMessage = "Failed to add to cart"
                }
            }
        };
    }

    public async Task<ShoppingListItemDto> AddChildToParentAsync(
        Guid itemId,
        AddChildToParentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding child to parent item {ItemId}", itemId);

        var item = await _context.ShoppingListItems
            .Include(i => i.ShoppingList)
                .ThenInclude(sl => sl!.ShoppingLocation)
            .Include(i => i.Product)
                .ThenInclude(p => p!.ChildProducts)
            .Include(i => i.Product)
                .ThenInclude(p => p!.QuantityUnitPurchase)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        if (item.Product == null)
        {
            throw new DomainException("Shopping list item does not have a linked parent product");
        }

        Product? childProduct = null;
        var storeId = item.ShoppingList?.ShoppingLocationId ?? Guid.Empty;

        if (request.ProductId.HasValue)
        {
            // Link existing product as child
            childProduct = await _context.Products.FindAsync(new object[] { request.ProductId.Value }, cancellationToken);
            if (childProduct == null)
            {
                throw new EntityNotFoundException(nameof(Product), request.ProductId.Value);
            }

            // Set parent if not already set
            if (childProduct.ParentProductId == null)
            {
                childProduct.ParentProductId = item.Product.Id;
            }
            else if (childProduct.ParentProductId != item.Product.Id)
            {
                throw new DomainException("Product is already a child of a different parent product");
            }

            // Create store metadata if we have store info
            if (!string.IsNullOrEmpty(request.ExternalProductId) && storeId != Guid.Empty)
            {
                var existingMetadata = await _context.Set<ProductStoreMetadata>()
                    .FirstOrDefaultAsync(m => m.ProductId == childProduct.Id && m.ShoppingLocationId == storeId,
                        cancellationToken);

                if (existingMetadata == null)
                {
                    var metadata = new ProductStoreMetadata
                    {
                        Id = Guid.NewGuid(),
                        ProductId = childProduct.Id,
                        ShoppingLocationId = storeId,
                        ExternalProductId = request.ExternalProductId,
                        TenantId = item.TenantId
                    };
                    _context.Set<ProductStoreMetadata>().Add(metadata);
                }
            }
        }
        else if (!string.IsNullOrEmpty(request.ProductName))
        {
            // Create ad-hoc child product
            var defaultLocation = await _context.Locations
                .FirstOrDefaultAsync(l => l.Name == "Kitchen", cancellationToken)
                ?? await _context.Locations
                    .OrderBy(l => l.Name)
                    .FirstOrDefaultAsync(cancellationToken);

            var defaultQuantityUnit = await _context.QuantityUnits
                .OrderBy(q => q.Name)
                .FirstOrDefaultAsync(cancellationToken);

            if (defaultLocation == null || defaultQuantityUnit == null)
            {
                throw new DomainException("Cannot create product: No default location or quantity unit configured");
            }

            childProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = request.ProductName,
                ParentProductId = item.Product.Id,
                LocationId = defaultLocation.Id,
                QuantityUnitIdPurchase = defaultQuantityUnit.Id,
                QuantityUnitIdStock = defaultQuantityUnit.Id,
                QuantityUnitFactorPurchaseToStock = 1.0m,
                IsActive = true,
                TenantId = item.TenantId
            };
            _context.Products.Add(childProduct);

            // Create store metadata if we have store info
            if (!string.IsNullOrEmpty(request.ExternalProductId) && storeId != Guid.Empty)
            {
                var metadata = new ProductStoreMetadata
                {
                    Id = Guid.NewGuid(),
                    ProductId = childProduct.Id,
                    ShoppingLocationId = storeId,
                    ExternalProductId = request.ExternalProductId,
                    TenantId = item.TenantId
                };
                _context.Set<ProductStoreMetadata>().Add(metadata);
            }
        }
        else
        {
            throw new DomainException("Either ProductId or ProductName must be provided");
        }

        // Add to child purchases
        var purchases = ParseChildPurchases(item.ChildPurchasesJson);
        var existingEntry = purchases.FirstOrDefault(p => p.ChildProductId == childProduct.Id);
        if (existingEntry != null)
        {
            existingEntry.Quantity += request.Quantity;
            existingEntry.PurchasedAt = DateTime.UtcNow;
        }
        else
        {
            purchases.Add(new ChildPurchaseEntry
            {
                ChildProductId = childProduct.Id,
                ChildProductName = childProduct.Name,
                Quantity = request.Quantity,
                ExternalProductId = request.ExternalProductId,
                PurchasedAt = DateTime.UtcNow
            });
        }

        item.ChildPurchasesJson = JsonSerializer.Serialize(purchases);

        // Update parent purchased status
        var totalPurchased = purchases.Sum(p => p.Quantity);
        if (totalPurchased >= item.Amount && !item.IsPurchased)
        {
            item.IsPurchased = true;
            item.PurchasedAt = DateTime.UtcNow;
            item.BestBeforeDate = request.BestBeforeDate;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added child {ChildId} to parent item {ItemId}",
            childProduct.Id, itemId);

        return await MapItemToDtoWithChildInfo(item, cancellationToken);
    }

    public async Task<List<StoreProductSearchResult>> SearchStoreForChildAsync(
        Guid itemId,
        string query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching store for child products for item {ItemId} with query '{Query}'",
            itemId, query);

        var item = await _context.ShoppingListItems
            .Include(i => i.ShoppingList)
                .ThenInclude(sl => sl!.ShoppingLocation)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

        if (item == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingListItem), itemId);
        }

        var shoppingLocation = item.ShoppingList?.ShoppingLocation;
        if (shoppingLocation?.IntegrationType == null)
        {
            return new List<StoreProductSearchResult>();
        }

        try
        {
            var searchRequest = new StoreProductSearchRequest
            {
                Query = query,
                MaxResults = 20
            };

            var storeResults = await _storeIntegrationService.SearchProductsAtStoreAsync(
                shoppingLocation.Id,
                searchRequest,
                cancellationToken);

            // Check if any results are already linked to local products
            var externalIds = storeResults
                .Where(r => !string.IsNullOrEmpty(r.ExternalProductId))
                .Select(r => r.ExternalProductId!)
                .ToList();

            var linkedProducts = await _context.Set<ProductStoreMetadata>()
                .Where(m => m.ShoppingLocationId == shoppingLocation.Id && externalIds.Contains(m.ExternalProductId!))
                .ToDictionaryAsync(m => m.ExternalProductId!, m => m.ProductId, cancellationToken);

            return storeResults.Select(r => new StoreProductSearchResult
            {
                ExternalProductId = r.ExternalProductId ?? string.Empty,
                ProductName = r.Name ?? "Unknown",
                Description = r.Description,
                Price = r.Price,
                PriceUnit = r.PriceUnit,
                Aisle = r.Aisle,
                Department = r.Department,
                ImageUrl = r.ImageUrl,
                Barcode = r.Barcode,
                InStock = r.InStock,
                LinkedProductId = !string.IsNullOrEmpty(r.ExternalProductId) && linkedProducts.TryGetValue(r.ExternalProductId, out var pid)
                    ? pid
                    : null
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search store for child products");
            return new List<StoreProductSearchResult>();
        }
    }

    #endregion

    #region Child Product Helpers

    private static List<ChildPurchaseEntry> ParseChildPurchases(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<ChildPurchaseEntry>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ChildPurchaseEntry>>(json)
                ?? new List<ChildPurchaseEntry>();
        }
        catch
        {
            return new List<ChildPurchaseEntry>();
        }
    }

    private async Task<string?> GetProductImageUrlAsync(Product product, CancellationToken cancellationToken)
    {
        var image = await _context.ProductImages
            .Where(pi => pi.ProductId == product.Id)
            .OrderByDescending(pi => pi.IsPrimary)
            .ThenBy(pi => pi.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (image == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(image.ExternalUrl))
        {
            return image.ExternalUrl;
        }

        var token = _tokenService.GenerateToken("product-image", image.Id, image.TenantId);
        return _fileStorage.GetProductImageUrl(product.Id, image.Id, token);
    }

    private async Task<ShoppingListItemDto> MapItemToDtoWithChildInfo(
        ShoppingListItem item,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<ShoppingListItemDto>(item);

        if (item.ProductId.HasValue)
        {
            // Load all barcodes for this product
            dto.Barcodes = await _context.ProductBarcodes
                .Where(pb => pb.ProductId == item.ProductId.Value)
                .Select(pb => pb.Barcode)
                .ToListAsync(cancellationToken);
        }

        if (item.Product != null)
        {
            // Count child products
            var childCount = await _context.Products
                .CountAsync(p => p.ParentProductId == item.ProductId, cancellationToken);

            dto.IsParentProduct = childCount > 0;
            dto.HasChildren = childCount > 0;
            dto.ChildProductCount = childCount;

            // Check if any children have store metadata
            if (childCount > 0 && item.ShoppingList?.ShoppingLocationId != Guid.Empty)
            {
                var storeId = item.ShoppingList?.ShoppingLocationId ?? Guid.Empty;
                if (storeId != Guid.Empty)
                {
                    var childIds = await _context.Products
                        .Where(p => p.ParentProductId == item.ProductId)
                        .Select(p => p.Id)
                        .ToListAsync(cancellationToken);

                    dto.HasChildrenAtStore = await _context.Set<ProductStoreMetadata>()
                        .AnyAsync(m => childIds.Contains(m.ProductId) && m.ShoppingLocationId == storeId,
                            cancellationToken);
                }
            }

            // Parse child purchases
            var purchases = ParseChildPurchases(item.ChildPurchasesJson);
            dto.ChildPurchases = purchases;
            dto.ChildPurchasedQuantity = purchases.Sum(p => p.Quantity);
        }

        return dto;
    }

    #endregion
}
