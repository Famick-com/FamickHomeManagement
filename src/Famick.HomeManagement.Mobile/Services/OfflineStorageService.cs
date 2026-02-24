using System.Text.Json;
using Famick.HomeManagement.Mobile.Models;
using SQLite;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Service for local SQLite storage of shopping sessions and offline operations.
/// </summary>
public class OfflineStorageService
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;

    public OfflineStorageService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "shopping.db");
    }

    private async Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        if (_database == null)
        {
            _database = new SQLiteAsyncConnection(_dbPath);
            await _database.CreateTableAsync<ShoppingSession>().ConfigureAwait(false);
            await _database.CreateTableAsync<CachedShoppingListItem>().ConfigureAwait(false);
            await _database.CreateTableAsync<OfflineOperation>().ConfigureAwait(false);
        }
        return _database;
    }

    /// <summary>
    /// Gets a cached shopping session if available.
    /// </summary>
    public async Task<ShoppingSession?> GetCachedSessionAsync(Guid listId)
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var session = await db.Table<ShoppingSession>()
            .FirstOrDefaultAsync(s => s.ShoppingListId == listId && s.IsActive).ConfigureAwait(false);

        if (session != null)
        {
            session.Items = await db.Table<CachedShoppingListItem>()
                .Where(i => i.SessionId == session.Id)
                .OrderBy(i => i.SortOrder)
                .ToListAsync().ConfigureAwait(false);

            // Fix for stale cached data: if OriginalIsPurchased wasn't set (old cache without the column),
            // set it to match IsPurchased so we treat the current state as the baseline
            foreach (var item in session.Items)
            {
                if (!item.OriginalIsPurchased && item.IsPurchased)
                {
                    item.OriginalIsPurchased = item.IsPurchased;
                    await db.UpdateAsync(item).ConfigureAwait(false);
                }
            }
        }

        return session;
    }

    /// <summary>
    /// Caches a shopping list for offline use.
    /// </summary>
    public async Task<ShoppingSession> CacheShoppingSessionAsync(Guid listId, ShoppingListDetail list)
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);

        // Clear any existing session for this list
        await ClearSessionAsync(listId).ConfigureAwait(false);

        // Create new session
        var session = new ShoppingSession
        {
            Id = Guid.NewGuid(),
            ShoppingListId = listId,
            ShoppingListName = list.Name,
            StoreId = list.ShoppingLocationId,
            StoreName = list.ShoppingLocationName ?? "",
            StartedAt = DateTime.UtcNow,
            IsActive = true
        };

        await db.InsertAsync(session).ConfigureAwait(false);

        // Cache items - preserve the order from the backend (already sorted by custom aisle order)
        var sortOrder = 0;
        foreach (var item in list.Items)
        {
            var cachedItem = new CachedShoppingListItem
            {
                Id = item.Id,
                SessionId = session.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName ?? "",
                ImageUrl = item.ImageUrl,
                Amount = item.Amount,
                QuantityUnitName = item.QuantityUnitName,
                Note = item.Note,
                IsPurchased = item.IsPurchased,
                OriginalIsPurchased = item.IsPurchased,
                PurchasedAt = item.PurchasedAt,
                PurchasedQuantity = item.PurchasedQuantity,
                BestBeforeDate = item.BestBeforeDate,
                TracksBestBeforeDate = item.TracksBestBeforeDate,
                DefaultBestBeforeDays = item.DefaultBestBeforeDays,
                DefaultLocationId = item.DefaultLocationId,
                Aisle = item.Aisle,
                Shelf = item.Shelf,
                Department = item.Department,
                ExternalProductId = item.ExternalProductId,
                Price = item.Price,
                Barcode = item.Barcode,
                BarcodesJson = item.Barcodes.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(item.Barcodes)
                    : null,
                SortOrder = sortOrder++,
                IsNewItem = false,
                // Parent/child product support
                IsParentProduct = item.IsParentProduct,
                ChildProductCount = item.ChildProductCount,
                HasChildrenAtStore = item.HasChildrenAtStore,
                ChildPurchasedQuantity = item.ChildPurchasedQuantity
            };

            await db.InsertOrReplaceAsync(cachedItem).ConfigureAwait(false);
            session.Items.Add(cachedItem);
        }

        return session;
    }

    /// <summary>
    /// Updates the state of a cached item.
    /// </summary>
    public async Task UpdateItemStateAsync(CachedShoppingListItem item)
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);
        await db.UpdateAsync(item).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a new item to a cached session.
    /// </summary>
    public async Task AddItemToSessionAsync(Guid listId, CachedShoppingListItem item)
    {
        var db = await GetDatabaseAsync();
        var session = await db.Table<ShoppingSession>()
            .FirstOrDefaultAsync(s => s.ShoppingListId == listId && s.IsActive);

        if (session != null)
        {
            item.SessionId = session.Id;
            await db.InsertAsync(item);
        }
    }

    /// <summary>
    /// Queues an operation for later sync.
    /// </summary>
    public async Task EnqueueOperationAsync(OfflineOperation operation)
    {
        var db = await GetDatabaseAsync();
        await db.InsertAsync(operation);
    }

    /// <summary>
    /// Gets all pending (not completed) operations.
    /// </summary>
    public async Task<List<OfflineOperation>> GetPendingOperationsAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<OfflineOperation>()
            .Where(o => !o.IsCompleted)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Removes any pending TogglePurchased operations for a specific item.
    /// </summary>
    public async Task RemovePendingToggleOperationsAsync(Guid listId, Guid itemId)
    {
        var db = await GetDatabaseAsync();
        var pendingOps = await db.Table<OfflineOperation>()
            .Where(o => !o.IsCompleted && o.OperationType == "TogglePurchased")
            .ToListAsync();

        foreach (var op in pendingOps)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<TogglePurchasedPayload>(op.PayloadJson);
                if (payload != null && payload.ListId == listId && payload.ItemId == itemId)
                {
                    await db.DeleteAsync(op);
                }
            }
            catch
            {
                // Skip malformed operations
            }
        }
    }

    /// <summary>
    /// Marks an operation as completed.
    /// </summary>
    public async Task MarkOperationCompletedAsync(Guid operationId)
    {
        var db = await GetDatabaseAsync();
        var operation = await db.Table<OfflineOperation>()
            .FirstOrDefaultAsync(o => o.Id == operationId);

        if (operation != null)
        {
            operation.IsCompleted = true;
            await db.UpdateAsync(operation);
        }
    }

    /// <summary>
    /// Gets purchase counts for a specific list from the local cache.
    /// </summary>
    public async Task<(int total, int purchased)?> GetLocalPurchaseCountsAsync(Guid listId)
    {
        var db = await GetDatabaseAsync();
        var session = await db.Table<ShoppingSession>()
            .FirstOrDefaultAsync(s => s.ShoppingListId == listId && s.IsActive);

        if (session == null)
            return null;

        var items = await db.Table<CachedShoppingListItem>()
            .Where(i => i.SessionId == session.Id)
            .ToListAsync();

        return (items.Count, items.Count(i => i.IsPurchased));
    }

    /// <summary>
    /// Gets all active sessions with their purchase counts.
    /// </summary>
    public async Task<Dictionary<Guid, (int total, int purchased)>> GetAllLocalPurchaseCountsAsync()
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var sessions = await db.Table<ShoppingSession>()
            .Where(s => s.IsActive)
            .ToListAsync().ConfigureAwait(false);

        var result = new Dictionary<Guid, (int total, int purchased)>();

        foreach (var session in sessions)
        {
            var items = await db.Table<CachedShoppingListItem>()
                .Where(i => i.SessionId == session.Id)
                .ToListAsync().ConfigureAwait(false);

            result[session.ShoppingListId] = (items.Count, items.Count(i => i.IsPurchased));
        }

        return result;
    }

    /// <summary>
    /// Syncs all pending operations to the server.
    /// </summary>
    public async Task SyncPendingOperationsAsync(ShoppingApiClient apiClient)
    {
        var operations = await GetPendingOperationsAsync();
        Console.WriteLine($"SyncPendingOperationsAsync: Found {operations.Count} pending operations");

        foreach (var operation in operations)
        {
            Console.WriteLine($"SyncPendingOperationsAsync: Processing {operation.OperationType} - {operation.PayloadJson}");
            try
            {
                var success = await ProcessOperationAsync(apiClient, operation);
                Console.WriteLine($"SyncPendingOperationsAsync: {operation.OperationType} result = {success}");
                if (success)
                {
                    await MarkOperationCompletedAsync(operation.Id);
                }
                else
                {
                    operation.RetryCount++;
                    var db = await GetDatabaseAsync();
                    await db.UpdateAsync(operation);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyncPendingOperationsAsync: Exception - {ex.Message}");
                operation.RetryCount++;
                var db = await GetDatabaseAsync();
                await db.UpdateAsync(operation);
            }
        }
    }

    private async Task<bool> ProcessOperationAsync(ShoppingApiClient apiClient, OfflineOperation operation)
    {
        switch (operation.OperationType)
        {
            case "TogglePurchased":
                var toggleData = JsonSerializer.Deserialize<TogglePurchasedPayload>(operation.PayloadJson);
                if (toggleData != null)
                {
                    var result = await apiClient.TogglePurchasedAsync(toggleData.ListId, toggleData.ItemId, toggleData.BestBeforeDate);
                    return result.Success;
                }
                break;

            case "AddItem":
                var addData = JsonSerializer.Deserialize<AddItemPayload>(operation.PayloadJson);
                if (addData != null)
                {
                    var result = await apiClient.QuickAddItemAsync(
                        addData.ListId,
                        addData.ProductName,
                        addData.Amount,
                        addData.Barcode,
                        addData.Note,
                        addData.IsPurchased);
                    return result.Success;
                }
                break;

            case "RemoveItem":
                var removeData = JsonSerializer.Deserialize<RemoveItemPayload>(operation.PayloadJson);
                if (removeData != null)
                {
                    var result = await apiClient.RemoveItemAsync(removeData.ListId, removeData.ItemId);
                    return result.Success;
                }
                break;

            case "UpdateQuantity":
                var updateQtyData = JsonSerializer.Deserialize<UpdateQuantityPayload>(operation.PayloadJson);
                if (updateQtyData != null)
                {
                    var result = await apiClient.UpdateItemQuantityAsync(
                        updateQtyData.ListId, updateQtyData.ItemId, updateQtyData.Amount, updateQtyData.Note);
                    return result.Success;
                }
                break;

            case "ScanPurchase":
                var scanData = JsonSerializer.Deserialize<ScanPurchasePayload>(operation.PayloadJson);
                if (scanData != null)
                {
                    var result = await apiClient.ScanPurchaseAsync(scanData.ListId, scanData.ItemId, scanData.Quantity);
                    return result.Success;
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Removes an item from a cached session.
    /// </summary>
    public async Task RemoveItemFromSessionAsync(Guid itemId)
    {
        var db = await GetDatabaseAsync();
        await db.Table<CachedShoppingListItem>().DeleteAsync(i => i.Id == itemId);
    }

    /// <summary>
    /// Clears a specific session and its items.
    /// </summary>
    public async Task ClearSessionAsync(Guid listId)
    {
        var db = await GetDatabaseAsync().ConfigureAwait(false);
        var session = await db.Table<ShoppingSession>()
            .FirstOrDefaultAsync(s => s.ShoppingListId == listId).ConfigureAwait(false);

        if (session != null)
        {
            await db.Table<CachedShoppingListItem>()
                .DeleteAsync(i => i.SessionId == session.Id).ConfigureAwait(false);
            await db.DeleteAsync(session).ConfigureAwait(false);
        }

        // Also clear completed operations for this list
        var listIdStr = listId.ToString();
        var operations = await db.Table<OfflineOperation>()
            .Where(o => o.IsCompleted && o.PayloadJson.Contains(listIdStr))
            .ToListAsync().ConfigureAwait(false);

        foreach (var op in operations)
        {
            await db.DeleteAsync(op).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public async Task ClearAllAsync()
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAllAsync<CachedShoppingListItem>();
        await db.DeleteAllAsync<ShoppingSession>();
        await db.DeleteAllAsync<OfflineOperation>();
    }

    private class TogglePurchasedPayload
    {
        public Guid ListId { get; set; }
        public Guid ItemId { get; set; }
        public bool IsPurchased { get; set; }
        public DateTime? BestBeforeDate { get; set; }
    }

    private class AddItemPayload
    {
        public Guid ListId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal Amount { get; set; }
        public string? Barcode { get; set; }
        public string? Note { get; set; }
        public bool IsPurchased { get; set; }
    }

    private class RemoveItemPayload
    {
        public Guid ListId { get; set; }
        public Guid ItemId { get; set; }
    }

    private class UpdateQuantityPayload
    {
        public Guid ListId { get; set; }
        public Guid ItemId { get; set; }
        public decimal Amount { get; set; }
        public string? Note { get; set; }
    }

    private class ScanPurchasePayload
    {
        public Guid ListId { get; set; }
        public Guid ItemId { get; set; }
        public decimal Quantity { get; set; }
    }
}
