using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Famick.HomeManagement.Mobile.Messages;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

/// <summary>
/// Message sent when the user taps "Check Off Parent Directly" on the child selection page.
/// </summary>
public sealed class CheckOffParentMessage(Guid itemId) : ValueChangedMessage<Guid>(itemId);

/// <summary>
/// Message sent when the user taps "Done" on the child selection page, indicating child quantities may have changed.
/// </summary>
public sealed class ChildSelectionDoneMessage(Guid itemId) : ValueChangedMessage<Guid>(itemId);

[QueryProperty(nameof(ListId), "ListId")]
[QueryProperty(nameof(ListName), "ListName")]
public partial class ShoppingSessionPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly OfflineStorageService _offlineStorage;
    private readonly ConnectivityService _connectivityService;
    private readonly ImageCacheService _imageCacheService;

    private Guid _listId;
    private ShoppingSession? _session;
    private bool _isPopulatingItems;
    private Guid? _bestBeforePromptItemId; // guards against async CheckedChanged during prompt
    private CachedShoppingListItem? _detailItem;

    public string ListId
    {
        set => _listId = Guid.Parse(value);
    }

    public string ListName { get; set; } = "Shopping";

    public ObservableCollection<ItemGroup> GroupedItems { get; } = new();

    public ICommand RemoveItemCommand { get; }
    public ICommand ToggleItemCommand { get; }
    public ICommand ShowItemDetailCommand { get; }

    public ShoppingSessionPage(
        ShoppingApiClient apiClient,
        OfflineStorageService offlineStorage,
        ConnectivityService connectivityService,
        ImageCacheService imageCacheService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _offlineStorage = offlineStorage;
        _connectivityService = connectivityService;
        _imageCacheService = imageCacheService;

        RemoveItemCommand = new Command<CachedShoppingListItem>(async item => await RemoveItemAsync(item));
        ToggleItemCommand = new Command<CachedShoppingListItem>(async item => await ToggleItemAsync(item));
        ShowItemDetailCommand = new Command<CachedShoppingListItem>(ShowItemDetail);

        BindingContext = this;

        _connectivityService.ConnectivityChanged += OnConnectivityChanged;

        WeakReferenceMessenger.Default.Register<CheckOffParentMessage>(this, async (recipient, message) =>
        {
            var item = _session?.Items.FirstOrDefault(i => i.Id == message.Value);
            if (item != null && !item.IsPurchased)
            {
                await ToggleItemAsync(item);
            }
        });

        WeakReferenceMessenger.Default.Register<BleScannerBarcodeMessage>(this, async (recipient, message) =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await HandleScannedBarcodeAsync(message.Value);
            });
        });

        // ChildSelectionDoneMessage no longer needed — LoadSessionAsync always fetches fresh data
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        PageTitleLabel.Text = ListName;
        UpdateConnectivityUI();

        await LoadSessionAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _connectivityService.ConnectivityChanged -= OnConnectivityChanged;
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private async Task LoadSessionAsync()
    {
        ShowLoading(true);

        try
        {
            if (_connectivityService.IsOnline)
            {
                // Always fetch fresh data from server when online
                var result = await _apiClient.GetShoppingListAsync(_listId);
                if (result.Success && result.Data != null)
                {
                    _session = await _offlineStorage.CacheShoppingSessionAsync(_listId, result.Data);
                    await _imageCacheService.CacheImagesForSessionAsync(_session, result.Data.Items);

                    // Persist cached image paths back to SQLite
                    foreach (var item in _session.Items.Where(i => !string.IsNullOrEmpty(i.LocalImagePath)))
                    {
                        await _offlineStorage.UpdateItemStateAsync(item);
                    }
                }
                else
                {
                    // Server fetch failed - fall back to cache
                    _session = await _offlineStorage.GetCachedSessionAsync(_listId);
                    if (_session == null)
                    {
                        await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to load list", "OK");
                        await Shell.Current.GoToAsync("..");
                        return;
                    }
                }
            }
            else
            {
                // Offline - use cached data
                _session = await _offlineStorage.GetCachedSessionAsync(_listId);
            }

            if (_session != null)
            {
                PopulateItems();
                UpdateSubtotal();
            }
            else
            {
                await DisplayAlertAsync("Error", "Unable to load shopping list. Please try again online.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private void PopulateItems()
    {
        if (_session == null) return;

        // Set flag to prevent OnItemCheckedChanged from queueing operations during UI binding
        _isPopulatingItems = true;

        try
        {
            GroupedItems.Clear();

            // Separate unpurchased and purchased items
            var unpurchased = _session.Items.Where(i => !i.IsPurchased).OrderBy(i => i.SortOrder).ToList();
            var purchased = _session.Items.Where(i => i.IsPurchased).OrderBy(i => i.SortOrder).ToList();

            // Group unpurchased items by aisle/department, ordered by custom aisle order
            var groups = unpurchased
                .GroupBy(i => string.IsNullOrEmpty(i.Aisle)
                    ? i.Department ?? "Other"
                    : int.TryParse(i.Aisle, out _) ? $"Aisle {i.Aisle}" : i.Aisle)
                .OrderBy(g => g.Min(i => i.SortOrder)); // Order groups by their first item's sort order

            foreach (var group in groups)
            {
                var itemGroup = new ItemGroup(group.Key, group.ToList());
                GroupedItems.Add(itemGroup);
            }

            // Add purchased items as a separate group at the bottom
            if (purchased.Count > 0)
            {
                var purchasedGroup = new ItemGroup($"Purchased ({purchased.Count})", purchased);
                GroupedItems.Add(purchasedGroup);
            }
        }
        finally
        {
            // Use a small delay to ensure UI binding is complete before enabling change tracking
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _isPopulatingItems = false;
            });
        }
    }

    /// <summary>
    /// Moves an item from its current group to the correct group (purchased or aisle)
    /// without clearing and rebuilding the entire GroupedItems collection.
    /// This preserves the CollectionView scroll position.
    /// </summary>
    private void MoveItemBetweenGroups(CachedShoppingListItem item)
    {
        _isPopulatingItems = true;
        try
        {
            // Remove item from its current group
            foreach (var group in GroupedItems)
            {
                if (group.Remove(item))
                    break;
            }

            // Remove any now-empty groups
            for (int i = GroupedItems.Count - 1; i >= 0; i--)
            {
                if (GroupedItems[i].Count == 0)
                    GroupedItems.RemoveAt(i);
            }

            if (item.IsPurchased)
            {
                // Add to purchased group
                var purchasedGroup = GroupedItems.FirstOrDefault(g => g.Key.StartsWith("Purchased"));
                if (purchasedGroup != null)
                {
                    // Update the group key to reflect new count
                    var purchasedCount = purchasedGroup.Count + 1;
                    var idx = GroupedItems.IndexOf(purchasedGroup);
                    purchasedGroup.Add(item);

                    // Replace group to update the header text with new count
                    GroupedItems[idx] = new ItemGroup($"Purchased ({purchasedCount})", purchasedGroup);
                }
                else
                {
                    GroupedItems.Add(new ItemGroup("Purchased (1)", new[] { item }));
                }
            }
            else
            {
                // Determine the correct aisle/department group
                var groupKey = string.IsNullOrEmpty(item.Aisle)
                    ? item.Department ?? "Other"
                    : int.TryParse(item.Aisle, out _) ? $"Aisle {item.Aisle}" : item.Aisle;

                // Find existing group (exclude purchased group)
                var targetGroup = GroupedItems.FirstOrDefault(g => g.Key == groupKey && !g.Key.StartsWith("Purchased"));
                if (targetGroup != null)
                {
                    // Insert at correct sort position
                    var insertIdx = 0;
                    for (int i = 0; i < targetGroup.Count; i++)
                    {
                        if (targetGroup[i].SortOrder > item.SortOrder)
                            break;
                        insertIdx = i + 1;
                    }
                    targetGroup.Insert(insertIdx, item);
                }
                else
                {
                    // Create new group and insert before the Purchased group
                    var newGroup = new ItemGroup(groupKey, new[] { item });
                    var purchasedIdx = -1;
                    for (int i = 0; i < GroupedItems.Count; i++)
                    {
                        if (GroupedItems[i].Key.StartsWith("Purchased"))
                        {
                            purchasedIdx = i;
                            break;
                        }
                    }
                    if (purchasedIdx >= 0)
                        GroupedItems.Insert(purchasedIdx, newGroup);
                    else
                        GroupedItems.Add(newGroup);
                }

                // Update the purchased group header count if it still exists
                var existingPurchasedGroup = GroupedItems.FirstOrDefault(g => g.Key.StartsWith("Purchased"));
                if (existingPurchasedGroup != null)
                {
                    var idx = GroupedItems.IndexOf(existingPurchasedGroup);
                    GroupedItems[idx] = new ItemGroup($"Purchased ({existingPurchasedGroup.Count})", existingPurchasedGroup);
                }
            }
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => _isPopulatingItems = false);
        }
    }

    /// <summary>
    /// Removes an item from the GroupedItems without full rebuild.
    /// </summary>
    private void RemoveItemFromGroups(CachedShoppingListItem item)
    {
        _isPopulatingItems = true;
        try
        {
            foreach (var group in GroupedItems)
            {
                if (group.Remove(item))
                    break;
            }

            // Remove empty groups
            for (int i = GroupedItems.Count - 1; i >= 0; i--)
            {
                if (GroupedItems[i].Count == 0)
                    GroupedItems.RemoveAt(i);
            }

            // Update purchased group header count
            var purchasedGroup = GroupedItems.FirstOrDefault(g => g.Key.StartsWith("Purchased"));
            if (purchasedGroup != null)
            {
                var idx = GroupedItems.IndexOf(purchasedGroup);
                GroupedItems[idx] = new ItemGroup($"Purchased ({purchasedGroup.Count})", purchasedGroup);
            }
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => _isPopulatingItems = false);
        }
    }

    private void UpdateSubtotal()
    {
        if (_session == null) return;

        var itemsWithPrice = _session.Items.Where(i => i.Price.HasValue).ToList();
        var totalItems = _session.Items.Count;

        if (itemsWithPrice.Count == 0)
        {
            SubtotalLabel.Text = "No prices";
            SubtotalNote.Text = "";
        }
        else if (itemsWithPrice.Count < totalItems)
        {
            var subtotal = itemsWithPrice.Sum(i => i.Price!.Value * i.Amount);
            SubtotalLabel.Text = $"${subtotal:F2}";
            SubtotalNote.Text = $"({totalItems - itemsWithPrice.Count} items missing prices)";
        }
        else
        {
            var subtotal = itemsWithPrice.Sum(i => i.Price!.Value * i.Amount);
            SubtotalLabel.Text = $"${subtotal:F2}";
            SubtotalNote.Text = "Estimated total";
        }
    }

    private async Task ToggleItemAsync(CachedShoppingListItem? item)
    {
        if (item == null || _session == null) return;

        // Parent products with children at this store navigate to child selection
        if (item.NeedsChildSelection)
        {
            await NavigateToChildSelectionAsync(item);
            return;
        }

        // When marking as purchased and product tracks best-before dates, show popup
        // then call API and reload list from server (same pattern as OnItemCheckedChanged)
        if (!item.IsPurchased && item.TracksBestBeforeDate)
        {
            _bestBeforePromptItemId = item.Id;
            try
            {
                var (proceed, date) = await ShowBestBeforeDatePromptAsync(item);
                if (!proceed) return;

                // Always update local state first so offline cache is correct
                item.IsPurchased = true;
                item.PurchasedAt = DateTime.UtcNow;
                if (date.HasValue) item.BestBeforeDate = date.Value;
                await _offlineStorage.UpdateItemStateAsync(item);

                // Sync to server or queue for later
                if (!item.IsNewItem)
                {
                    if (_connectivityService.IsOnline)
                    {
                        await _apiClient.TogglePurchasedAsync(_listId, item.Id, date);
                    }
                    else
                    {
                        await EnqueueToggleOperationAsync(item);
                    }
                }

                // Reload the list — from server when online, from cache when offline
                await LoadSessionAsync();
            }
            finally
            {
                _bestBeforePromptItemId = null;
            }
            return;
        }

        // Standard toggle (no best-before prompt) — fast local update
        item.IsPurchased = !item.IsPurchased;
        item.PurchasedAt = item.IsPurchased ? DateTime.UtcNow : null;

        await _offlineStorage.UpdateItemStateAsync(item);

        if (!item.IsNewItem)
        {
            if (_connectivityService.IsOnline)
            {
                var result = await _apiClient.TogglePurchasedAsync(_listId, item.Id);
                if (result.Success)
                {
                    item.OriginalIsPurchased = item.IsPurchased;
                    await _offlineStorage.UpdateItemStateAsync(item);
                }
                else
                {
                    await EnqueueToggleOperationAsync(item);
                }
            }
            else
            {
                await EnqueueToggleOperationAsync(item);
            }
        }

        MoveItemBetweenGroups(item);
        UpdateSubtotal();
    }

    /// <summary>
    /// Increments PurchasedQuantity by 1 for barcode scan flow.
    /// Auto-completes when PurchasedQuantity >= Amount. No popup shown.
    /// </summary>
    private async Task ScanPurchaseItemAsync(CachedShoppingListItem item)
    {
        if (_session == null) return;

        // Increment local purchased quantity
        item.PurchasedQuantity += 1;

        // Treat Amount <= 0 as 1 for completion logic
        var effectiveAmount = item.Amount > 0 ? item.Amount : 1;

        if (item.PurchasedQuantity >= effectiveAmount && !item.IsPurchased)
        {
            item.IsPurchased = true;
            item.PurchasedAt = DateTime.UtcNow;
        }

        await _offlineStorage.UpdateItemStateAsync(item);

        if (!item.IsNewItem)
        {
            if (_connectivityService.IsOnline)
            {
                var result = await _apiClient.ScanPurchaseAsync(_listId, item.Id);
                if (result.Success)
                {
                    item.OriginalIsPurchased = item.IsPurchased;
                    await _offlineStorage.UpdateItemStateAsync(item);
                }
                else
                {
                    await EnqueueScanPurchaseOperationAsync(item);
                }
            }
            else
            {
                await EnqueueScanPurchaseOperationAsync(item);
            }
        }

        MoveItemBetweenGroups(item);
        UpdateSubtotal();
    }

    private async Task EnqueueScanPurchaseOperationAsync(CachedShoppingListItem item)
    {
        await _offlineStorage.EnqueueOperationAsync(new OfflineOperation
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OperationType = "ScanPurchase",
            PayloadJson = JsonSerializer.Serialize(new { ListId = _listId, ItemId = item.Id, Quantity = 1m })
        });
    }

    private async Task RemoveItemAsync(CachedShoppingListItem? item)
    {
        if (item == null || _session == null) return;

        // Remove from in-memory session
        _session.Items.Remove(item);

        // Remove from SQLite cache
        await _offlineStorage.RemoveItemFromSessionAsync(item.Id);

        // Try API delete if online, otherwise queue
        if (!item.IsNewItem)
        {
            if (_connectivityService.IsOnline)
            {
                var result = await _apiClient.RemoveItemAsync(_listId, item.Id);
                if (!result.Success)
                {
                    await EnqueueRemoveOperationAsync(item.Id);
                }
            }
            else
            {
                await EnqueueRemoveOperationAsync(item.Id);
            }
        }

        RemoveItemFromGroups(item);
        UpdateSubtotal();
    }

    private async Task EnqueueRemoveOperationAsync(Guid itemId)
    {
        await _offlineStorage.EnqueueOperationAsync(new OfflineOperation
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OperationType = "RemoveItem",
            PayloadJson = JsonSerializer.Serialize(new { ListId = _listId, ItemId = itemId })
        });
    }

    private async void OnItemCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        // Skip during initial UI population - we only want to track user-initiated changes
        if (_isPopulatingItems) return;

        if (sender is not CheckBox checkBox || checkBox.BindingContext is not CachedShoppingListItem item) return;

        // Skip events fired while a best-before prompt is active
        if (_bestBeforePromptItemId != null) return;

        // Parent products needing child selection: undo checkbox and navigate
        if (item.NeedsChildSelection)
        {
            _isPopulatingItems = true;
            checkBox.IsChecked = false;
            item.IsPurchased = false;
            _isPopulatingItems = false;
            _ = NavigateToChildSelectionAsync(item);
            return;
        }

        // When marking as purchased and product tracks best-before dates, show popup
        // then call the API and reload the list from the server (avoids async checkbox issues on iOS)
        if (e.Value && item.TracksBestBeforeDate)
        {
            // Immediately revert the checkbox while the popup is shown
            _isPopulatingItems = true;
            checkBox.IsChecked = false;
            item.IsPurchased = false;
            _isPopulatingItems = false;

            _bestBeforePromptItemId = item.Id;
            try
            {
                var (proceed, date) = await ShowBestBeforeDatePromptAsync(item);
                if (!proceed) return;

                // Always update local state first so offline cache is correct
                item.IsPurchased = true;
                item.PurchasedAt = DateTime.UtcNow;
                if (date.HasValue) item.BestBeforeDate = date.Value;
                await _offlineStorage.UpdateItemStateAsync(item);

                // Sync to server or queue for later
                if (!item.IsNewItem)
                {
                    if (_connectivityService.IsOnline)
                    {
                        await _apiClient.TogglePurchasedAsync(_listId, item.Id, date);
                    }
                    else
                    {
                        await EnqueueToggleOperationAsync(item);
                    }
                }

                // Reload the list — from server when online, from cache when offline
                await LoadSessionAsync();
            }
            finally
            {
                _bestBeforePromptItemId = null;
            }
            return;
        }

        // Standard toggle (no best-before prompt) — fast local update
        item.PurchasedAt = e.Value ? DateTime.UtcNow : null;
        await _offlineStorage.UpdateItemStateAsync(item);

        if (!item.IsNewItem)
        {
            if (_connectivityService.IsOnline)
            {
                var result = await _apiClient.TogglePurchasedAsync(_listId, item.Id);
                if (result.Success)
                {
                    item.OriginalIsPurchased = item.IsPurchased;
                    await _offlineStorage.UpdateItemStateAsync(item);
                }
                else
                {
                    await EnqueueToggleOperationAsync(item);
                }
            }
            else
            {
                await EnqueueToggleOperationAsync(item);
            }
        }

        MoveItemBetweenGroups(item);
        UpdateSubtotal();
    }

    private async Task EnqueueToggleOperationAsync(CachedShoppingListItem item)
    {
        // Remove any existing pending toggle for this item
        await _offlineStorage.RemovePendingToggleOperationsAsync(_listId, item.Id);

        // Only enqueue if current state differs from original
        if (item.IsPurchased != item.OriginalIsPurchased)
        {
            await _offlineStorage.EnqueueOperationAsync(new OfflineOperation
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                OperationType = "TogglePurchased",
                PayloadJson = JsonSerializer.Serialize(new { ListId = _listId, ItemId = item.Id, item.IsPurchased, item.BestBeforeDate })
            });
        }
    }

    private async void OnAisleOrderClicked(object? sender, EventArgs e)
    {
        if (_session == null)
        {
            await DisplayAlertAsync("Error", "Shopping session not loaded", "OK");
            return;
        }

        var navigationParameter = new Dictionary<string, object>
        {
            { "LocationId", _session.StoreId.ToString() },
            { "StoreName", _session.StoreName }
        };
        await Shell.Current.GoToAsync(nameof(AisleOrderPage), navigationParameter);
    }

    private async void OnAddItemClicked(object? sender, EventArgs e)
    {
        var navigationParameter = new Dictionary<string, object>
        {
            { "ListId", _listId.ToString() }
        };
        await Shell.Current.GoToAsync(nameof(AddItemPage), navigationParameter);
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        // Request camera permission before opening scanner
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlertAsync(
                    "Camera Required",
                    "Camera permission is needed to scan barcodes. Please enable it in Settings.",
                    "OK");
                return;
            }
        }

        var scannerPage = new BarcodeScannerPage();
        await Navigation.PushAsync(scannerPage);
        var barcode = await scannerPage.ScanAsync();

        if (string.IsNullOrEmpty(barcode))
            return;

        await HandleScannedBarcodeAsync(barcode);
    }

    private async Task HandleScannedBarcodeAsync(string barcode)
    {
        if (_session == null) return;

        // First try server-side lookup which checks all barcode variants and child products
        if (_connectivityService.IsOnline)
        {
            var scanResult = await _apiClient.ScanBarcodeAsync(_listId, barcode);

            if (scanResult.Success && scanResult.Data != null && scanResult.Data.Found)
            {
                var result = scanResult.Data;

                // Find the matching item in our cached session
                var cachedItem = _session.Items.FirstOrDefault(i => i.Id == result.ItemId);

                if (cachedItem != null)
                {
                    if (result.NeedsChildSelection)
                    {
                        // Navigate to child selection page
                        await NavigateToChildSelectionAsync(cachedItem);
                    }
                    else if (!result.IsChildProduct)
                    {
                        // Direct match - increment purchased quantity (no popup)
                        await ScanPurchaseItemAsync(cachedItem);
                    }
                    else
                    {
                        // Child product match - navigate to child selection
                        await NavigateToChildSelectionAsync(cachedItem);
                    }
                    return;
                }
            }
        }
        else
        {
            // Offline fallback: check cached items by barcode (single barcode + all product barcodes)
            var existingItem = _session.Items.FirstOrDefault(i =>
                i.Barcode?.Equals(barcode, StringComparison.OrdinalIgnoreCase) == true
                || i.Barcodes.Any(b => b.Equals(barcode, StringComparison.OrdinalIgnoreCase)));

            if (existingItem != null)
            {
                // Offline match - increment purchased quantity (no popup)
                await ScanPurchaseItemAsync(existingItem);
                return;
            }
        }

        // Not found via barcode scan - look up the product and check if it's already on the list
        string? resolvedProductName = null;

        if (_connectivityService.IsOnline)
        {
            // 1. Check if product already exists in inventory by barcode
            var productResult = await _apiClient.GetProductByBarcodeAsync(barcode);
            if (productResult.Success && productResult.Data != null)
            {
                var product = productResult.Data;

                // Check if this product is already on the list (by ProductId or name)
                var existingItem = _session.Items.FirstOrDefault(i =>
                    (i.ProductId.HasValue && i.ProductId == product.Id) ||
                    i.ProductName.Equals(product.Name, StringComparison.OrdinalIgnoreCase));

                if (existingItem != null && !existingItem.IsPurchased)
                {
                    // Found on list - increment purchased quantity (no popup)
                    await ScanPurchaseItemAsync(existingItem);
                    return;
                }

                // Product exists in inventory but not on the list - add and check off
                resolvedProductName = product.Name;
            }

            // 2. Try store integration lookup
            if (resolvedProductName == null)
            {
                var storeResult = await _apiClient.LookupProductByBarcodeAsync(_listId, barcode);
                if (storeResult.Success && storeResult.Data != null)
                {
                    var storeProduct = storeResult.Data;

                    // Check if this product is already on the list by name
                    var existingItem = _session.Items.FirstOrDefault(i =>
                        i.ProductName.Equals(storeProduct.Name, StringComparison.OrdinalIgnoreCase));

                    if (existingItem != null && !existingItem.IsPurchased)
                    {
                        // Found on list - increment purchased quantity (no popup)
                        await ScanPurchaseItemAsync(existingItem);
                        return;
                    }

                    // Not on list - add with store data
                    var addResult = await _apiClient.QuickAddItemAsync(
                        _listId, storeProduct.Name, 1, barcode, null,
                        isPurchased: true,
                        aisle: storeProduct.Aisle,
                        department: storeProduct.Department,
                        externalProductId: storeProduct.ExternalProductId,
                        price: storeProduct.Price,
                        imageUrl: storeProduct.ImageUrl);

                    if (addResult.Success)
                    {
                        await LoadSessionAsync();
                        await DisplayAlertAsync("Added", $"{storeProduct.Name} added and checked off", "OK");
                    }
                    else
                    {
                        await DisplayAlertAsync("Error", addResult.ErrorMessage ?? "Failed to add item", "OK");
                    }
                    return;
                }
            }
        }

        // 3. Add with known product name, or prompt if unknown
        if (resolvedProductName == null)
        {
            resolvedProductName = await DisplayPromptAsync(
                "New Product",
                $"Barcode: {barcode}\nEnter the product name:",
                "Add",
                "Cancel",
                placeholder: "Product name");

            if (string.IsNullOrWhiteSpace(resolvedProductName))
                return;

            resolvedProductName = resolvedProductName.Trim();
        }

        var quickAddResult = await _apiClient.QuickAddItemAsync(
            _listId, resolvedProductName, 1, barcode, null, isPurchased: true);

        if (quickAddResult.Success)
        {
            await LoadSessionAsync();
            await DisplayAlertAsync("Added", $"{resolvedProductName} added and checked off", "OK");
        }
        else
        {
            await DisplayAlertAsync("Error", quickAddResult.ErrorMessage ?? "Failed to add item", "OK");
        }
    }

    private async void OnCompleteClicked(object? sender, EventArgs e)
    {
        if (!_connectivityService.IsOnline)
        {
            await DisplayAlertAsync("Offline", "Please connect to the internet to complete shopping.", "OK");
            return;
        }

        if (_session == null)
        {
            await DisplayAlertAsync("Error", "No shopping session found.", "OK");
            return;
        }

        var purchasedItems = _session.Items.Where(i => i.IsPurchased).ToList();
        if (purchasedItems.Count == 0)
        {
            var confirmEmpty = await DisplayAlertAsync(
                "No Items Purchased",
                "You haven't marked any items as purchased. Do you want to exit without completing?",
                "Exit",
                "Cancel");

            if (confirmEmpty)
            {
                await _offlineStorage.ClearSessionAsync(_listId);
                await Shell.Current.GoToAsync("..");
            }
            return;
        }

        var confirm = await DisplayAlertAsync(
            "Complete Shopping?",
            $"This will move {purchasedItems.Count} purchased item(s) to your inventory.",
            "Complete",
            "Cancel");

        if (!confirm) return;

        ShowLoading(true);

        try
        {
            // Sync any pending offline operations first
            await _offlineStorage.SyncPendingOperationsAsync(_apiClient);

            // Get the shopping location ID from the session
            var shoppingLocationId = _session?.StoreId;

            var request = new MoveToInventoryRequest
            {
                ShoppingListId = _listId,
                Items = purchasedItems.Select(i => new MoveToInventoryItem
                {
                    ShoppingListItemId = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Amount = i.Amount,
                    Price = i.Price,
                    Barcode = i.Barcode,
                    ImageUrl = i.ImageUrl,
                    BestBeforeDate = i.BestBeforeDate,
                    LocationId = i.DefaultLocationId,
                    ExternalProductId = i.ExternalProductId,
                    ShoppingLocationId = shoppingLocationId,
                    Aisle = i.Aisle,
                    Shelf = i.Shelf,
                    Department = i.Department
                }).ToList()
            };

            var result = await _apiClient.MoveToInventoryAsync(request);

            if (result.Success && result.Data != null)
            {
                var message = $"Added {result.Data.ItemsAddedToStock} item(s) to inventory.";
                if (result.Data.TodoItemsCreated > 0)
                    message += $"\n{result.Data.TodoItemsCreated} item(s) need product setup.";
                if (result.Data.Errors.Count > 0)
                    message += $"\n{result.Data.Errors.Count} error(s) occurred.";

                await DisplayAlertAsync("Shopping Complete", message, "OK");

                await _offlineStorage.ClearSessionAsync(_listId);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to complete shopping", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to complete shopping: {ex.Message}", "OK");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        if (_connectivityService.IsOnline)
        {
            // Sync pending operations, then reload
            await _offlineStorage.SyncPendingOperationsAsync(_apiClient);
            await _offlineStorage.ClearSessionAsync(_listId);
            await LoadSessionAsync();
        }
        RefreshContainer.IsRefreshing = false;
    }

    private async void OnConnectivityChanged(object? sender, bool isOnline)
    {
        MainThread.BeginInvokeOnMainThread(UpdateConnectivityUI);

        if (isOnline)
        {
            // Auto-sync pending operations when coming back online
            await _offlineStorage.SyncPendingOperationsAsync(_apiClient);
        }
    }

    private void UpdateConnectivityUI()
    {
        var isOnline = _connectivityService.IsOnline;
        OfflineBanner.IsVisible = !isOnline;
        CompleteButton.IsEnabled = isOnline;
        CompleteButton.Opacity = isOnline ? 1.0 : 0.5;
    }

    /// <summary>
    /// Shows the best-before date popup and awaits the user's choice.
    /// Returns (true, date) for confirm, (true, null) for skip, (false, null) for cancel.
    /// </summary>
    private async Task<(bool proceed, DateTime? date)> ShowBestBeforeDatePromptAsync(CachedShoppingListItem item)
    {
        var popup = new Popups.BestBeforeDatePopup(item.ProductName, item.DefaultBestBeforeDays);
        var popupResult = await this.ShowPopupAsync<Popups.BestBeforeDateResult>(popup, PopupOptions.Empty, CancellationToken.None);

        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null)
        {
            Console.WriteLine("[BestBefore] Popup cancelled");
            return (false, null);
        }

        var dateResult = popupResult.Result;
        Console.WriteLine($"[BestBefore] Popup result: HasDate={dateResult.HasDate}, Date={dateResult.Date}");
        return (true, dateResult.Date);
    }

    private void ShowItemDetail(CachedShoppingListItem? item)
    {
        if (item == null) return;

        // Any unpurchased parent product navigates to child selection
        if (item.IsParentProduct && !item.IsPurchased)
        {
            _ = NavigateToChildSelectionAsync(item);
            return;
        }

        _detailItem = item;
        DetailProductName.Text = item.ProductName;
        DetailQuantity.Text = item.Amount.ToString("G");

        // Image
        if (item.HasImage)
        {
            DetailImage.Source = item.ImageSource;
            DetailImage.IsVisible = true;
            DetailNoImage.IsVisible = false;
        }
        else
        {
            DetailImage.IsVisible = false;
            DetailNoImage.IsVisible = true;
        }

        // Location
        DetailAisle.Text = !string.IsNullOrEmpty(item.Aisle)
            ? (int.TryParse(item.Aisle, out _) ? $"Aisle {item.Aisle}" : item.Aisle)
            : "—";
        DetailShelf.Text = !string.IsNullOrEmpty(item.Shelf) ? item.Shelf : "—";
        DetailDepartment.Text = !string.IsNullOrEmpty(item.Department) ? item.Department : "—";

        // Price
        DetailPriceSection.IsVisible = item.HasPrice;
        DetailPrice.Text = item.Price.HasValue ? $"${item.Price:F2}" : "";

        // Note
        if (!string.IsNullOrEmpty(item.Note))
        {
            DetailNote.Text = item.Note;
            DetailNote.IsVisible = true;
        }
        else
        {
            DetailNote.IsVisible = false;
        }

        DetailOverlay.IsVisible = true;
    }

    private void OnDetailOverlayTapped(object? sender, TappedEventArgs e)
    {
        _detailItem = null;
        DetailOverlay.IsVisible = false;
    }

    private void OnDetailCloseClicked(object? sender, EventArgs e)
    {
        _detailItem = null;
        DetailOverlay.IsVisible = false;
    }

    private async void OnDetailIncreaseQuantity(object? sender, EventArgs e)
    {
        var item = _detailItem;
        if (item == null || _session == null) return;

        // Update in memory + UI immediately (optimistic)
        item.Amount += 1;
        DetailQuantity.Text = item.Amount.ToString("G");
        PopulateItems();
        UpdateSubtotal();

        // Persist in background
        await PersistQuantityChangeAsync(item);
    }

    private async void OnDetailDecreaseQuantity(object? sender, EventArgs e)
    {
        var item = _detailItem;
        if (item == null || _session == null) return;

        if (item.Amount <= 1)
        {
            var confirm = await DisplayAlertAsync(
                "Remove Item?",
                $"Remove {item.ProductName} from the list?",
                "Remove",
                "Cancel");

            if (!confirm) return;

            _detailItem = null;
            DetailOverlay.IsVisible = false;
            await RemoveItemAsync(item);
            return;
        }

        // Update in memory + UI immediately (optimistic)
        item.Amount -= 1;
        DetailQuantity.Text = item.Amount.ToString("G");
        PopulateItems();
        UpdateSubtotal();

        // Persist in background
        await PersistQuantityChangeAsync(item);
    }

    private async Task PersistQuantityChangeAsync(CachedShoppingListItem item)
    {
        // Update local cache
        await _offlineStorage.UpdateItemStateAsync(item);

        // Sync to server or queue offline
        if (!item.IsNewItem)
        {
            if (_connectivityService.IsOnline)
            {
                await _apiClient.UpdateItemQuantityAsync(_listId, item.Id, item.Amount, item.Note);
            }
            else
            {
                await _offlineStorage.EnqueueOperationAsync(new OfflineOperation
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    OperationType = "UpdateQuantity",
                    PayloadJson = JsonSerializer.Serialize(new { ListId = _listId, ItemId = item.Id, Amount = item.Amount, item.Note })
                });
            }
        }
    }

    private async Task NavigateToChildSelectionAsync(CachedShoppingListItem item)
    {
        var navigationParameter = new Dictionary<string, object>
        {
            { "ListId", _listId.ToString() },
            { "ItemId", item.Id.ToString() },
            { "ParentName", item.ProductName },
            { "ParentAmount", item.Amount.ToString("G") },
            { "ParentImageUrl", item.ImageUrl ?? "" }
        };
        await Shell.Current.GoToAsync(nameof(ChildProductSelectionPage), navigationParameter);
    }

    private void ShowLoading(bool show)
    {
        LoadingIndicator.IsRunning = show;
        LoadingIndicator.IsVisible = show;
    }
}

/// <summary>
/// Grouping class for CollectionView
/// </summary>
public class ItemGroup : ObservableCollection<CachedShoppingListItem>
{
    public string Key { get; }

    public ItemGroup(string key, IEnumerable<CachedShoppingListItem> items) : base(items)
    {
        Key = key;
    }
}
