using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

[QueryProperty(nameof(Barcode), "Barcode")]
[QueryProperty(nameof(ListId), "ListId")]
public partial class AddItemPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly OfflineStorageService _offlineStorage;
    private readonly ConnectivityService _connectivityService;
    private readonly ImageCacheService _imageCacheService;

    private string? _barcode;
    private Guid _listId;
    private int _quantity = 1;
    private StoreProductResult? _lookupResult;
    private Guid? _selectedProductId;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _autocompleteCts;
    private string? _currentSearchText;

    public ObservableCollection<ProductAutocompleteResult> AutocompleteResults { get; } = new();
    public ObservableCollection<StoreProductResult> SearchResults { get; } = new();

    public string? Barcode
    {
        get => _barcode;
        set
        {
            _barcode = value;
            if (!string.IsNullOrEmpty(value))
            {
                BarcodeLabel.Text = value;
                BarcodeFrame.IsVisible = true;
            }
        }
    }

    public string ListId
    {
        set => _listId = Guid.Parse(value);
    }

    public AddItemPage(
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
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Try to look up the barcode if online
        if (!string.IsNullOrEmpty(_barcode) && _connectivityService.IsOnline)
        {
            await LookupBarcodeAsync();
        }
    }

    private async Task LookupBarcodeAsync()
    {
        if (string.IsNullOrEmpty(_barcode)) return;

        SetLoading(true);

        try
        {
            var result = await _apiClient.LookupProductByBarcodeAsync(_listId, _barcode);

            if (result.Success && result.Data != null)
            {
                _lookupResult = result.Data;
                LookupProductName.Text = result.Data.ProductName;
                LookupPrice.Text = result.Data.Price.HasValue ? $"${result.Data.Price:F2}" : "";
                LookupResultFrame.IsVisible = true;

                // Pre-fill the product name
                ProductNameEntry.Text = result.Data.ProductName;
            }
        }
        catch
        {
            // Lookup is best-effort
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void OnProductNameTextChanged(object? sender, TextChangedEventArgs e)
    {
        _currentSearchText = e.NewTextValue?.Trim();

        // Clear selected product when text changes
        _selectedProductId = null;
        _lookupResult = null;
        LookupResultFrame.IsVisible = false;

        // Update action buttons text and visibility
        var hasText = !string.IsNullOrWhiteSpace(_currentSearchText) && _currentSearchText.Length >= 3;
        ActionButtonsPanel.IsVisible = hasText;
        if (hasText)
        {
            AddAsFreeTextButton.Text = $"Add \"{_currentSearchText}\" as new item";
        }

        // Debounced autocomplete
        _autocompleteCts?.Cancel();
        _autocompleteCts = new CancellationTokenSource();
        var ct = _autocompleteCts.Token;

        if (!hasText)
        {
            DismissSearchOverlay();
            return;
        }

        _ = DebounceAutocompleteAsync(_currentSearchText!, ct);
    }

    private async Task DebounceAutocompleteAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct);
            if (ct.IsCancellationRequested) return;
            await AutocompleteSearchAsync(query, ct);
        }
        catch (OperationCanceledException)
        {
            // Debounce cancelled, ignore
        }
    }

    private async Task AutocompleteSearchAsync(string query, CancellationToken ct)
    {
        if (!_connectivityService.IsOnline)
        {
            var isReachable = await _connectivityService.CheckServerReachableAsync();
            if (!isReachable) return;
        }

        try
        {
            var result = await _apiClient.AutocompleteProductsAsync(query);

            if (ct.IsCancellationRequested) return;

            AutocompleteResults.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var product in result.Data)
                {
                    AutocompleteResults.Add(product);
                }
            }

            SearchResultsView.ItemsSource = AutocompleteResults;
            ShowSearchOverlay();
        }
        catch (OperationCanceledException)
        {
            // Cancelled, ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Autocomplete exception: {ex.Message}");
        }
    }

    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        // Enter key still triggers the external store search for broader results
        _ = SearchProductsAsync();
    }

    private async Task SearchProductsAsync()
    {
        var query = ProductNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(query) || query.Length < 2)
        {
            DismissSearchOverlay();
            return;
        }

        // Check connectivity - if marked offline, try to verify server is reachable
        if (!_connectivityService.IsOnline)
        {
            var isReachable = await _connectivityService.CheckServerReachableAsync();
            if (!isReachable)
            {
                NoResultsLabel.Text = "Server not reachable - search unavailable";
                SearchResults.Clear();
                SearchResultsView.ItemsSource = SearchResults;
                ShowSearchOverlay();
                return;
            }
        }

        // Cancel any previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        SetLoading(true);

        try
        {
            var result = await _apiClient.SearchProductsAsync(_listId, query);

            SearchResults.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var product in result.Data.Take(10))
                {
                    SearchResults.Add(product);
                }
            }
            else if (!result.Success)
            {
                NoResultsLabel.Text = $"Search error: {result.ErrorMessage}";
            }

            SearchResultsView.ItemsSource = SearchResults;
            ShowSearchOverlay();
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled, ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search exception: {ex.Message}");
            NoResultsLabel.Text = $"Search failed: {ex.Message}";
            SearchResults.Clear();
            SearchResultsView.ItemsSource = SearchResults;
            ShowSearchOverlay();
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void OnSearchResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        var selection = e.CurrentSelection.FirstOrDefault();
        if (selection == null) return;

        // Clear selection
        SearchResultsView.SelectedItem = null;

        if (selection is ProductAutocompleteResult autocompleteResult)
        {
            // From autocomplete - store product ID directly
            _selectedProductId = autocompleteResult.Id;
            _lookupResult = null;
            ProductNameEntry.TextChanged -= OnProductNameTextChanged;
            ProductNameEntry.Text = autocompleteResult.Name;
            ProductNameEntry.TextChanged += OnProductNameTextChanged;

            LookupProductName.Text = autocompleteResult.Name;
            LookupPrice.Text = "";
            LookupResultFrame.IsVisible = true;
        }
        else if (selection is StoreProductResult storeResult)
        {
            // From external search
            _lookupResult = storeResult;
            _selectedProductId = null;
            ProductNameEntry.TextChanged -= OnProductNameTextChanged;
            ProductNameEntry.Text = storeResult.ProductName;
            ProductNameEntry.TextChanged += OnProductNameTextChanged;

            LookupProductName.Text = storeResult.ProductName;
            LookupPrice.Text = storeResult.Price.HasValue ? $"${storeResult.Price:F2}" : "";
            LookupResultFrame.IsVisible = true;
        }

        // Hide search overlay
        DismissSearchOverlay();
    }

    private async void OnAddAsFreeTextClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentSearchText)) return;

        SetLoading(true);

        try
        {
            var request = new CreateProductFromLookupMobileRequest { Name = _currentSearchText.Trim() };
            var result = await _apiClient.CreateProductFromLookupAsync(request);

            if (result.Success && result.Data != null)
            {
                _selectedProductId = result.Data.Id;
                _lookupResult = null;

                ProductNameEntry.TextChanged -= OnProductNameTextChanged;
                ProductNameEntry.Text = result.Data.Name;
                ProductNameEntry.TextChanged += OnProductNameTextChanged;

                LookupProductName.Text = result.Data.Name;
                LookupPrice.Text = "";
                LookupResultFrame.IsVisible = true;

                DismissSearchOverlay();
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to create product", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to create product: {ex.Message}", "OK");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnSearchExternalClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentSearchText)) return;

        // Cancel autocomplete
        _autocompleteCts?.Cancel();
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        SetLoading(true);

        try
        {
            var result = await _apiClient.SearchProductsAsync(_listId, _currentSearchText.Trim());

            SearchResults.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var product in result.Data.Take(10))
                {
                    SearchResults.Add(product);
                }
            }
            else if (!result.Success)
            {
                NoResultsLabel.Text = $"Search error: {result.ErrorMessage}";
            }

            SearchResultsView.ItemsSource = SearchResults;
            ShowSearchOverlay();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"External search exception: {ex.Message}");
            NoResultsLabel.Text = $"Search failed: {ex.Message}";
            SearchResults.Clear();
            SearchResultsView.ItemsSource = SearchResults;
            ShowSearchOverlay();
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void OnMinusClicked(object? sender, EventArgs e)
    {
        if (_quantity > 1)
        {
            _quantity--;
            QuantityLabel.Text = _quantity.ToString();
        }
    }

    private void OnPlusClicked(object? sender, EventArgs e)
    {
        if (_quantity < 99)
        {
            _quantity++;
            QuantityLabel.Text = _quantity.ToString();
        }
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var productName = ProductNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(productName))
        {
            await DisplayAlertAsync("Error", "Please enter a product name", "OK");
            return;
        }

        SetLoading(true);

        try
        {
            var isPurchased = MarkPurchasedCheckBox.IsChecked;

            // If we have a selected product ID (from autocomplete or free-text creation),
            // ensure the server knows about it. If not and online, auto-create via free-text.
            if (!_selectedProductId.HasValue && _lookupResult == null && _connectivityService.IsOnline)
            {
                var createResult = await _apiClient.CreateProductFromLookupAsync(
                    new CreateProductFromLookupMobileRequest { Name = productName });
                if (createResult.Success && createResult.Data != null)
                {
                    _selectedProductId = createResult.Data.Id;
                }
            }

            // Cache product image if available
            string? localImagePath = null;
            if (!string.IsNullOrEmpty(_lookupResult?.ImageUrl))
            {
                localImagePath = await _imageCacheService.CacheImageAsync(_lookupResult.ImageUrl);
            }

            // Create the new item for local cache
            var newItem = new CachedShoppingListItem
            {
                Id = Guid.NewGuid(),
                SessionId = _listId,
                ProductName = productName,
                Amount = _quantity,
                Note = NoteEntry.Text?.Trim(),
                Barcode = _barcode,
                IsPurchased = isPurchased,
                PurchasedAt = isPurchased ? DateTime.UtcNow : null,
                IsNewItem = true,
                Price = _lookupResult?.Price,
                Aisle = _lookupResult?.Aisle,
                Department = _lookupResult?.Department,
                ExternalProductId = _lookupResult?.ExternalProductId,
                ImageUrl = _lookupResult?.ImageUrl,
                LocalImagePath = localImagePath
            };

            // Try to add to server immediately if online
            if (_connectivityService.IsOnline)
            {
                var result = await _apiClient.QuickAddItemAsync(
                    _listId,
                    productName,
                    _quantity,
                    _barcode,
                    NoteEntry.Text?.Trim(),
                    isPurchased: isPurchased,
                    aisle: _lookupResult?.Aisle,
                    department: _lookupResult?.Department,
                    externalProductId: _lookupResult?.ExternalProductId,
                    price: _lookupResult?.Price,
                    imageUrl: _lookupResult?.ImageUrl);

                if (result.Success)
                {
                    // Server add succeeded - clear cache and reload to get server state
                    await _offlineStorage.ClearSessionAsync(_listId);
                    await Shell.Current.GoToAsync("..");
                    return;
                }
                else
                {
                    // Server add failed - show error but still add to local cache
                    System.Diagnostics.Debug.WriteLine($"Server add failed: {result.ErrorMessage}");
                }
            }

            // Offline or server failed - add to local cache and queue for later
            await _offlineStorage.AddItemToSessionAsync(_listId, newItem);
            await _offlineStorage.EnqueueOperationAsync(new OfflineOperation
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                OperationType = "AddItem",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ListId = _listId,
                    ProductName = productName,
                    Amount = _quantity,
                    Barcode = _barcode,
                    Note = NoteEntry.Text?.Trim(),
                    IsPurchased = isPurchased
                })
            });

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to add item: {ex.Message}", "OK");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void ShowSearchOverlay()
    {
        SearchOverlay.IsVisible = true;
        FormScrollView.IsVisible = false;
    }

    private void DismissSearchOverlay()
    {
        SearchOverlay.IsVisible = false;
        FormScrollView.IsVisible = true;
        ActionButtonsPanel.IsVisible = false;
    }

    private void OnDismissSearchClicked(object? sender, EventArgs e)
    {
        _autocompleteCts?.Cancel();
        DismissSearchOverlay();
    }

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        AddButton.IsEnabled = !loading;
    }
}
