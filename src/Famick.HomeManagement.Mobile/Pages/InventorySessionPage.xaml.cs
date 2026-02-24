using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class InventorySessionPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    private List<LocationDto> _locations = new();
    private LocationDto? _selectedLocation;
    private ProductDto? _currentProduct;
    private List<StockEntryDto> _stockEntries = new();
    private List<ProductLookupResultDto> _lookupResults = new();
    private List<IndividualStockItem> _pendingEntries = new();
    private string? _lastBarcode;

    private const string LastLocationIdKey = "inventory_session_last_location_id";

    public InventorySessionPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadLocationsAsync();
    }

    #region Location Management

    private async Task LoadLocationsAsync()
    {
        var result = await _apiClient.GetLocationsAsync();
        if (result.Success && result.Data != null)
        {
            _locations = result.Data;
            LocationPicker.ItemsSource = _locations.Select(l => l.Name).ToList();

            // Restore last used location
            var lastLocationId = Preferences.Get(LastLocationIdKey, string.Empty);
            if (!string.IsNullOrEmpty(lastLocationId) && Guid.TryParse(lastLocationId, out var savedId))
            {
                var index = _locations.FindIndex(l => l.Id == savedId);
                if (index >= 0)
                {
                    LocationPicker.SelectedIndex = index;
                    _selectedLocation = _locations[index];
                    return;
                }
            }

            // Default to first location
            if (_locations.Count > 0)
            {
                LocationPicker.SelectedIndex = 0;
                _selectedLocation = _locations[0];
            }
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (LocationPicker.SelectedIndex >= 0 && LocationPicker.SelectedIndex < _locations.Count)
        {
            _selectedLocation = _locations[LocationPicker.SelectedIndex];
            Preferences.Set(LastLocationIdKey, _selectedLocation.Id.ToString());
        }
    }

    #endregion

    #region Search & Scan

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        var scannerPage = new BarcodeScannerPage();
        await Navigation.PushAsync(scannerPage);
        var barcode = await scannerPage.ScanAsync();

        if (!string.IsNullOrEmpty(barcode))
        {
            _lastBarcode = barcode;
            SearchEntry.Text = barcode;
            await ProcessSearchAsync(barcode);
        }
    }

    private async void OnSearchSubmitted(object? sender, EventArgs e)
    {
        var query = SearchEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(query))
        {
            await ProcessSearchAsync(query);
        }
    }

    private async Task ProcessSearchAsync(string query)
    {
        if (_selectedLocation == null)
        {
            await DisplayAlert("Location Required", "Please select a location first.", "OK");
            return;
        }

        ShowState(PageState.Searching);

        try
        {
            // Try barcode lookup first if it looks like a barcode
            if (IsBarcode(query))
            {
                var barcodeResult = await _apiClient.GetProductByBarcodeAsync(query);
                if (barcodeResult.Success && barcodeResult.Data != null)
                {
                    _lastBarcode = query;
                    await ProcessProductFoundAsync(barcodeResult.Data);
                    return;
                }
            }
            else
            {
                // Name search
                var searchResult = await _apiClient.SearchProductsAsync(query);
                if (searchResult.Success && searchResult.Data != null && searchResult.Data.Count > 0)
                {
                    // Use best match
                    await ProcessProductFoundAsync(searchResult.Data[0]);
                    return;
                }
            }

            // Not found locally - try external lookup
            var lookupResult = await _apiClient.ProductLookupAsync(new ProductLookupRequest
            {
                Query = query,
                MaxResults = 10,
                IncludeStoreResults = true
            });

            if (lookupResult.Success && lookupResult.Data?.Results?.Count > 0)
            {
                _lookupResults = lookupResult.Data.Results;
                ExternalResultsCollection.ItemsSource = _lookupResults;
                ShowState(PageState.ExternalResults);
            }
            else
            {
                NotFoundDetailLabel.Text = $"No results found for \"{query}\".";
                ShowState(PageState.NotFound);
            }
        }
        catch (Exception ex)
        {
            NotFoundDetailLabel.Text = $"Search error: {ex.Message}";
            ShowState(PageState.NotFound);
        }
    }

    private async Task ProcessProductFoundAsync(ProductDto product)
    {
        _currentProduct = product;

        // Update product card
        ProductNameLabel.Text = product.Name;
        ProductLocationLabel.Text = $"Default location: {product.LocationName}";

        // Show product image if available
        var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary) ?? product.Images.FirstOrDefault();
        if (primaryImage != null)
        {
            ProductImage.Source = primaryImage.ThumbnailDisplayUrl;
            ProductImage.IsVisible = true;
            ProductImagePlaceholder.IsVisible = false;
        }
        else
        {
            ProductImage.IsVisible = false;
            ProductImagePlaceholder.IsVisible = true;
        }

        // Load stock entries at selected location
        var stockResult = await _apiClient.GetStockByProductAndLocationAsync(
            product.Id, _selectedLocation!.Id);

        if (stockResult.Success && stockResult.Data != null)
        {
            _stockEntries = stockResult.Data
                .OrderBy(s => s.BestBeforeDate ?? DateTime.MaxValue)
                .ToList();
        }
        else
        {
            _stockEntries = new();
        }

        // Show appropriate UI based on stock status
        if (_stockEntries.Count > 0)
        {
            StockCountLabel.Text = $"{_stockEntries.Count} stock {(_stockEntries.Count == 1 ? "entry" : "entries")}";
            StockEntriesCollection.ItemsSource = _stockEntries;
            StockEntriesSection.IsVisible = true;
            NoStockSection.IsVisible = false;
            AddStockButton.Text = "Add More Stock";
        }
        else
        {
            StockEntriesSection.IsVisible = false;
            NoStockSection.IsVisible = true;
            AddStockButton.Text = "Add Stock";
        }

        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        ShowState(PageState.ProductFound);
    }

    private async void OnSearchExternallyClicked(object? sender, EventArgs e)
    {
        var query = SearchEntry.Text?.Trim();
        if (string.IsNullOrEmpty(query)) return;

        try
        {
            SearchExternallyButton.IsEnabled = false;
            SearchExternallyButton.Text = "Searching externally...";

            var lookupResult = await _apiClient.ProductLookupAsync(new ProductLookupRequest
            {
                Query = query,
                MaxResults = 10,
                IncludeStoreResults = true
            });

            if (lookupResult.Success && lookupResult.Data?.Results?.Count > 0)
            {
                _lookupResults = lookupResult.Data.Results;
                ExternalResultsCollection.ItemsSource = _lookupResults;
                ShowState(PageState.ExternalResults);
            }
            else
            {
                await DisplayAlert("No Results", "No external results found.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"External search failed: {ex.Message}", "OK");
        }
        finally
        {
            SearchExternallyButton.IsEnabled = true;
            SearchExternallyButton.Text = "Search externally for more options";
        }
    }

    #endregion

    #region Add Stock

    private void OnAddStockClicked(object? sender, EventArgs e)
    {
        _pendingEntries.Clear();

        // Pre-populate best before date from product defaults
        if (_currentProduct != null && _currentProduct.DefaultBestBeforeDays > 0)
        {
            BestBeforeDatePicker.Date = DateTime.Today.AddDays(_currentProduct.DefaultBestBeforeDays);
        }
        else
        {
            BestBeforeDatePicker.Date = DateTime.Today.AddDays(30);
        }

        QuantityEntry.Text = "1";
        PriceEntry.Text = "";
        UpdatePendingEntriesDisplay();
        ShowState(PageState.AddStock);
    }

    private void OnAddAnotherEntryClicked(object? sender, EventArgs e)
    {
        if (!TryParseCurrentEntry(out var entry, out _)) return;

        var qty = GetCurrentQuantity();
        for (var i = 0; i < qty; i++)
        {
            _pendingEntries.Add(new IndividualStockItem
            {
                BestBeforeDate = entry.BestBeforeDate,
                Note = entry.Note
            });
        }
        UpdatePendingEntriesDisplay();

        // Reset form for next entry
        QuantityEntry.Text = "1";
        PriceEntry.Text = "";
        if (_currentProduct != null && _currentProduct.DefaultBestBeforeDays > 0)
        {
            BestBeforeDatePicker.Date = DateTime.Today.AddDays(_currentProduct.DefaultBestBeforeDays);
        }
    }

    private async void OnSaveStockClicked(object? sender, EventArgs e)
    {
        if (_currentProduct == null || _selectedLocation == null) return;

        // Add current form entry to pending list
        decimal? price = null;
        if (TryParseCurrentEntry(out var currentEntry, out price))
        {
            var qty = GetCurrentQuantity();
            for (var i = 0; i < qty; i++)
            {
                _pendingEntries.Add(new IndividualStockItem
                {
                    BestBeforeDate = currentEntry.BestBeforeDate,
                    Note = currentEntry.Note
                });
            }
        }

        if (_pendingEntries.Count == 0)
        {
            await DisplayAlert("No Entries", "Please add at least one stock entry.", "OK");
            return;
        }

        try
        {
            var request = new AddStockBatchRequest
            {
                ProductId = _currentProduct.Id,
                LocationId = _selectedLocation.Id,
                PurchasedDate = DateTime.Today,
                Price = price,
                IndividualItems = _pendingEntries
            };

            var result = await _apiClient.AddStockBatchAsync(request);
            if (result.Success)
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                var total = _pendingEntries.Count;
                SuccessDetailLabel.Text = $"Added {total} {_currentProduct.QuantityUnitStockName} of {_currentProduct.Name}";
                _pendingEntries.Clear();
                ShowState(PageState.Success);
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to add stock", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }

    private void OnCancelAddStockClicked(object? sender, EventArgs e)
    {
        _pendingEntries.Clear();
        ShowState(PageState.ProductFound);
    }

    private bool TryParseCurrentEntry(out IndividualStockItem entry, out decimal? price)
    {
        entry = new IndividualStockItem();
        price = null;

        if (!decimal.TryParse(QuantityEntry.Text, out var qty) || qty <= 0)
        {
            DisplayAlert("Invalid Quantity", "Please enter a valid quantity.", "OK");
            return false;
        }

        // IndividualItems creates one stock entry per item (Amount=1 each).
        // For qty > 1, add multiple entries in the caller.
        entry.BestBeforeDate = BestBeforeDatePicker.Date;

        if (!string.IsNullOrEmpty(PriceEntry.Text) && decimal.TryParse(PriceEntry.Text, out var parsedPrice))
        {
            price = parsedPrice;
        }

        return true;
    }

    private int GetCurrentQuantity()
    {
        if (decimal.TryParse(QuantityEntry.Text, out var qty) && qty > 0)
            return (int)Math.Ceiling(qty);
        return 1;
    }

    private void UpdatePendingEntriesDisplay()
    {
        if (_pendingEntries.Count > 0)
        {
            // Group by best before date for display
            var grouped = _pendingEntries
                .GroupBy(e => e.BestBeforeDate?.ToString("yyyy-MM-dd") ?? "No date")
                .Select(g => new
                {
                    Amount = (decimal)g.Count(),
                    BestBeforeDateDisplay = g.Key
                })
                .ToList();
            PendingEntriesCollection.ItemsSource = null;
            PendingEntriesCollection.ItemsSource = grouped;
            PendingEntriesCollection.HeightRequest = grouped.Count * 44;
        }
        else
        {
            PendingEntriesCollection.HeightRequest = 0;
        }
    }

    #endregion

    #region External Results & Product Creation

    private async void OnExternalResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProductLookupResultDto selected) return;

        // Reset selection
        ExternalResultsCollection.SelectedItem = null;

        try
        {
            // Create product from lookup result
            var createRequest = new CreateProductRequest
            {
                Name = selected.Name,
                Description = selected.Description,
                LocationId = _selectedLocation?.Id,
                Barcodes = !string.IsNullOrEmpty(selected.Barcode) ? new List<string> { selected.Barcode } : new(),
                ImageUrl = selected.ImageUrl,
                TracksBestBeforeDate = true
            };

            var createResult = await _apiClient.CreateProductAsync(createRequest);
            if (createResult.Success && createResult.Data != null)
            {
                // Create a todo item for reviewing this product later
                await _apiClient.CreateTodoItemAsync(new CreateTodoItemRequest
                {
                    TaskType = "ReviewProduct",
                    Reason = "Product created during inventory session - review details",
                    RelatedEntityId = createResult.Data.Id,
                    RelatedEntityType = "Product",
                    Description = $"Review and complete details for \"{createResult.Data.Name}\""
                });

                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                await ProcessProductFoundAsync(createResult.Data);
            }
            else
            {
                await DisplayAlert("Error", createResult.ErrorMessage ?? "Failed to create product", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create product: {ex.Message}", "OK");
        }
    }

    private async void OnCreateProductManuallyClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Create Product", "Enter product name:",
            placeholder: "Product name", maxLength: 200);

        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var createRequest = new CreateProductRequest
            {
                Name = name.Trim(),
                LocationId = _selectedLocation?.Id,
                Barcodes = !string.IsNullOrEmpty(_lastBarcode) ? new List<string> { _lastBarcode } : new(),
                TracksBestBeforeDate = true
            };

            var result = await _apiClient.CreateProductAsync(createRequest);
            if (result.Success && result.Data != null)
            {
                // Create review todo
                await _apiClient.CreateTodoItemAsync(new CreateTodoItemRequest
                {
                    TaskType = "ReviewProduct",
                    Reason = "Product manually created during inventory session - review details",
                    RelatedEntityId = result.Data.Id,
                    RelatedEntityType = "Product",
                    Description = $"Review and complete details for \"{result.Data.Name}\""
                });

                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                await ProcessProductFoundAsync(result.Data);
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create product", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create product: {ex.Message}", "OK");
        }
    }

    #endregion

    #region Navigation

    private void OnNextItemClicked(object? sender, EventArgs e)
    {
        ResetState();
        SearchEntry.Text = "";
        SearchEntry.Focus();
        HapticFeedback.Default.Perform(HapticFeedbackType.Click);
    }

    private void ResetState()
    {
        _currentProduct = null;
        _stockEntries.Clear();
        _lookupResults.Clear();
        _pendingEntries.Clear();
        _lastBarcode = null;

        ShowState(PageState.Idle);
    }

    #endregion

    #region State Management

    private enum PageState
    {
        Idle,
        Searching,
        ProductFound,
        AddStock,
        ExternalResults,
        NotFound,
        Success
    }

    private void ShowState(PageState state)
    {
        SearchingState.IsVisible = state == PageState.Searching;
        ProductFoundState.IsVisible = state == PageState.ProductFound;
        AddStockState.IsVisible = state == PageState.AddStock;
        ExternalResultsState.IsVisible = state == PageState.ExternalResults;
        NotFoundState.IsVisible = state == PageState.NotFound;
        SuccessState.IsVisible = state == PageState.Success;
    }

    private static bool IsBarcode(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var cleaned = input.Trim().Replace("-", "").Replace(" ", "");
        return System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^[0-9]{8,14}$");
    }

    #endregion
}
