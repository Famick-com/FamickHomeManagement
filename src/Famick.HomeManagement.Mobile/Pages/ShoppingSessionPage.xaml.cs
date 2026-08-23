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
using Famick.HomeManagement.Shared.Barcodes;

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
    private bool _isScanning;

    // Cached parent→children barcode index, refreshed on session load while online, so a
    // scanned child of a list item can be recognized offline.
    private List<ShoppingListChildIndexEntry> _childIndex = new();

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
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Re-arm handlers on every appearance (OnDisappearing unregisters them). These were
        // previously registered in the constructor, which left BLE scanning dead after
        // navigating away and back: the page instance is reused, so the constructor doesn't
        // run again, but OnDisappearing had already called UnregisterAll(this).
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

        PageTitleLabel.Text = ListName;
        StoreNameLabel.Text = "";
        UpdateConnectivityUI();

        await LoadSessionAsync();
        if (_session != null)
        {
            StoreNameLabel.Text = _session.StoreName;
        }
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

                    // Cache the child barcode index so scanned children resolve offline.
                    var indexResult = await _apiClient.GetChildIndexAsync(_listId);
                    if (indexResult.Success && indexResult.Data != null)
                        _childIndex = indexResult.Data;
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
    /// Records a scan purchase for an item. Increments PurchasedQuantity by 1 (or by the
    /// weighed amount for a by-weight scan) and auto-completes when the needed amount is
    /// reached. If the item is already fully purchased, the user is asked whether to add
    /// an additional purchase before over-counting.
    /// </summary>
    private async Task ScanPurchaseItemAsync(CachedShoppingListItem item,
        decimal? embeddedWeight = null, decimal? embeddedPrice = null)
    {
        if (_session == null) return;

        // Treat Amount <= 0 as 1 for completion logic
        var effectiveAmount = item.Amount > 0 ? item.Amount : 1;

        // Once the needed quantity is already met, a further scan asks whether to add an
        // additional purchase rather than silently over-counting (auto until qty, then prompt).
        if (item.IsPurchased || item.PurchasedQuantity >= effectiveAmount)
        {
            var addAnother = await DisplayAlertAsync(
                "Already checked off",
                $"\"{item.ProductName}\" is already checked off. Add another purchase?",
                "Add another", "Cancel");
            if (!addAnother) return;
        }

        // Increment local purchased quantity — by the weighed amount for by-weight scans,
        // otherwise by a single unit.
        if (embeddedWeight is > 0)
            item.PurchasedQuantity += embeddedWeight.Value;
        else
            item.PurchasedQuantity += 1;

        // Record the price from a price-embedded barcode.
        if (embeddedPrice is > 0)
            item.Price = embeddedPrice;

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
                var result = await _apiClient.ScanPurchaseAsync(_listId, item.Id,
                    embeddedWeight: embeddedWeight, embeddedPrice: embeddedPrice);
                if (result.Success)
                {
                    item.OriginalIsPurchased = item.IsPurchased;
                    await _offlineStorage.UpdateItemStateAsync(item);
                }
                else
                {
                    await EnqueueScanPurchaseOperationAsync(item, embeddedWeight, embeddedPrice);
                }
            }
            else
            {
                await EnqueueScanPurchaseOperationAsync(item, embeddedWeight, embeddedPrice);
            }
        }

        MoveItemBetweenGroups(item);
        UpdateSubtotal();
    }

    private async Task EnqueueScanPurchaseOperationAsync(CachedShoppingListItem item,
        decimal? embeddedWeight = null, decimal? embeddedPrice = null)
    {
        await _offlineStorage.EnqueueOperationAsync(new OfflineOperation
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OperationType = "ScanPurchase",
            PayloadJson = JsonSerializer.Serialize(new
            {
                ListId = _listId,
                ItemId = item.Id,
                Quantity = 1m,
                EmbeddedWeight = embeddedWeight,
                EmbeddedPrice = embeddedPrice
            })
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

    private async void OnChangeStoreClicked(object? sender, EventArgs e)
    {
        if (_session == null) return;

        var storesResult = await _apiClient.GetShoppingLocationsAsync();
        if (!storesResult.Success || storesResult.Data == null || storesResult.Data.Count < 2)
        {
            await DisplayAlertAsync("Change Store", "No other stores available.", "OK");
            return;
        }

        var storeNames = storesResult.Data
            .Where(s => s.Id != _session.StoreId)
            .Select(s => s.Name)
            .ToArray();

        var selected = await DisplayActionSheet("Switch Store", "Cancel", null, storeNames);
        if (string.IsNullOrEmpty(selected) || selected == "Cancel") return;

        var newStore = storesResult.Data.First(s => s.Name == selected);

        var confirm = await DisplayAlertAsync(
            "Change Store",
            $"Switch to {newStore.Name}? Items will be re-looked up at the new store.",
            "Switch",
            "Cancel");

        if (!confirm) return;

        var request = new UpdateShoppingListRequest
        {
            Name = _session.ShoppingListName,
            ShoppingLocationId = newStore.Id
        };

        var result = await _apiClient.UpdateShoppingListAsync(_listId, request);
        if (result.Success)
        {
            StoreNameLabel.Text = newStore.Name;
            await LoadSessionAsync();
            if (_session != null)
            {
                StoreNameLabel.Text = _session.StoreName;
            }
        }
        else
        {
            await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to switch store", "OK");
        }
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
        if (_isScanning) return;
        _isScanning = true;

        try
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
        finally
        {
            _isScanning = false;
        }
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
                    if (!result.IsChildProduct && !result.NeedsChildSelection)
                    {
                        // Direct match — record the purchase, carrying any barcode-embedded
                        // weight/price so it is stored and flows through to inventory.
                        await ScanPurchaseItemAsync(cachedItem,
                            embeddedWeight: result.EmbeddedWeight, embeddedPrice: result.EmbeddedPrice);

                        // Surface the embedded price/weight from Type 2 barcodes.
                        if (result.EmbeddedPrice.HasValue)
                            await CommunityToolkit.Maui.Alerts.Toast.Make($"{result.ProductName} - ${result.EmbeddedPrice:F2}").Show();
                        else if (result.EmbeddedWeight.HasValue)
                            await CommunityToolkit.Maui.Alerts.Toast.Make($"{result.ProductName} - {result.EmbeddedWeight:F2} lbs").Show();
                    }
                    else if (result.IsChildProduct && result.ChildProductId.HasValue && !result.NeedsChildSelection)
                    {
                        // Scanned an unambiguous child of a list item — record the child
                        // purchase under the parent automatically (no selection page).
                        await CheckOffChildForItemAsync(cachedItem, result.ChildProductId.Value,
                            result.ChildProductName ?? result.ProductName ?? "Item");
                    }
                    else
                    {
                        // Ambiguous (parent has multiple children at store) — let the user pick.
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

            // Offline Type 2 barcode fallback: extract item number and match
            if (existingItem == null && WeightBarcodeParser.IsType2Barcode(barcode))
            {
                var parsed = WeightBarcodeParser.ParseType2Barcode(barcode);
                if (parsed != null)
                {
                    existingItem = _session.Items.FirstOrDefault(i =>
                        i.Barcodes.Any(b => b.Equals(parsed.ItemNumber, StringComparison.OrdinalIgnoreCase)));

                    // Try alternate position if no match
                    if (existingItem == null)
                    {
                        var parsedAlt = WeightBarcodeParser.ParseType2Barcode(barcode, 2);
                        if (parsedAlt != null && parsedAlt.ItemNumber != parsed.ItemNumber)
                        {
                            existingItem = _session.Items.FirstOrDefault(i =>
                                i.Barcodes.Any(b => b.Equals(parsedAlt.ItemNumber, StringComparison.OrdinalIgnoreCase)));
                        }
                    }
                }
            }

            if (existingItem != null)
            {
                // Offline match - increment purchased quantity (no popup)
                await ScanPurchaseItemAsync(existingItem);
                return;
            }

            // Offline: is the scanned barcode a child of a parent already on the list?
            var childMatch = FindChildInIndex(barcode);
            if (childMatch != null)
            {
                var parent = _session.Items.FirstOrDefault(i => i.Id == childMatch.Value.ParentItemId);
                if (parent != null)
                {
                    await CheckOffChildForItemAsync(parent, childMatch.Value.ChildProductId, childMatch.Value.ChildName);
                    return;
                }
            }
        }

        // Not matched to the list. Resolve a product/name (inventory then store) for the prompt.
        string? resolvedName = null;
        Guid? resolvedProductId = null;
        StoreProductResult? storeProduct = null;
        var tracksBestBefore = false;
        var defaultBestBeforeDays = 0;

        // Offline there is no product catalogue to consult — only the cached session, which
        // by definition does not contain this barcode. We cannot tell whether the product
        // tracks a best-before date, and the item is about to be added already checked off,
        // so the date would be lost for good. Ask rather than silently drop it; the prompt
        // has a Skip for the non-perishable case.
        var bestBeforeUnknown = !_connectivityService.IsOnline;

        if (_connectivityService.IsOnline)
        {
            var productResult = await _apiClient.GetProductByBarcodeAsync(barcode);
            if (productResult.Success && productResult.Data != null)
            {
                var product = productResult.Data;
                var existingItem = _session.Items.FirstOrDefault(i =>
                    (i.ProductId.HasValue && i.ProductId == product.Id) ||
                    i.ProductName.Equals(product.Name, StringComparison.OrdinalIgnoreCase));
                if (existingItem != null && !existingItem.IsPurchased)
                {
                    await ScanPurchaseItemAsync(existingItem);
                    return;
                }
                resolvedName = product.Name;
                resolvedProductId = product.Id;
                tracksBestBefore = product.TracksBestBeforeDate;
                defaultBestBeforeDays = product.DefaultBestBeforeDays;
            }

            if (resolvedName == null)
            {
                var storeResult = await _apiClient.LookupProductByBarcodeAsync(_listId, barcode);
                if (storeResult.Success && storeResult.Data != null)
                {
                    storeProduct = storeResult.Data;
                    var existingItem = _session.Items.FirstOrDefault(i =>
                        i.ProductName.Equals(storeProduct.Name, StringComparison.OrdinalIgnoreCase));
                    if (existingItem != null && !existingItem.IsPurchased)
                    {
                        await ScanPurchaseItemAsync(existingItem);
                        return;
                    }
                    resolvedName = storeProduct.Name;
                }
            }
        }

        // Not on the list → prompt to add it, or make it a child of an existing item.
        await PromptAddOrChildAsync(barcode, resolvedName, resolvedProductId, storeProduct,
            tracksBestBefore, defaultBestBeforeDays, bestBeforeUnknown);
    }

    /// <summary>
    /// Records a purchased child under a parent list item (local-first + online/queue),
    /// mirroring ScanPurchaseItemAsync. Used when a scanned barcode is a known child.
    /// </summary>
    private async Task CheckOffChildForItemAsync(CachedShoppingListItem parent, Guid childProductId, string childName)
    {
        if (_session == null) return;

        var effectiveAmount = parent.Amount > 0 ? parent.Amount : 1;

        // Once the parent's needed quantity is already met, confirm before adding another
        // child purchase rather than silently over-counting (auto until qty, then prompt).
        if (parent.IsPurchased || parent.ChildPurchasedQuantity >= effectiveAmount)
        {
            var addAnother = await DisplayAlertAsync(
                "Already checked off",
                $"\"{parent.ProductName}\" is already checked off. Add another purchase?",
                "Add another", "Cancel");
            if (!addAnother) return;
        }

        parent.ChildPurchasedQuantity += 1;
        if (parent.ChildPurchasedQuantity >= effectiveAmount && !parent.IsPurchased)
        {
            parent.IsPurchased = true;
            parent.PurchasedAt = DateTime.UtcNow;
        }
        await _offlineStorage.UpdateItemStateAsync(parent);

        var request = new CheckOffChildRequest { ChildProductId = childProductId, Quantity = 1 };
        if (_connectivityService.IsOnline)
        {
            var result = await _apiClient.CheckOffChildAsync(_listId, parent.Id, request);
            if (!result.Success)
                await EnqueueCheckOffChildOperationAsync(parent.Id, request);
        }
        else
        {
            await EnqueueCheckOffChildOperationAsync(parent.Id, request);
        }

        MoveItemBetweenGroups(parent);
        UpdateSubtotal();
        await CommunityToolkit.Maui.Alerts.Toast.Make($"{childName} ✓ under {parent.ProductName}").Show();
    }

    /// <summary>
    /// A scanned item that is not on the list: prompt to add it as a new item, or make it a
    /// child of an existing list item (permanent hierarchy link + barcode attach).
    /// </summary>
    private async Task PromptAddOrChildAsync(string barcode, string? resolvedName, Guid? resolvedProductId, StoreProductResult? store,
        bool tracksBestBefore = false, int defaultBestBeforeDays = 0, bool bestBeforeUnknown = false)
    {
        if (_session == null) return;

        var candidates = _session.Items.ToList();
        var title = resolvedName != null ? $"\"{resolvedName}\" is not on the list" : $"Barcode {barcode} is not on the list";

        var action = candidates.Count > 0
            ? await DisplayActionSheet(title, "Cancel", null, "Add as new item", "Add under an existing item…")
            : "Add as new item";

        if (string.IsNullOrEmpty(action) || action == "Cancel")
            return;

        if (action == "Add under an existing item…")
        {
            var names = candidates.Select(i => i.ProductName).ToArray();
            var pick = await DisplayActionSheet("Add under which item?", "Cancel", null, names);
            if (string.IsNullOrEmpty(pick) || pick == "Cancel")
                return;
            var parent = candidates.FirstOrDefault(i => i.ProductName == pick);
            if (parent == null) return;

            var childName = resolvedName;
            if (resolvedProductId == null && string.IsNullOrWhiteSpace(childName))
            {
                childName = await DisplayPromptAsync("Child name", $"Barcode: {barcode}\nName of this variant:",
                    "Add", "Cancel", placeholder: "Product name");
                if (string.IsNullOrWhiteSpace(childName)) return;
                childName = childName.Trim();
            }

            var request = new AddChildToParentRequest
            {
                ProductId = resolvedProductId,
                ProductName = resolvedProductId == null ? childName : null,
                Barcode = barcode,
                ExternalProductId = store?.ExternalProductId,
                Quantity = 1
            };

            if (_connectivityService.IsOnline)
            {
                var result = await _apiClient.AddChildToParentAsync(_listId, parent.Id, request);
                if (result.Success)
                {
                    await LoadSessionAsync();
                    await CommunityToolkit.Maui.Alerts.Toast.Make($"Added under {parent.ProductName}").Show();
                }
                else
                {
                    await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to add child", "OK");
                }
            }
            else
            {
                await EnqueueAddChildOperationAsync(parent.Id, request);
                await CommunityToolkit.Maui.Alerts.Toast.Make($"Added under {parent.ProductName} (will sync)").Show();
            }
            return;
        }

        // Add as a new item
        var name = resolvedName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = await DisplayPromptAsync("New Product", $"Barcode: {barcode}\nEnter the product name:",
                "Add", "Cancel", placeholder: "Product name");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
        }

        // The scanned item is added already checked off, so — like the manual check-off
        // path — capture an expiration date for products that track best-before dates,
        // and also when we could not determine that (offline), where skipping the prompt
        // would discard the date silently.
        DateTime? bestBeforeDate = null;
        if (tracksBestBefore || bestBeforeUnknown)
        {
            var (proceed, date) = await ShowBestBeforeDatePromptAsync(name, defaultBestBeforeDays);
            if (!proceed) return;
            bestBeforeDate = date;
        }

        if (_connectivityService.IsOnline)
        {
            var result = await _apiClient.QuickAddItemAsync(_listId, name, 1, barcode, null, isPurchased: true,
                aisle: store?.Aisle, department: store?.Department, externalProductId: store?.ExternalProductId,
                price: store?.Price, imageUrl: store?.ImageUrl, bestBeforeDate: bestBeforeDate);
            if (result.Success)
            {
                await LoadSessionAsync();
                await CommunityToolkit.Maui.Alerts.Toast.Make($"{name} added and checked off").Show();
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to add item", "OK");
            }
        }
        else
        {
            await EnqueueAddItemOperationAsync(name, barcode, bestBeforeDate);
            await CommunityToolkit.Maui.Alerts.Toast.Make($"{name} added (will sync)").Show();
        }
    }

    /// <summary>Finds a scanned barcode among the cached child index (offline child detection).</summary>
    private (Guid ParentItemId, Guid ChildProductId, string ChildName)? FindChildInIndex(string barcode)
    {
        foreach (var entry in _childIndex)
        {
            var child = entry.Children.FirstOrDefault(c =>
                c.Barcodes.Any(b => b.Equals(barcode, StringComparison.OrdinalIgnoreCase)));
            if (child != null)
                return (entry.ItemId, child.ProductId, child.ProductName);
        }
        return null;
    }

    private async Task EnqueueCheckOffChildOperationAsync(Guid itemId, CheckOffChildRequest request)
    {
        await _offlineStorage.EnqueueOperationAsync(new OfflineOperation
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OperationType = "CheckOffChild",
            PayloadJson = JsonSerializer.Serialize(new
            {
                ListId = _listId,
                ItemId = itemId,
                request.ChildProductId,
                request.Quantity,
                request.BestBeforeDate
            })
        });
    }

    private async Task EnqueueAddChildOperationAsync(Guid itemId, AddChildToParentRequest request)
    {
        await _offlineStorage.EnqueueOperationAsync(new OfflineOperation
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OperationType = "AddChildToParent",
            PayloadJson = JsonSerializer.Serialize(new
            {
                ListId = _listId,
                ItemId = itemId,
                request.ProductId,
                request.ProductName,
                request.ExternalProductId,
                request.Barcode,
                request.Quantity
            })
        });
    }

    private async Task EnqueueAddItemOperationAsync(string productName, string barcode, DateTime? bestBeforeDate = null)
    {
        await _offlineStorage.EnqueueOperationAsync(new OfflineOperation
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OperationType = "AddItem",
            PayloadJson = JsonSerializer.Serialize(new
            {
                ListId = _listId,
                ProductName = productName,
                Amount = 1m,
                Barcode = barcode,
                Note = (string?)null,
                IsPurchased = true,
                BestBeforeDate = bestBeforeDate
            })
        });
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
    private Task<(bool proceed, DateTime? date)> ShowBestBeforeDatePromptAsync(CachedShoppingListItem item)
        => ShowBestBeforeDatePromptAsync(item.ProductName, item.DefaultBestBeforeDays);

    private async Task<(bool proceed, DateTime? date)> ShowBestBeforeDatePromptAsync(string productName, int defaultBestBeforeDays)
    {
        var popup = new Popups.BestBeforeDatePopup(productName, defaultBestBeforeDays);
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
