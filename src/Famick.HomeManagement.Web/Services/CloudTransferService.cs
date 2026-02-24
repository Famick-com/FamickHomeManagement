using Famick.HomeManagement.Core.DTOs.Transfer;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Web.Services;

/// <summary>
/// Orchestrates data transfer from self-hosted instance to Famick cloud.
/// Reads local entities, compares with cloud via CloudApiClient, posts missing items.
/// Transfer runs as a background task; progress is polled by the UI.
/// </summary>
public class CloudTransferService : ICloudTransferService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CloudTransferService> _logger;

    // In-memory state shared between background task and polling endpoint
    private TransferProgress? _currentProgress;
    private CancellationTokenSource? _transferCts;
    private CloudApiClient? _cloudClient;
    private Guid? _activeSessionId;

    // Category transfer order
    private static readonly string[] CategoryOrder =
    [
        "Locations", "Quantity Units", "Product Groups", "Shopping Locations",
        "Equipment Categories", "Contact Tags",
        "Contacts", "Products", "Equipment", "Vehicles",
        "Recipes", "Chores", "Chore Logs",
        "Todo Items", "Shopping Lists", "Storage Bins",
        "Home", "Calendar Events", "Stock"
    ];

    // Categories that are history-only (skipped unless includeHistory is true)
    private static readonly HashSet<string> HistoryCategories = ["Chore Logs"];

    public CloudTransferService(
        IServiceScopeFactory scopeFactory,
        ILogger<CloudTransferService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<TransferAuthenticateResponse> AuthenticateAsync(
        TransferAuthenticateRequest request, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        var httpClient = httpClientFactory.CreateClient("CloudApi");
        _cloudClient = new CloudApiClient(httpClient, loggerFactory.CreateLogger<CloudApiClient>());

        if (request.IsRegistration)
        {
            if (string.IsNullOrEmpty(request.FirstName) || string.IsNullOrEmpty(request.LastName))
                return new TransferAuthenticateResponse { Success = false, ErrorMessage = "First and last name are required for registration" };

            var regResult = await _cloudClient.RegisterAsync(
                request.Email, request.Password, request.FirstName, request.LastName, ct);

            if (!regResult.IsSuccess)
                return new TransferAuthenticateResponse { Success = false, ErrorMessage = regResult.ErrorMessage };

            await SaveSessionAuth(request.Email, _cloudClient.RefreshToken, ct);
            return new TransferAuthenticateResponse { Success = true, CloudUserEmail = request.Email };
        }

        var loginResult = await _cloudClient.LoginAsync(request.Email, request.Password, ct);
        if (!loginResult.IsSuccess)
            return new TransferAuthenticateResponse { Success = false, ErrorMessage = loginResult.ErrorMessage };

        await SaveSessionAuth(request.Email, _cloudClient.RefreshToken, ct);
        return new TransferAuthenticateResponse { Success = true, CloudUserEmail = request.Email };
    }

    public async Task<TransferDataSummary> GetSummaryAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();

        return new TransferDataSummary
        {
            Locations = await db.Locations.CountAsync(ct),
            QuantityUnits = await db.QuantityUnits.CountAsync(ct),
            ProductGroups = await db.ProductGroups.CountAsync(ct),
            ShoppingLocations = await db.ShoppingLocations.CountAsync(ct),
            EquipmentCategories = await db.EquipmentCategories.CountAsync(ct),
            ContactTags = await db.ContactTags.CountAsync(ct),
            Contacts = await db.Contacts.CountAsync(ct),
            Products = await db.Products.CountAsync(ct),
            Equipment = await db.Equipment.CountAsync(ct),
            Vehicles = await db.Vehicles.CountAsync(ct),
            Recipes = await db.Recipes.CountAsync(ct),
            Chores = await db.Chores.CountAsync(ct),
            ChoreLogs = await db.ChoresLog.CountAsync(ct),
            TodoItems = await db.TodoItems.CountAsync(ct),
            ShoppingLists = await db.ShoppingLists.CountAsync(ct),
            StorageBins = await db.StorageBins.CountAsync(ct),
            CalendarEvents = await db.CalendarEvents.CountAsync(ct),
            StockEntries = await db.Stock.CountAsync(ct),
        };
    }

    public async Task<TransferSessionInfo> GetSessionInfoAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();

        var session = await transferDb.TransferSessions
            .Where(s => s.Status == TransferSessionStatus.InProgress)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (session == null)
            return new TransferSessionInfo { HasIncompleteSession = false };

        return new TransferSessionInfo
        {
            HasIncompleteSession = true,
            SessionId = session.Id,
            CurrentCategory = session.CurrentCategory,
            StartedAt = session.StartedAt
        };
    }

    public async Task<TransferStartResponse> StartTransferAsync(TransferStartRequest request, CancellationToken ct)
    {
        if (_cloudClient == null)
            throw new InvalidOperationException("Must authenticate before starting transfer");

        _transferCts?.Cancel();
        _transferCts = new CancellationTokenSource();

        using var scope = _scopeFactory.CreateScope();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();

        TransferSession session;

        if (request.Resume)
        {
            session = await transferDb.TransferSessions
                .Where(s => s.Status == TransferSessionStatus.InProgress)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("No incomplete session to resume");

            // Re-auth with stored refresh token if available
            if (!string.IsNullOrEmpty(session.EncryptedRefreshToken))
            {
                await _cloudClient.RestoreAuthAsync(session.EncryptedRefreshToken, ct);
            }

            session.IncludeHistory = request.IncludeHistory;
        }
        else
        {
            // Cancel any existing incomplete sessions
            var existingSessions = await transferDb.TransferSessions
                .Where(s => s.Status == TransferSessionStatus.InProgress)
                .ToListAsync(ct);
            foreach (var s in existingSessions)
                s.Status = TransferSessionStatus.Cancelled;

            session = new TransferSession
            {
                Id = Guid.NewGuid(),
                CloudEmail = "cloud-user",
                IncludeHistory = request.IncludeHistory,
                Status = TransferSessionStatus.InProgress,
                StartedAt = DateTime.UtcNow
            };
            transferDb.TransferSessions.Add(session);
        }

        await transferDb.SaveChangesAsync(ct);
        _activeSessionId = session.Id;

        // Initialize progress
        var categories = GetActiveCategories(request.IncludeHistory);
        _currentProgress = new TransferProgress
        {
            SessionStatus = TransferSessionStatus.InProgress,
            TotalCategories = categories.Length
        };

        // Start background task
        var cloudClient = _cloudClient;
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_transferCts.Token);
        _ = Task.Run(() => RunTransferAsync(session.Id, cloudClient, request.IncludeHistory, request.Resume, linkedCts.Token), linkedCts.Token);

        return new TransferStartResponse { SessionId = session.Id };
    }

    public TransferProgress? GetCurrentProgress() => _currentProgress;

    public void CancelTransfer()
    {
        _transferCts?.Cancel();
    }

    public async Task<List<TransferCategoryResult>> GetResultsAsync(CancellationToken ct)
    {
        if (_activeSessionId == null)
            return new List<TransferCategoryResult>();

        using var scope = _scopeFactory.CreateScope();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();

        var items = await transferDb.TransferItemLogs
            .Where(i => i.TransferSessionId == _activeSessionId)
            .ToListAsync(ct);

        return items
            .GroupBy(i => i.Category)
            .Select(g => new TransferCategoryResult
            {
                Category = g.Key,
                CreatedCount = g.Count(i => i.Status == TransferItemStatus.Created),
                SkippedCount = g.Count(i => i.Status == TransferItemStatus.Skipped),
                FailedCount = g.Count(i => i.Status == TransferItemStatus.Failed),
                Items = g.Select(i => new TransferItemResult
                {
                    Name = i.Name ?? string.Empty,
                    Status = i.Status,
                    ErrorMessage = i.ErrorMessage
                }).ToList()
            })
            .ToList();
    }

    private static string[] GetActiveCategories(bool includeHistory)
    {
        return includeHistory
            ? CategoryOrder
            : CategoryOrder.Where(c => !HistoryCategories.Contains(c)).ToArray();
    }

    private async Task RunTransferAsync(
        Guid sessionId, CloudApiClient cloudClient, bool includeHistory, bool isResume, CancellationToken ct)
    {
        try
        {
            var categories = GetActiveCategories(includeHistory);

            for (var i = 0; i < categories.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                var category = categories[i];

                // For resume: skip categories that are fully completed
                if (isResume && await IsCategoryComplete(sessionId, category, ct))
                {
                    await UpdateCompletedCategorySummary(sessionId, category, i, categories.Length, ct);
                    continue;
                }

                _currentProgress!.CurrentCategory = category;
                _currentProgress.CurrentCategoryIndex = i;
                _currentProgress.CurrentItemIndex = 0;
                _currentProgress.TotalItemsInCategory = 0;
                _currentProgress.CategoryCreatedCount = 0;
                _currentProgress.CategorySkippedCount = 0;
                _currentProgress.CategoryFailedCount = 0;
                _currentProgress.CurrentItemName = null;
                _currentProgress.LastItemStatus = null;

                await UpdateSessionCategory(sessionId, category, ct);

                try
                {
                    await TransferCategoryAsync(sessionId, category, cloudClient, includeHistory, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error transferring category {Category}", category);
                }

                // Add to completed list
                _currentProgress!.CompletedCategories.Add(new TransferCategorySummary
                {
                    Category = category,
                    CreatedCount = _currentProgress.CategoryCreatedCount,
                    SkippedCount = _currentProgress.CategorySkippedCount,
                    FailedCount = _currentProgress.CategoryFailedCount
                });

                UpdateOverallProgress(i + 1, categories.Length);
            }

            await CompleteSession(sessionId, TransferSessionStatus.Completed, ct);
            _currentProgress!.SessionStatus = TransferSessionStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transfer {SessionId} was cancelled", sessionId);
            await CompleteSession(sessionId, TransferSessionStatus.Cancelled, CancellationToken.None);
            if (_currentProgress != null)
                _currentProgress.SessionStatus = TransferSessionStatus.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer {SessionId} failed", sessionId);
            await CompleteSession(sessionId, TransferSessionStatus.Failed, CancellationToken.None);
            if (_currentProgress != null)
                _currentProgress.SessionStatus = TransferSessionStatus.Failed;
        }
    }

    private async Task TransferCategoryAsync(
        Guid sessionId, string category, CloudApiClient cloudClient,
        bool includeHistory, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();

        switch (category)
        {
            case "Locations":
                await TransferSimpleEntitiesAsync<Location, CloudNamedDto>(sessionId, category, cloudClient, transferDb,
                    await db.Locations.AsNoTracking().ToListAsync(ct),
                    "api/v1/locations",
                    e => e.Id, e => e.Name,
                    e => new { e.Name, e.Description, e.SortOrder },
                    (cloud, local) => string.Equals(cloud.Name, local.Name, StringComparison.OrdinalIgnoreCase),
                    ct);
                break;

            case "Quantity Units":
                await TransferSimpleEntitiesAsync<QuantityUnit, CloudNamedDto>(sessionId, category, cloudClient, transferDb,
                    await db.QuantityUnits.AsNoTracking().ToListAsync(ct),
                    "api/v1/quantity-units",
                    e => e.Id, e => e.Name,
                    e => new { e.Name, e.NamePlural, e.Description },
                    (cloud, local) => string.Equals(cloud.Name, local.Name, StringComparison.OrdinalIgnoreCase),
                    ct);
                break;

            case "Product Groups":
                await TransferSimpleEntitiesAsync<ProductGroup, CloudNamedDto>(sessionId, category, cloudClient, transferDb,
                    await db.ProductGroups.AsNoTracking().ToListAsync(ct),
                    "api/v1/productgroups",
                    e => e.Id, e => e.Name,
                    e => new { e.Name, e.Description },
                    (cloud, local) => string.Equals(cloud.Name, local.Name, StringComparison.OrdinalIgnoreCase),
                    ct);
                break;

            case "Shopping Locations":
                await TransferSimpleEntitiesAsync<ShoppingLocation, CloudNamedDto>(sessionId, category, cloudClient, transferDb,
                    await db.ShoppingLocations.AsNoTracking().ToListAsync(ct),
                    "api/v1/shoppinglocations",
                    e => e.Id, e => e.Name,
                    e => new { e.Name, e.Description },
                    (cloud, local) => string.Equals(cloud.Name, local.Name, StringComparison.OrdinalIgnoreCase),
                    ct);
                break;

            case "Equipment Categories":
                await TransferSimpleEntitiesAsync<EquipmentCategory, CloudNamedDto>(sessionId, category, cloudClient, transferDb,
                    await db.EquipmentCategories.AsNoTracking().ToListAsync(ct),
                    "api/v1/equipment/categories",
                    e => e.Id, e => e.Name,
                    e => new { e.Name, e.Description },
                    (cloud, local) => string.Equals(cloud.Name, local.Name, StringComparison.OrdinalIgnoreCase),
                    ct);
                break;

            case "Contact Tags":
                await TransferSimpleEntitiesAsync<ContactTag, CloudNamedDto>(sessionId, category, cloudClient, transferDb,
                    await db.ContactTags.AsNoTracking().ToListAsync(ct),
                    "api/v1/contacts/tags",
                    e => e.Id, e => e.Name,
                    e => new { e.Name },
                    (cloud, local) => string.Equals(cloud.Name, local.Name, StringComparison.OrdinalIgnoreCase),
                    ct);
                break;

            case "Contacts":
                await TransferContactsAsync(sessionId, cloudClient, db, transferDb, ct);
                break;

            case "Products":
                await TransferProductsAsync(sessionId, cloudClient, db, transferDb, ct);
                break;

            case "Equipment":
                await TransferEquipmentAsync(sessionId, cloudClient, db, transferDb, includeHistory, ct);
                break;

            case "Vehicles":
                await TransferVehiclesAsync(sessionId, cloudClient, db, transferDb, includeHistory, ct);
                break;

            case "Recipes":
                await TransferRecipesAsync(sessionId, cloudClient, db, transferDb, ct);
                break;

            case "Chores":
                await TransferChoresAsync(sessionId, cloudClient, db, transferDb, ct);
                break;

            case "Chore Logs":
                await TransferChoreLogsAsync(sessionId, cloudClient, db, transferDb, ct);
                break;

            case "Todo Items":
                await TransferTodoItemsAsync(sessionId, cloudClient, db, transferDb, ct);
                break;

            case "Shopping Lists":
                await TransferShoppingListsAsync(sessionId, cloudClient, db, transferDb, ct);
                break;

            case "Storage Bins":
                await TransferStorageBinsAsync(sessionId, cloudClient, db, transferDb, ct);
                break;

            case "Home":
                await TransferHomeAsync(sessionId, cloudClient, db, transferDb, ct);
                break;

            case "Calendar Events":
                await TransferCalendarEventsAsync(sessionId, cloudClient, db, transferDb, ct);
                break;

            case "Stock":
                await TransferStockAsync(sessionId, cloudClient, db, transferDb, ct);
                break;
        }
    }

    #region Generic Transfer Helper

    /// <summary>
    /// Transfers simple entities that have a name-based duplicate match and no sub-resources.
    /// </summary>
    private async Task TransferSimpleEntitiesAsync<TEntity, TCloudDto>(
        Guid sessionId, string category, CloudApiClient cloudClient, TransferDbContext transferDb,
        List<TEntity> localEntities, string apiEndpoint,
        Func<TEntity, Guid> getId, Func<TEntity, string> getName,
        Func<TEntity, object> toCreateRequest,
        Func<TCloudDto, TEntity, bool> isDuplicate,
        CancellationToken ct)
        where TCloudDto : class
    {
        _currentProgress!.TotalItemsInCategory = localEntities.Count;

        // Get existing cloud items
        var cloudResult = await cloudClient.GetAsync<List<TCloudDto>>(apiEndpoint, ct);
        var cloudItems = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<TCloudDto>();

        // Get already-transferred items for this session (for resume)
        var alreadyTransferred = await transferDb.TransferItemLogs
            .Where(i => i.TransferSessionId == sessionId && i.Category == category)
            .Select(i => i.SourceId)
            .ToHashSetAsync(ct);

        for (var i = 0; i < localEntities.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entity = localEntities[i];
            var entityId = getId(entity);
            var entityName = getName(entity);

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = entityName;

            if (alreadyTransferred.Contains(entityId))
            {
                continue; // Already handled in a previous run
            }

            // Check for duplicate
            if (cloudItems.Any(c => isDuplicate(c, entity)))
            {
                await LogItemAsync(transferDb, sessionId, category, entityId, null, entityName,
                    TransferItemStatus.Skipped, null, ct);
                _currentProgress.CategorySkippedCount++;
                _currentProgress.LastItemStatus = TransferItemStatus.Skipped;
                continue;
            }

            // Create in cloud
            var createRequest = toCreateRequest(entity);
            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(apiEndpoint, createRequest, ct);

            if (result.IsSuccess && result.Data != null)
            {
                await LogItemAsync(transferDb, sessionId, category, entityId, result.Data.Id, entityName,
                    TransferItemStatus.Created, null, ct);
                _currentProgress.CategoryCreatedCount++;
                _currentProgress.LastItemStatus = TransferItemStatus.Created;
            }
            else
            {
                await LogItemAsync(transferDb, sessionId, category, entityId, null, entityName,
                    TransferItemStatus.Failed, result.ErrorMessage, ct);
                _currentProgress.CategoryFailedCount++;
                _currentProgress.LastItemStatus = TransferItemStatus.Failed;
            }
        }
    }

    #endregion

    #region Complex Category Transfers

    private async Task TransferContactsAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var contacts = await db.Contacts
            .Include(c => c.Addresses)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.EmailAddresses)
            .Include(c => c.SocialMedia)
            .AsNoTracking()
            .ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = contacts.Count;

        var cloudResult = await cloudClient.GetAsync<List<CloudContactDto>>(
            "api/v1/contacts", ct);
        var cloudContacts = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<CloudContactDto>();

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Contacts", ct);

        for (var i = 0; i < contacts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var contact = contacts[i];
            var displayName = !string.IsNullOrEmpty(contact.CompanyName)
                ? contact.CompanyName
                : $"{contact.FirstName} {contact.LastName}".Trim();

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = displayName;

            if (alreadyTransferred.Contains(contact.Id)) continue;

            // Duplicate check
            var isDup = cloudContacts.Any(c =>
                (!string.IsNullOrEmpty(contact.CompanyName) &&
                 string.Equals(c.CompanyName, contact.CompanyName, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(c.FirstName, contact.FirstName, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(c.LastName, contact.LastName, StringComparison.OrdinalIgnoreCase) &&
                 !string.IsNullOrEmpty(contact.FirstName)));

            if (isDup)
            {
                await LogItemSkipped(transferDb, sessionId, "Contacts", contact.Id, displayName, ct);
                continue;
            }

            // Create contact
            var createRequest = new
            {
                contact.FirstName,
                contact.LastName,
                contact.CompanyName,
                contact.MiddleName,
                contact.PreferredName,
                contact.Title,
                contact.BirthYear,
                contact.BirthMonth,
                contact.BirthDay,
                contact.BirthDatePrecision,
                contact.Gender,
                contact.Notes
            };
            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/contacts", createRequest, ct);

            if (!result.IsSuccess || result.Data == null)
            {
                await LogItemFailed(transferDb, sessionId, "Contacts", contact.Id, displayName, result.ErrorMessage, ct);
                continue;
            }

            var cloudId = result.Data.Id;
            await LogItemCreated(transferDb, sessionId, "Contacts", contact.Id, cloudId, displayName, ct);

            // Sub-resources: addresses, phones, emails, social media
            foreach (var addr in contact.Addresses)
                await cloudClient.PostAsync<object, object>($"api/v1/contacts/{cloudId}/addresses",
                    new { addr.Tag, addr.IsPrimary, addr.Label }, ct);

            foreach (var phone in contact.PhoneNumbers)
                await cloudClient.PostAsync<object, object>($"api/v1/contacts/{cloudId}/phones",
                    new { phone.PhoneNumber, phone.Tag, phone.IsPrimary, phone.Label }, ct);

            foreach (var email in contact.EmailAddresses)
                await cloudClient.PostAsync<object, object>($"api/v1/contacts/{cloudId}/emails",
                    new { email.Email, email.Tag, email.IsPrimary, email.Label }, ct);

            foreach (var social in contact.SocialMedia)
                await cloudClient.PostAsync<object, object>($"api/v1/contacts/{cloudId}/social-media",
                    new { social.Service, social.Username, social.ProfileUrl }, ct);
        }
    }

    private async Task TransferProductsAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var products = await db.Products
            .Include(p => p.Barcodes)
            .AsNoTracking()
            .ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = products.Count;

        var cloudResult = await cloudClient.GetAsync<List<CloudNamedDto>>(
            "api/v1/products", ct);
        var cloudProducts = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<CloudNamedDto>();

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Products", ct);

        // Get ID mappings for reference data
        var locationMap = await GetIdMap(transferDb, sessionId, "Locations", ct);
        var quantityUnitMap = await GetIdMap(transferDb, sessionId, "Quantity Units", ct);
        var productGroupMap = await GetIdMap(transferDb, sessionId, "Product Groups", ct);
        var shoppingLocationMap = await GetIdMap(transferDb, sessionId, "Shopping Locations", ct);

        for (var i = 0; i < products.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var product = products[i];

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = product.Name;

            if (alreadyTransferred.Contains(product.Id)) continue;

            if (cloudProducts.Any(c => string.Equals(c.Name, product.Name, StringComparison.OrdinalIgnoreCase)))
            {
                await LogItemSkipped(transferDb, sessionId, "Products", product.Id, product.Name, ct);
                continue;
            }

            var createRequest = new
            {
                product.Name,
                product.Description,
                LocationId = MapId(locationMap, (Guid?)product.LocationId),
                QuantityUnitIdPurchase = MapId(quantityUnitMap, (Guid?)product.QuantityUnitIdPurchase),
                QuantityUnitIdStock = MapId(quantityUnitMap, (Guid?)product.QuantityUnitIdStock),
                product.QuantityUnitFactorPurchaseToStock,
                ProductGroupId = MapId(productGroupMap, product.ProductGroupId),
                ShoppingLocationId = MapId(shoppingLocationMap, product.ShoppingLocationId),
                product.MinStockAmount,
                product.DefaultBestBeforeDays,
                product.TracksBestBeforeDate,
            };

            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/products", createRequest, ct);

            if (!result.IsSuccess || result.Data == null)
            {
                await LogItemFailed(transferDb, sessionId, "Products", product.Id, product.Name, result.ErrorMessage, ct);
                continue;
            }

            var cloudId = result.Data.Id;
            await LogItemCreated(transferDb, sessionId, "Products", product.Id, cloudId, product.Name, ct);

            // Barcodes
            foreach (var barcode in product.Barcodes)
                await cloudClient.PostAsync<object, object>($"api/v1/products/{cloudId}/barcodes",
                    new { barcode.Barcode, barcode.Note }, ct);
        }
    }

    private async Task TransferEquipmentAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb,
        bool includeHistory, CancellationToken ct)
    {
        var equipment = await db.Equipment
            .AsNoTracking()
            .ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = equipment.Count;

        var cloudResult = await cloudClient.GetAsync<List<CloudNamedDto>>(
            "api/v1/equipment", ct);
        var cloudEquipment = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<CloudNamedDto>();

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Equipment", ct);
        var categoryMap = await GetIdMap(transferDb, sessionId, "Equipment Categories", ct);

        for (var i = 0; i < equipment.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = equipment[i];

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = item.Name;

            if (alreadyTransferred.Contains(item.Id)) continue;

            if (cloudEquipment.Any(c => string.Equals(c.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
            {
                await LogItemSkipped(transferDb, sessionId, "Equipment", item.Id, item.Name, ct);
                continue;
            }

            var createRequest = new
            {
                item.Name,
                item.Description,
                item.Manufacturer,
                item.ModelNumber,
                item.SerialNumber,
                item.PurchaseDate,
                item.Location,
                item.WarrantyExpirationDate,
                item.Notes,
                CategoryId = MapId(categoryMap, item.CategoryId),
            };

            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/equipment", createRequest, ct);

            if (!result.IsSuccess || result.Data == null)
            {
                await LogItemFailed(transferDb, sessionId, "Equipment", item.Id, item.Name, result.ErrorMessage, ct);
                continue;
            }

            var cloudId = result.Data.Id;
            await LogItemCreated(transferDb, sessionId, "Equipment", item.Id, cloudId, item.Name, ct);

            // History sub-resources
            if (includeHistory)
            {
                var maintenanceRecords = await db.EquipmentMaintenanceRecords
                    .Where(r => r.EquipmentId == item.Id).AsNoTracking().ToListAsync(ct);
                foreach (var record in maintenanceRecords)
                    await cloudClient.PostAsync<object, object>($"api/v1/equipment/{cloudId}/maintenance",
                        new { record.Description, record.CompletedDate, record.UsageAtCompletion, record.Notes }, ct);

                var usageLogs = await db.EquipmentUsageLogs
                    .Where(r => r.EquipmentId == item.Id).AsNoTracking().ToListAsync(ct);
                foreach (var log in usageLogs)
                    await cloudClient.PostAsync<object, object>($"api/v1/equipment/{cloudId}/usage",
                        new { log.Date, log.Reading, log.Notes }, ct);
            }
        }
    }

    private async Task TransferVehiclesAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb,
        bool includeHistory, CancellationToken ct)
    {
        var vehicles = await db.Vehicles.AsNoTracking().ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = vehicles.Count;

        var cloudResult = await cloudClient.GetAsync<List<CloudVehicleDto>>(
            "api/v1/vehicles", ct);
        var cloudVehicles = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<CloudVehicleDto>();

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Vehicles", ct);

        for (var i = 0; i < vehicles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var vehicle = vehicles[i];
            var displayName = $"{vehicle.Year} {vehicle.Make} {vehicle.Model}";

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = displayName;

            if (alreadyTransferred.Contains(vehicle.Id)) continue;

            if (cloudVehicles.Any(c =>
                c.Year == vehicle.Year &&
                string.Equals(c.Make, vehicle.Make, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Model, vehicle.Model, StringComparison.OrdinalIgnoreCase)))
            {
                await LogItemSkipped(transferDb, sessionId, "Vehicles", vehicle.Id, displayName, ct);
                continue;
            }

            var createRequest = new
            {
                vehicle.Year,
                vehicle.Make,
                vehicle.Model,
                vehicle.Trim,
                vehicle.Vin,
                vehicle.LicensePlate,
                vehicle.Color,
                vehicle.PurchaseDate,
                vehicle.PurchasePrice,
                vehicle.CurrentMileage,
                vehicle.Notes
            };

            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/vehicles", createRequest, ct);

            if (!result.IsSuccess || result.Data == null)
            {
                await LogItemFailed(transferDb, sessionId, "Vehicles", vehicle.Id, displayName, result.ErrorMessage, ct);
                continue;
            }

            var cloudId = result.Data.Id;
            await LogItemCreated(transferDb, sessionId, "Vehicles", vehicle.Id, cloudId, displayName, ct);

            // Schedules
            var schedules = await db.VehicleMaintenanceSchedules
                .Where(s => s.VehicleId == vehicle.Id).AsNoTracking().ToListAsync(ct);
            foreach (var schedule in schedules)
                await cloudClient.PostAsync<object, object>($"api/v1/vehicles/{cloudId}/schedules",
                    new { schedule.Name, schedule.Description, schedule.IntervalMiles, schedule.IntervalMonths, schedule.Notes, schedule.IsActive }, ct);

            if (includeHistory)
            {
                var mileageLogs = await db.VehicleMileageLogs
                    .Where(m => m.VehicleId == vehicle.Id).AsNoTracking().ToListAsync(ct);
                foreach (var log in mileageLogs)
                    await cloudClient.PostAsync<object, object>($"api/v1/vehicles/{cloudId}/mileage",
                        new { log.Mileage, log.ReadingDate, log.Notes }, ct);

                var maintenanceRecords = await db.VehicleMaintenanceRecords
                    .Where(r => r.VehicleId == vehicle.Id).AsNoTracking().ToListAsync(ct);
                foreach (var record in maintenanceRecords)
                    await cloudClient.PostAsync<object, object>($"api/v1/vehicles/{cloudId}/maintenance",
                        new { record.Description, record.CompletedDate, record.Cost, record.MileageAtCompletion, record.ServiceProvider, record.Notes }, ct);
            }
        }
    }

    private async Task TransferRecipesAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var recipes = await db.Recipes
            .Include(r => r.Steps)
                .ThenInclude(s => s.Ingredients)
            .AsNoTracking()
            .ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = recipes.Count;

        var cloudResult = await cloudClient.GetAsync<List<CloudNamedDto>>(
            "api/v1/recipes", ct);
        var cloudRecipes = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<CloudNamedDto>();

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Recipes", ct);
        var productMap = await GetIdMap(transferDb, sessionId, "Products", ct);
        var quantityUnitMap = await GetIdMap(transferDb, sessionId, "Quantity Units", ct);

        for (var i = 0; i < recipes.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var recipe = recipes[i];

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = recipe.Name;

            if (alreadyTransferred.Contains(recipe.Id)) continue;

            if (cloudRecipes.Any(c => string.Equals(c.Name, recipe.Name, StringComparison.OrdinalIgnoreCase)))
            {
                await LogItemSkipped(transferDb, sessionId, "Recipes", recipe.Id, recipe.Name, ct);
                continue;
            }

            var createRequest = new
            {
                recipe.Name,
                recipe.Servings,
                recipe.Source,
                recipe.Notes,
                recipe.Attribution,
                recipe.IsMeal,
            };

            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/recipes", createRequest, ct);

            if (!result.IsSuccess || result.Data == null)
            {
                await LogItemFailed(transferDb, sessionId, "Recipes", recipe.Id, recipe.Name, result.ErrorMessage, ct);
                continue;
            }

            var cloudId = result.Data.Id;
            await LogItemCreated(transferDb, sessionId, "Recipes", recipe.Id, cloudId, recipe.Name, ct);

            // Steps and ingredients
            foreach (var step in recipe.Steps.OrderBy(s => s.StepOrder))
            {
                var stepResult = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                    $"api/v1/recipes/{cloudId}/steps",
                    new { step.Instructions, step.StepOrder, step.Title, step.Description }, ct);

                if (stepResult.IsSuccess && stepResult.Data != null)
                {
                    var cloudStepId = stepResult.Data.Id;
                    foreach (var ingredient in step.Ingredients)
                    {
                        await cloudClient.PostAsync<object, object>(
                            $"api/v1/recipes/{cloudId}/steps/{cloudStepId}/ingredients",
                            new
                            {
                                ProductId = MapId(productMap, ingredient.ProductId),
                                ingredient.Amount,
                                QuantityUnitId = MapId(quantityUnitMap, ingredient.QuantityUnitId),
                                ingredient.Note,
                                ingredient.OnlyCheckSingleUnitInStock
                            }, ct);
                    }
                }
            }
        }
    }

    private async Task TransferChoresAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var chores = await db.Chores.AsNoTracking().ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = chores.Count;

        var cloudResult = await cloudClient.GetAsync<List<CloudNamedDto>>(
            "api/v1/chores", ct);
        var cloudChores = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<CloudNamedDto>();

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Chores", ct);

        for (var i = 0; i < chores.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var chore = chores[i];

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = chore.Name;

            if (alreadyTransferred.Contains(chore.Id)) continue;

            if (cloudChores.Any(c => string.Equals(c.Name, chore.Name, StringComparison.OrdinalIgnoreCase)))
            {
                await LogItemSkipped(transferDb, sessionId, "Chores", chore.Id, chore.Name, ct);
                continue;
            }

            var createRequest = new
            {
                chore.Name,
                chore.Description,
                chore.PeriodType,
                chore.PeriodDays,
            };

            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/chores", createRequest, ct);

            if (result.IsSuccess && result.Data != null)
                await LogItemCreated(transferDb, sessionId, "Chores", chore.Id, result.Data.Id, chore.Name, ct);
            else
                await LogItemFailed(transferDb, sessionId, "Chores", chore.Id, chore.Name, result.ErrorMessage, ct);
        }
    }

    private async Task TransferChoreLogsAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var logs = await db.ChoresLog.AsNoTracking().ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = logs.Count;

        var choreMap = await GetIdMap(transferDb, sessionId, "Chores", ct);
        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Chore Logs", ct);

        // Batch import via cloud transfer endpoint
        var batch = new List<object>();
        var logEntries = new List<(Guid sourceId, string name)>();

        for (var i = 0; i < logs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var log = logs[i];

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = $"Chore log {i + 1}";

            if (alreadyTransferred.Contains(log.Id)) continue;

            var cloudChoreId = MapId(choreMap, log.ChoreId);
            if (cloudChoreId == null)
            {
                await LogItemFailed(transferDb, sessionId, "Chore Logs", log.Id, $"Log {i + 1}",
                    "Chore not found in cloud", ct);
                continue;
            }

            batch.Add(new
            {
                ChoreId = cloudChoreId.Value,
                TrackedTime = log.TrackedTime,
                WasSkipped = log.Skipped
            });
            logEntries.Add((log.Id, $"Chore log {i + 1}"));
        }

        if (batch.Count > 0)
        {
            var result = await cloudClient.PostAsync<object, object>(
                "api/v1/transfer/chore-logs", batch, ct);

            foreach (var (sourceId, name) in logEntries)
            {
                if (result.IsSuccess)
                    await LogItemCreated(transferDb, sessionId, "Chore Logs", sourceId, null, name, ct);
                else
                    await LogItemFailed(transferDb, sessionId, "Chore Logs", sourceId, name, result.ErrorMessage, ct);
            }
        }
    }

    private async Task TransferTodoItemsAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var items = await db.TodoItems.AsNoTracking().ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = items.Count;

        var cloudResult = await cloudClient.GetAsync<List<CloudTodoDto>>(
            "api/v1/todoitems", ct);
        var cloudItems = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<CloudTodoDto>();

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Todo Items", ct);

        for (var i = 0; i < items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = items[i];

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = item.Reason;

            if (alreadyTransferred.Contains(item.Id)) continue;

            if (cloudItems.Any(c => string.Equals(c.Reason, item.Reason, StringComparison.OrdinalIgnoreCase)))
            {
                await LogItemSkipped(transferDb, sessionId, "Todo Items", item.Id, item.Reason, ct);
                continue;
            }

            var createRequest = new { item.Reason, item.Description, item.TaskType };

            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/todoitems", createRequest, ct);

            if (result.IsSuccess && result.Data != null)
                await LogItemCreated(transferDb, sessionId, "Todo Items", item.Id, result.Data.Id, item.Reason, ct);
            else
                await LogItemFailed(transferDb, sessionId, "Todo Items", item.Id, item.Reason, result.ErrorMessage, ct);
        }
    }

    private async Task TransferShoppingListsAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var lists = await db.ShoppingLists
            .Include(l => l.Items)
            .AsNoTracking()
            .ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = lists.Count;

        var cloudResult = await cloudClient.GetAsync<List<CloudNamedDto>>(
            "api/v1/shoppinglists", ct);
        var cloudLists = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<CloudNamedDto>();

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Shopping Lists", ct);
        var productMap = await GetIdMap(transferDb, sessionId, "Products", ct);

        for (var i = 0; i < lists.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var list = lists[i];

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = list.Name;

            if (alreadyTransferred.Contains(list.Id)) continue;

            if (cloudLists.Any(c => string.Equals(c.Name, list.Name, StringComparison.OrdinalIgnoreCase)))
            {
                await LogItemSkipped(transferDb, sessionId, "Shopping Lists", list.Id, list.Name, ct);
                continue;
            }

            var createRequest = new { list.Name, list.Description };

            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/shoppinglists", createRequest, ct);

            if (!result.IsSuccess || result.Data == null)
            {
                await LogItemFailed(transferDb, sessionId, "Shopping Lists", list.Id, list.Name, result.ErrorMessage, ct);
                continue;
            }

            var cloudId = result.Data.Id;
            await LogItemCreated(transferDb, sessionId, "Shopping Lists", list.Id, cloudId, list.Name, ct);

            // Items
            foreach (var item in list.Items ?? [])
                await cloudClient.PostAsync<object, object>($"api/v1/shoppinglists/{cloudId}/items",
                    new
                    {
                        ProductId = MapId(productMap, item.ProductId),
                        item.Amount,
                        item.ProductName,
                        item.Note,
                        item.IsPurchased
                    }, ct);
        }
    }

    private async Task TransferStorageBinsAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var bins = await db.StorageBins.AsNoTracking().ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = bins.Count;

        var cloudResult = await cloudClient.GetAsync<List<CloudStorageBinDto>>(
            "api/v1/storage-bins", ct);
        var cloudBins = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<CloudStorageBinDto>();

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Storage Bins", ct);
        var locationMap = await GetIdMap(transferDb, sessionId, "Locations", ct);

        for (var i = 0; i < bins.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var bin = bins[i];
            var displayName = $"{bin.Category}: {bin.ShortCode}";

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = displayName;

            if (alreadyTransferred.Contains(bin.Id)) continue;

            if (cloudBins.Any(c =>
                string.Equals(c.Category, bin.Category, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.ShortCode, bin.ShortCode, StringComparison.OrdinalIgnoreCase)))
            {
                await LogItemSkipped(transferDb, sessionId, "Storage Bins", bin.Id, displayName, ct);
                continue;
            }

            var createRequest = new
            {
                bin.Category,
                bin.ShortCode,
                bin.Description,
                LocationId = MapId(locationMap, bin.LocationId),
            };

            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/storage-bins", createRequest, ct);

            if (result.IsSuccess && result.Data != null)
                await LogItemCreated(transferDb, sessionId, "Storage Bins", bin.Id, result.Data.Id, displayName, ct);
            else
                await LogItemFailed(transferDb, sessionId, "Storage Bins", bin.Id, displayName, result.ErrorMessage, ct);
        }
    }

    private async Task TransferHomeAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var home = await db.Homes.AsNoTracking().FirstOrDefaultAsync(ct);
        if (home == null)
        {
            _currentProgress!.TotalItemsInCategory = 0;
            return;
        }

        _currentProgress!.TotalItemsInCategory = 1;
        _currentProgress.CurrentItemIndex = 0;
        _currentProgress.CurrentItemName = "Home";

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Home", ct);
        if (alreadyTransferred.Contains(home.Id)) return;

        // Update/merge home data
        var updateRequest = new
        {
            home.Unit,
            home.YearBuilt,
            home.SquareFootage,
            home.Bedrooms,
            home.Bathrooms,
            home.HoaName,
            home.HoaContactInfo,
            home.AcFilterSizes,
            home.AcFilterReplacementIntervalDays,
        };

        var result = await cloudClient.PutAsync<object, object>("api/v1/home", updateRequest, ct);

        if (result.IsSuccess)
            await LogItemCreated(transferDb, sessionId, "Home", home.Id, null, "Home", ct);
        else
            await LogItemFailed(transferDb, sessionId, "Home", home.Id, "Home", result.ErrorMessage, ct);

        // Utilities
        var utilities = await db.HomeUtilities
            .Where(u => u.HomeId == home.Id).AsNoTracking().ToListAsync(ct);
        foreach (var utility in utilities)
            await cloudClient.PostAsync<object, object>("api/v1/home/utilities",
                new { utility.UtilityType, utility.CompanyName, utility.AccountNumber, utility.PhoneNumber, utility.Website, utility.LoginEmail, utility.Notes }, ct);

        // Property links
        var propertyLinks = await db.PropertyLinks
            .Where(p => p.HomeId == home.Id).AsNoTracking().ToListAsync(ct);
        foreach (var link in propertyLinks)
            await cloudClient.PostAsync<object, object>("api/v1/home/property-links",
                new { link.Label, link.Url, link.SortOrder }, ct);
    }

    private async Task TransferCalendarEventsAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var events = await db.CalendarEvents
            .Include(e => e.Members)
            .AsNoTracking()
            .ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = events.Count;

        var cloudResult = await cloudClient.GetAsync<List<CloudCalendarEventDto>>(
            "api/v1/calendar/events", ct);
        var cloudEvents = cloudResult.IsSuccess ? cloudResult.Data ?? new() : new List<CloudCalendarEventDto>();

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Calendar Events", ct);

        for (var i = 0; i < events.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var ev = events[i];

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = ev.Title;

            if (alreadyTransferred.Contains(ev.Id)) continue;

            if (cloudEvents.Any(c =>
                string.Equals(c.Title, ev.Title, StringComparison.OrdinalIgnoreCase) &&
                c.StartTimeUtc == ev.StartTimeUtc))
            {
                await LogItemSkipped(transferDb, sessionId, "Calendar Events", ev.Id, ev.Title, ct);
                continue;
            }

            var createRequest = new
            {
                ev.Title,
                ev.Description,
                ev.StartTimeUtc,
                ev.EndTimeUtc,
                ev.IsAllDay,
                ev.Color,
                ev.RecurrenceRule,
                ev.Location,
            };

            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/calendar/events", createRequest, ct);

            if (result.IsSuccess && result.Data != null)
                await LogItemCreated(transferDb, sessionId, "Calendar Events", ev.Id, result.Data.Id, ev.Title, ct);
            else
                await LogItemFailed(transferDb, sessionId, "Calendar Events", ev.Id, ev.Title, result.ErrorMessage, ct);
        }
    }

    private async Task TransferStockAsync(
        Guid sessionId, CloudApiClient cloudClient,
        HomeManagementDbContext db, TransferDbContext transferDb, CancellationToken ct)
    {
        var stockEntries = await db.Stock.AsNoTracking().ToListAsync(ct);

        _currentProgress!.TotalItemsInCategory = stockEntries.Count;

        var alreadyTransferred = await GetAlreadyTransferred(transferDb, sessionId, "Stock", ct);
        var productMap = await GetIdMap(transferDb, sessionId, "Products", ct);
        var locationMap = await GetIdMap(transferDb, sessionId, "Locations", ct);

        for (var i = 0; i < stockEntries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var entry = stockEntries[i];

            _currentProgress.CurrentItemIndex = i;
            _currentProgress.CurrentItemName = $"Stock entry {i + 1}";

            if (alreadyTransferred.Contains(entry.Id)) continue;

            var cloudProductId = MapId(productMap, entry.ProductId);
            if (cloudProductId == null)
            {
                await LogItemFailed(transferDb, sessionId, "Stock", entry.Id, $"Stock {i + 1}",
                    "Product not found in cloud", ct);
                continue;
            }

            var createRequest = new
            {
                ProductId = cloudProductId.Value,
                Amount = entry.Amount,
                BestBeforeDate = entry.BestBeforeDate,
                PurchasedDate = entry.PurchasedDate,
                Price = entry.Price,
                LocationId = MapId(locationMap, entry.LocationId),
                OpenedDate = entry.OpenedDate,
                Note = entry.Note
            };

            var result = await cloudClient.PostAsync<object, CloudCreatedResponse>(
                "api/v1/stock", createRequest, ct);

            if (result.IsSuccess)
                await LogItemCreated(transferDb, sessionId, "Stock", entry.Id, result.Data?.Id, $"Stock {i + 1}", ct);
            else
                await LogItemFailed(transferDb, sessionId, "Stock", entry.Id, $"Stock {i + 1}", result.ErrorMessage, ct);
        }
    }

    #endregion

    #region Helpers

    private async Task LogItemAsync(TransferDbContext db, Guid sessionId, string category,
        Guid sourceId, Guid? cloudId, string? name, TransferItemStatus status, string? error, CancellationToken ct)
    {
        db.TransferItemLogs.Add(new TransferItemLog
        {
            Id = Guid.NewGuid(),
            TransferSessionId = sessionId,
            Category = category,
            SourceId = sourceId,
            CloudId = cloudId,
            Name = name,
            Status = status,
            ErrorMessage = error,
            TransferredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private Task LogItemCreated(TransferDbContext db, Guid sessionId, string category,
        Guid sourceId, Guid? cloudId, string? name, CancellationToken ct)
    {
        _currentProgress!.CategoryCreatedCount++;
        _currentProgress.LastItemStatus = TransferItemStatus.Created;
        return LogItemAsync(db, sessionId, category, sourceId, cloudId, name, TransferItemStatus.Created, null, ct);
    }

    private Task LogItemSkipped(TransferDbContext db, Guid sessionId, string category,
        Guid sourceId, string? name, CancellationToken ct)
    {
        _currentProgress!.CategorySkippedCount++;
        _currentProgress.LastItemStatus = TransferItemStatus.Skipped;
        return LogItemAsync(db, sessionId, category, sourceId, null, name, TransferItemStatus.Skipped, null, ct);
    }

    private Task LogItemFailed(TransferDbContext db, Guid sessionId, string category,
        Guid sourceId, string? name, string? error, CancellationToken ct)
    {
        _currentProgress!.CategoryFailedCount++;
        _currentProgress.LastItemStatus = TransferItemStatus.Failed;
        return LogItemAsync(db, sessionId, category, sourceId, null, name, TransferItemStatus.Failed, error, ct);
    }

    private async Task<HashSet<Guid>> GetAlreadyTransferred(TransferDbContext db, Guid sessionId, string category, CancellationToken ct)
    {
        return await db.TransferItemLogs
            .Where(i => i.TransferSessionId == sessionId && i.Category == category)
            .Select(i => i.SourceId)
            .ToHashSetAsync(ct);
    }

    private async Task<Dictionary<Guid, Guid>> GetIdMap(TransferDbContext db, Guid sessionId, string category, CancellationToken ct)
    {
        return await db.TransferItemLogs
            .Where(i => i.TransferSessionId == sessionId && i.Category == category && i.CloudId != null)
            .ToDictionaryAsync(i => i.SourceId, i => i.CloudId!.Value, ct);
    }

    private static Guid? MapId(Dictionary<Guid, Guid> map, Guid? localId)
    {
        if (localId == null) return null;
        return map.TryGetValue(localId.Value, out var cloudId) ? cloudId : null;
    }

    private async Task SaveSessionAuth(string email, string? refreshToken, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();

        var session = await transferDb.TransferSessions
            .Where(s => s.Status == TransferSessionStatus.InProgress)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (session != null)
        {
            session.CloudEmail = email;
            session.EncryptedRefreshToken = refreshToken;
            await transferDb.SaveChangesAsync(ct);
        }
    }

    private async Task UpdateSessionCategory(Guid sessionId, string category, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
        var session = await transferDb.TransferSessions.FindAsync(new object[] { sessionId }, ct);
        if (session != null)
        {
            session.CurrentCategory = category;
            await transferDb.SaveChangesAsync(ct);
        }
    }

    private async Task<bool> IsCategoryComplete(Guid sessionId, string category, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
        return await transferDb.TransferItemLogs
            .AnyAsync(i => i.TransferSessionId == sessionId && i.Category == category, ct);
    }

    private async Task UpdateCompletedCategorySummary(Guid sessionId, string category, int index, int total, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
        var items = await transferDb.TransferItemLogs
            .Where(i => i.TransferSessionId == sessionId && i.Category == category)
            .ToListAsync(ct);

        _currentProgress!.CompletedCategories.Add(new TransferCategorySummary
        {
            Category = category,
            CreatedCount = items.Count(i => i.Status == TransferItemStatus.Created),
            SkippedCount = items.Count(i => i.Status == TransferItemStatus.Skipped),
            FailedCount = items.Count(i => i.Status == TransferItemStatus.Failed)
        });

        UpdateOverallProgress(index + 1, total);
    }

    private async Task CompleteSession(Guid sessionId, TransferSessionStatus status, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
        var session = await transferDb.TransferSessions.FindAsync(new object[] { sessionId }, ct);
        if (session != null)
        {
            session.Status = status;
            session.CompletedAt = DateTime.UtcNow;
            await transferDb.SaveChangesAsync(ct);
        }
    }

    private void UpdateOverallProgress(int completedCategories, int totalCategories)
    {
        if (_currentProgress != null && totalCategories > 0)
            _currentProgress.OverallProgressPercent = (double)completedCategories / totalCategories * 100;
    }

    #endregion

    #region Cloud DTO types for deserialization

    // Minimal DTOs used only for deserialization of cloud API responses.
    // Named to avoid conflicts with shared DTOs.

    private record CloudCreatedResponse(Guid Id);

    private record CloudNamedDto(Guid Id, string Name);

    private record CloudContactDto(Guid Id, string? FirstName, string? LastName, string? CompanyName);

    private record CloudVehicleDto(Guid Id, int Year, string Make, string Model);

    private record CloudTodoDto(Guid Id, string Reason);

    private record CloudStorageBinDto(Guid Id, string Category, string ShortCode);

    private record CloudCalendarEventDto(Guid Id, string Title, DateTime StartTimeUtc);

    #endregion
}
