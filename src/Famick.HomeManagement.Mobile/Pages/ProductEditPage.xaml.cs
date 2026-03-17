using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

[QueryProperty(nameof(ProductId), "ProductId")]
public partial class ProductEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ProductDto? _product;
    private bool _loaded;
    private bool _isEditMode;

    private List<LocationDto> _locations = new();
    private List<ProductGroupSummary> _productGroups = new();
    private List<QuantityUnitSummary> _quantityUnits = new();
    private Guid? _selectedParentProductId;
    private string? _pendingParentName;
    private CancellationTokenSource? _parentSearchDebounce;
    private bool _suppressParentSearch;

    private CancellationTokenSource? _lookupSearchDebounce;
    private ProductLookupResultDto? _selectedLookupResult;

    // Barcode management (create mode collects locally, edit mode calls API immediately)
    private readonly List<string> _pendingBarcodes = new();

    // Image management (create mode collects locally, edit mode calls API immediately)
    private readonly List<FileResult> _pendingImages = new();

    public string ProductId { get; set; } = string.Empty;

    public ProductEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded) return;
        _loaded = true;

        ShowFormLoading(true);

        // Load picker data in parallel
        var locationsTask = _apiClient.GetLocationsAsync();
        var groupsTask = _apiClient.GetProductGroupsAsync();
        var unitsTask = _apiClient.GetQuantityUnitsAsync();

        await Task.WhenAll(locationsTask, groupsTask, unitsTask);

        if (locationsTask.Result.Success && locationsTask.Result.Data != null)
            _locations = locationsTask.Result.Data;

        if (groupsTask.Result.Success && groupsTask.Result.Data != null)
            _productGroups = groupsTask.Result.Data;

        if (unitsTask.Result.Success && unitsTask.Result.Data != null)
            _quantityUnits = unitsTask.Result.Data;

        PopulatePickers();

        _isEditMode = Guid.TryParse(ProductId, out _);

        if (_isEditMode)
        {
            TitleLabel.Text = "Edit Product";
            LookupSection.IsVisible = false;
            await LoadProductAsync();
        }
        else
        {
            TitleLabel.Text = "New Product";
            LookupSection.IsVisible = true;
            IsActiveSwitch.IsToggled = true;
            TracksBestBeforeSwitch.IsToggled = true;
            FactorEntry.Text = "1";
        }

        ShowFormLoading(false);
    }

    private void PopulatePickers()
    {
        LocationPicker.ItemsSource = _locations.Select(l => l.Name).ToList();

        var groupNames = new List<string> { "(None)" };
        groupNames.AddRange(_productGroups.Select(g => g.Name));
        ProductGroupPicker.ItemsSource = groupNames;
        ProductGroupPicker.SelectedIndex = 0;

        PurchaseUnitPicker.ItemsSource = _quantityUnits.Select(u => u.Name).ToList();
        StockUnitPicker.ItemsSource = _quantityUnits.Select(u => u.Name).ToList();
    }

    private async Task LoadProductAsync()
    {
        if (!Guid.TryParse(ProductId, out var id)) return;

        try
        {
            var result = await _apiClient.GetProductByIdAsync(id);
            if (result.Success && result.Data != null)
            {
                _product = result.Data;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PopulateForm();
                    RenderBarcodes();
                    RenderImages();
                });
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to load product", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load product: {ex.Message}", "OK");
        }
    }

    private void PopulateForm()
    {
        if (_product == null) return;

        NameEntry.Text = _product.Name;
        DescriptionEditor.Text = _product.Description;
        IsActiveSwitch.IsToggled = _product.IsActive;

        // Location
        var locIdx = _locations.FindIndex(l => l.Id == _product.LocationId);
        if (locIdx >= 0) LocationPicker.SelectedIndex = locIdx;

        // Product group
        if (_product.ProductGroupId.HasValue)
        {
            var groupIdx = _productGroups.FindIndex(g => g.Id == _product.ProductGroupId.Value);
            if (groupIdx >= 0) ProductGroupPicker.SelectedIndex = groupIdx + 1; // +1 for "(None)"
        }
        else
        {
            ProductGroupPicker.SelectedIndex = 0;
        }

        // Units
        var purchaseIdx = _quantityUnits.FindIndex(u => u.Id == _product.QuantityUnitIdPurchase);
        if (purchaseIdx >= 0) PurchaseUnitPicker.SelectedIndex = purchaseIdx;

        var stockIdx = _quantityUnits.FindIndex(u => u.Id == _product.QuantityUnitIdStock);
        if (stockIdx >= 0) StockUnitPicker.SelectedIndex = stockIdx;

        FactorEntry.Text = _product.QuantityUnitFactorPurchaseToStock.ToString("G");
        MinStockEntry.Text = _product.MinStockAmount.ToString("G");
        TracksBestBeforeSwitch.IsToggled = _product.TracksBestBeforeDate;
        BestBeforeDaysSection.IsVisible = _product.TracksBestBeforeDate;
        ExpiryWarningSection.IsVisible = _product.TracksBestBeforeDate;
        BestBeforeDaysEntry.Text = _product.DefaultBestBeforeDays.ToString();

        if (_product.ExpiryWarningDays.HasValue)
            ExpiryWarningDaysEntry.Text = _product.ExpiryWarningDays.Value.ToString();

        // Parent product
        if (_product.ParentProductId.HasValue && !string.IsNullOrEmpty(_product.ParentProductName))
        {
            _selectedParentProductId = _product.ParentProductId;
            ShowSelectedParent(_product.ParentProductName);
        }
    }

    private void OnTracksBestBeforeToggled(object? sender, ToggledEventArgs e)
    {
        BestBeforeDaysSection.IsVisible = e.Value;
        ExpiryWarningSection.IsVisible = e.Value;
    }

    #region Parent Product Search

    private void OnParentSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressParentSearch) return;

        _parentSearchDebounce?.Cancel();
        _parentSearchDebounce = new CancellationTokenSource();
        var token = _parentSearchDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                var query = e.NewTextValue?.Trim();
                if (string.IsNullOrEmpty(query) || query.Length < 2)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ParentSearchResults.IsVisible = false;
                    });
                    return;
                }

                var result = await _apiClient.SearchParentProductsAsync(query);
                if (token.IsCancellationRequested) return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (result.Success && result.Data != null && result.Data.Count > 0)
                    {
                        // Exclude the current product from results
                        var filtered = _product != null
                            ? result.Data.Where(p => p.Id != _product.Id).ToList()
                            : result.Data;

                        ParentSearchResults.ItemsSource = filtered;
                        ParentSearchResults.IsVisible = filtered.Count > 0;
                        CreateParentButton.IsVisible = false;
                    }
                    else
                    {
                        ParentSearchResults.IsVisible = false;
                        CreateParentButton.Text = $"Create \"{query}\" as parent product";
                        CreateParentButton.IsVisible = true;
                    }
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private async void OnParentProductSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ParentProductSearchResultDto selected) return;

        ParentSearchResults.SelectedItem = null;
        ParentSearchResults.IsVisible = false;
        _suppressParentSearch = true;
        ParentProductSearch.Text = string.Empty;
        _suppressParentSearch = false;

        if (selected.Source == "master" && selected.MasterProductId.HasValue)
        {
            // Auto-add master catalog product as tenant product
            var result = await _apiClient.EnsureProductFromMasterAsync(selected.MasterProductId.Value);
            if (result.Success && result.Data != null)
            {
                _selectedParentProductId = result.Data.Id;
                ShowSelectedParent(result.Data.Name, false);
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to add product from catalog", "OK");
            }
        }
        else
        {
            _selectedParentProductId = selected.Id;
            ShowSelectedParent(selected.Name, false);
        }
    }

    private void OnClearParentClicked(object? sender, EventArgs e)
    {
        _selectedParentProductId = null;
        _pendingParentName = null;
        SelectedParentBorder.IsVisible = false;
        PendingParentBorder.IsVisible = false;
        ClearParentButton.IsVisible = false;
        _suppressParentSearch = true;
        ParentProductSearch.Text = string.Empty;
        _suppressParentSearch = false;
    }

    private void OnCreateParentClicked(object? sender, EventArgs e)
    {
        var searchText = ParentProductSearch.Text?.Trim();
        if (string.IsNullOrEmpty(searchText)) return;

        _pendingParentName = searchText;
        _selectedParentProductId = null;

        PendingParentLabel.Text = $"Will create: \"{searchText}\"";
        PendingParentBorder.IsVisible = true;
        ClearParentButton.IsVisible = true;
        ParentSearchResults.IsVisible = false;
        CreateParentButton.IsVisible = false;
        SelectedParentBorder.IsVisible = false;
        _suppressParentSearch = true;
        ParentProductSearch.Text = string.Empty;
        _suppressParentSearch = false;
    }

    private void ShowSelectedParent(string name, bool fromCatalog = false)
    {
        _pendingParentName = null;
        PendingParentBorder.IsVisible = false;
        SelectedParentLabel.Text = fromCatalog ? $"{name} (from catalog)" : name;
        SelectedParentBorder.IsVisible = true;
        ClearParentButton.IsVisible = true;
    }

    /// <summary>
    /// Creates the pending parent product using the current form's attributes.
    /// Returns the new parent product's ID, or null on failure.
    /// </summary>
    private async Task<Guid?> CreatePendingParentProductAsync()
    {
        if (string.IsNullOrEmpty(_pendingParentName)) return null;

        var request = new CreateProductRequest
        {
            Name = _pendingParentName,
            LocationId = LocationPicker.SelectedIndex >= 0
                ? _locations[LocationPicker.SelectedIndex].Id
                : null,
            QuantityUnitIdPurchase = PurchaseUnitPicker.SelectedIndex >= 0
                ? _quantityUnits[PurchaseUnitPicker.SelectedIndex].Id
                : null,
            QuantityUnitIdStock = StockUnitPicker.SelectedIndex >= 0
                ? _quantityUnits[StockUnitPicker.SelectedIndex].Id
                : null,
            QuantityUnitFactorPurchaseToStock = decimal.TryParse(FactorEntry.Text, out var f) && f > 0 ? f : 1,
            MinStockAmount = decimal.TryParse(MinStockEntry.Text, out var ms) ? ms : 0,
            DefaultBestBeforeDays = TracksBestBeforeSwitch.IsToggled && int.TryParse(BestBeforeDaysEntry.Text, out var bbd) ? bbd : 0,
            TracksBestBeforeDate = TracksBestBeforeSwitch.IsToggled,
            IsActive = true
        };

        var result = await _apiClient.CreateProductAsync(request);
        if (result.Success && result.Data != null)
        {
            return result.Data.Id;
        }

        await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create parent product", "OK");
        return null;
    }

    #endregion

    #region Lookup Search

    private void OnLookupSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _lookupSearchDebounce?.Cancel();
        _lookupSearchDebounce = new CancellationTokenSource();
        var token = _lookupSearchDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                var query = e.NewTextValue?.Trim();
                if (string.IsNullOrEmpty(query) || query.Length < 2)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        LookupResultsCollection.IsVisible = false;
                        LookupLoadingIndicator.IsVisible = false;
                        LookupLoadingIndicator.IsRunning = false;
                    });
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    LookupLoadingIndicator.IsVisible = true;
                    LookupLoadingIndicator.IsRunning = true;
                });

                var request = new ProductLookupRequest
                {
                    Query = query,
                    MaxResults = 15,
                    IncludeStoreResults = true,
                    SearchMode = 0 // AllSources
                };

                var result = await _apiClient.ProductLookupAsync(request);
                if (token.IsCancellationRequested) return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    LookupLoadingIndicator.IsVisible = false;
                    LookupLoadingIndicator.IsRunning = false;

                    if (result.Success && result.Data?.Results != null)
                    {
                        var displayItems = result.Data.Results
                            .Select(r => new LookupResultDisplayModel(r, _apiClient.BaseUrl))
                            .ToList();
                        LookupResultsCollection.ItemsSource = displayItems;
                        LookupResultsCollection.IsVisible = true;
                    }
                    else
                    {
                        LookupResultsCollection.ItemsSource = new List<LookupResultDisplayModel>();
                        LookupResultsCollection.IsVisible = true;
                    }
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void OnLookupResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not LookupResultDisplayModel selected) return;

        LookupResultsCollection.SelectedItem = null;
        _selectedLookupResult = selected.Dto;

        // Pre-fill form fields
        NameEntry.Text = selected.Dto.Name;
        if (!string.IsNullOrEmpty(selected.Dto.Brand))
        {
            var desc = DescriptionEditor.Text?.Trim();
            if (string.IsNullOrEmpty(desc))
                DescriptionEditor.Text = selected.Dto.Brand;
        }

        // Show selected result indicator
        SelectedLookupLabel.Text = $"Selected: {selected.Dto.Name} ({selected.Dto.PluginDisplayName})";
        SelectedLookupBorder.IsVisible = true;
        LookupResultsCollection.IsVisible = false;
        LookupSearchEntry.Text = string.Empty;
    }

    private void OnClearLookupResultClicked(object? sender, EventArgs e)
    {
        _selectedLookupResult = null;
        SelectedLookupBorder.IsVisible = false;
    }

    #endregion

    #region Barcode Management

    private async void OnAddBarcodeClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Add Barcode", "Cancel", null, "Scan Barcode", "Enter Manually");
        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        string? barcode = null;

        if (action == "Scan Barcode")
        {
            var scannerPage = new BarcodeScannerPage();
            await Navigation.PushAsync(scannerPage);
            barcode = await scannerPage.ScanAsync();
        }
        else
        {
            barcode = await DisplayPromptAsync("Add Barcode", "Enter barcode value:", placeholder: "Barcode...");
        }

        barcode = barcode?.Trim();
        if (string.IsNullOrEmpty(barcode)) return;

        if (_isEditMode && _product != null)
        {
            // Edit mode: save immediately via API
            AddBarcodeButton.IsEnabled = false;
            var result = await _apiClient.AddProductBarcodeAsync(_product.Id, barcode);
            AddBarcodeButton.IsEnabled = true;

            if (result.Success && result.Data != null)
            {
                _product.Barcodes.Add(result.Data);
                RenderBarcodes();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to add barcode", "OK");
            }
        }
        else
        {
            // Create mode: collect locally
            if (!_pendingBarcodes.Contains(barcode))
            {
                _pendingBarcodes.Add(barcode);
                RenderPendingBarcodes();
            }
            else
            {
                await DisplayAlert("Duplicate", "This barcode has already been added.", "OK");
            }
        }
    }

    private void RenderBarcodes()
    {
        BarcodesListSection.Children.Clear();
        if (_product == null) return;

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        foreach (var barcode in _product.Barcodes)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Padding = new Thickness(8, 4)
            };

            var label = new Label
            {
                Text = string.IsNullOrEmpty(barcode.Note)
                    ? barcode.Barcode
                    : $"{barcode.Barcode} ({barcode.Note})",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center,
                TextColor = isDark ? Colors.White : Colors.Black
            };

            var deleteBtn = new Button
            {
                Text = "Remove",
                FontSize = 12,
                HeightRequest = 30,
                Padding = new Thickness(8, 0),
                CornerRadius = 6,
                BackgroundColor = isDark ? Color.FromArgb("#424242") : Color.FromArgb("#FFEBEE"),
                TextColor = isDark ? Color.FromArgb("#EF9A9A") : Color.FromArgb("#C62828")
            };

            var barcodeId = barcode.Id;
            deleteBtn.Clicked += async (_, _) => await DeleteBarcodeAsync(barcodeId);

            row.Add(label, 0);
            row.Add(deleteBtn, 1);
            BarcodesListSection.Children.Add(row);
        }
    }

    private void RenderPendingBarcodes()
    {
        BarcodesListSection.Children.Clear();
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        for (var i = 0; i < _pendingBarcodes.Count; i++)
        {
            var barcodeValue = _pendingBarcodes[i];
            var index = i;

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Padding = new Thickness(8, 4)
            };

            var label = new Label
            {
                Text = barcodeValue,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center,
                TextColor = isDark ? Colors.White : Colors.Black
            };

            var deleteBtn = new Button
            {
                Text = "Remove",
                FontSize = 12,
                HeightRequest = 30,
                Padding = new Thickness(8, 0),
                CornerRadius = 6,
                BackgroundColor = isDark ? Color.FromArgb("#424242") : Color.FromArgb("#FFEBEE"),
                TextColor = isDark ? Color.FromArgb("#EF9A9A") : Color.FromArgb("#C62828")
            };

            deleteBtn.Clicked += (_, _) =>
            {
                _pendingBarcodes.Remove(barcodeValue);
                RenderPendingBarcodes();
            };

            row.Add(label, 0);
            row.Add(deleteBtn, 1);
            BarcodesListSection.Children.Add(row);
        }
    }

    private async Task DeleteBarcodeAsync(Guid barcodeId)
    {
        if (_product == null) return;

        var confirm = await DisplayAlert("Delete Barcode", "Remove this barcode?", "Delete", "Cancel");
        if (!confirm) return;

        var result = await _apiClient.DeleteProductBarcodeAsync(_product.Id, barcodeId);
        if (result.Success)
        {
            _product.Barcodes.RemoveAll(b => b.Id == barcodeId);
            RenderBarcodes();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete barcode", "OK");
        }
    }

    #endregion

    #region Image Management

    private async void OnAddImageClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Add Photo", "Cancel", null, "Take Photo", "Choose from Library");
        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        try
        {
            FileResult? photo = null;
            if (action == "Take Photo")
            {
                if (MediaPicker.Default.IsCaptureSupported)
                    photo = await MediaPicker.Default.CapturePhotoAsync();
                else
                    await DisplayAlert("Error", "Camera not supported on this device.", "OK");
            }
            else
            {
                photo = await MediaPicker.Default.PickPhotoAsync();
            }

            if (photo == null) return;

            if (_isEditMode && _product != null)
            {
                // Edit mode: upload immediately
                AddImageButton.IsEnabled = false;
                using var stream = await photo.OpenReadAsync();
                var result = await _apiClient.UploadProductImageAsync(_product.Id, stream, photo.FileName);
                AddImageButton.IsEnabled = true;

                if (result.Success && result.Data != null)
                {
                    _product.Images.AddRange(result.Data);
                    RenderImages();
                }
                else
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to upload photo", "OK");
                }
            }
            else
            {
                // Create mode: collect locally
                _pendingImages.Add(photo);
                RenderPendingImages();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add photo: {ex.Message}", "OK");
        }
    }

    private void RenderImages()
    {
        ImagesGallery.Children.Clear();
        if (_product == null) return;

        foreach (var image in _product.Images)
        {
            var imageUrl = image.ThumbnailDisplayUrl;
            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                imageUrl = $"{_apiClient.BaseUrl}{(imageUrl.StartsWith('/') ? "" : "/")}{imageUrl}";

            var container = new Grid
            {
                WidthRequest = 100,
                HeightRequest = 110
            };

            var border = new Border
            {
                HeightRequest = 100,
                WidthRequest = 100,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = image.IsPrimary
                    ? Color.FromArgb("#1976D2")
                    : Colors.Transparent,
                StrokeThickness = image.IsPrimary ? 2 : 0,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A")
                    : Color.FromArgb("#F0F0F0")
            };

            var img = new Image
            {
                HeightRequest = 100,
                WidthRequest = 100,
                Aspect = Aspect.AspectFill
            };

            if (!string.IsNullOrEmpty(imageUrl) && Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                img.Source = ImageSource.FromUri(uri);

            border.Content = img;
            container.Children.Add(border);

            if (image.IsPrimary)
            {
                var primaryBadge = new Border
                {
                    Padding = new Thickness(4, 1),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                    Stroke = Colors.Transparent,
                    BackgroundColor = Color.FromArgb("#1976D2"),
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.End,
                    Margin = new Thickness(4, 0, 0, 4),
                    Content = new Label
                    {
                        Text = "Primary",
                        FontSize = 9,
                        TextColor = Colors.White
                    }
                };
                container.Children.Add(primaryBadge);
            }

            var imageId = image.Id;
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (_, _) => await OnImageTappedAsync(imageId, image.IsPrimary);
            container.GestureRecognizers.Add(tapGesture);

            ImagesGallery.Children.Add(container);
        }

        ImagesScrollView.IsVisible = _product.Images.Count > 0;
    }

    private async Task OnImageTappedAsync(Guid imageId, bool isPrimary)
    {
        if (_product == null) return;

        var actions = isPrimary
            ? new[] { "Delete" }
            : new[] { "Set as Primary", "Delete" };

        var action = await DisplayActionSheet("Image Options", "Cancel", null, actions);
        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        if (action == "Set as Primary")
        {
            var result = await _apiClient.SetProductPrimaryImageAsync(_product.Id, imageId);
            if (result.Success)
            {
                foreach (var img in _product.Images)
                    img.IsPrimary = img.Id == imageId;
                RenderImages();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to set primary image", "OK");
            }
        }
        else if (action == "Delete")
        {
            var confirm = await DisplayAlert("Delete Image", "Remove this image?", "Delete", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.DeleteProductImageAsync(_product.Id, imageId);
            if (result.Success)
            {
                _product.Images.RemoveAll(i => i.Id == imageId);
                RenderImages();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete image", "OK");
            }
        }
    }

    private void RenderPendingImages()
    {
        ImagesGallery.Children.Clear();

        for (var i = 0; i < _pendingImages.Count; i++)
        {
            var fileResult = _pendingImages[i];
            var index = i;

            var border = new Border
            {
                HeightRequest = 100,
                WidthRequest = 100,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A")
                    : Color.FromArgb("#F0F0F0")
            };

            var img = new Image
            {
                HeightRequest = 100,
                WidthRequest = 100,
                Aspect = Aspect.AspectFill,
                Source = ImageSource.FromFile(fileResult.FullPath)
            };

            border.Content = img;

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (_, _) =>
            {
                _pendingImages.RemoveAt(index);
                RenderPendingImages();
            };
            border.GestureRecognizers.Add(tapGesture);

            ImagesGallery.Children.Add(border);
        }

        ImagesScrollView.IsVisible = _pendingImages.Count > 0;
    }

    #endregion

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlert("Validation", "Product name is required.", "OK");
            return;
        }

        if (LocationPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Validation", "Please select a location.", "OK");
            return;
        }

        if (PurchaseUnitPicker.SelectedIndex < 0 || StockUnitPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Validation", "Please select purchase and stock units.", "OK");
            return;
        }

        if (!decimal.TryParse(FactorEntry.Text, out var factor) || factor <= 0)
            factor = 1;

        decimal.TryParse(MinStockEntry.Text, out var minStock);

        int bestBeforeDays = 0;
        if (TracksBestBeforeSwitch.IsToggled && !string.IsNullOrEmpty(BestBeforeDaysEntry.Text))
            int.TryParse(BestBeforeDaysEntry.Text, out bestBeforeDays);

        int? expiryWarningDays = null;
        if (TracksBestBeforeSwitch.IsToggled && !string.IsNullOrEmpty(ExpiryWarningDaysEntry.Text))
        {
            if (int.TryParse(ExpiryWarningDaysEntry.Text, out var ewDays) && ewDays > 0)
                expiryWarningDays = ewDays;
        }

        Guid? productGroupId = null;
        if (ProductGroupPicker.SelectedIndex > 0) // 0 = "(None)"
            productGroupId = _productGroups[ProductGroupPicker.SelectedIndex - 1].Id;

        SaveToolbarItem.IsEnabled = false;

        try
        {
            // Create pending parent product first if needed
            var parentId = _selectedParentProductId;
            if (_pendingParentName != null)
            {
                parentId = await CreatePendingParentProductAsync();
                if (parentId == null)
                {
                    SaveToolbarItem.IsEnabled = true;
                    return;
                }
            }

            if (_isEditMode && _product != null)
            {
                // Edit existing product
                var request = new UpdateProductMobileRequest
                {
                    Name = name,
                    Description = DescriptionEditor.Text?.Trim(),
                    LocationId = _locations[LocationPicker.SelectedIndex].Id,
                    QuantityUnitIdPurchase = _quantityUnits[PurchaseUnitPicker.SelectedIndex].Id,
                    QuantityUnitIdStock = _quantityUnits[StockUnitPicker.SelectedIndex].Id,
                    QuantityUnitFactorPurchaseToStock = factor,
                    MinStockAmount = minStock,
                    DefaultBestBeforeDays = bestBeforeDays,
                    TracksBestBeforeDate = TracksBestBeforeSwitch.IsToggled,
                    IsActive = IsActiveSwitch.IsToggled,
                    ExpiryWarningDays = expiryWarningDays,
                    ProductGroupId = productGroupId,
                    ParentProductId = parentId
                };

                var result = await _apiClient.UpdateProductAsync(_product.Id, request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                if (result.ErrorMessage?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await DisplayAlert("Duplicate Product",
                        $"A product named \"{name}\" already exists. Please use a different name.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update product", "OK");
                }
            }
            else
            {
                // Create new product
                var createRequest = new CreateProductRequest
                {
                    Name = name,
                    Description = DescriptionEditor.Text?.Trim(),
                    LocationId = _locations[LocationPicker.SelectedIndex].Id,
                    QuantityUnitIdPurchase = _quantityUnits[PurchaseUnitPicker.SelectedIndex].Id,
                    QuantityUnitIdStock = _quantityUnits[StockUnitPicker.SelectedIndex].Id,
                    QuantityUnitFactorPurchaseToStock = factor,
                    MinStockAmount = minStock,
                    DefaultBestBeforeDays = bestBeforeDays,
                    TracksBestBeforeDate = TracksBestBeforeSwitch.IsToggled,
                    IsActive = IsActiveSwitch.IsToggled,
                    ProductGroupId = productGroupId
                };

                var createResult = await _apiClient.CreateProductAsync(createRequest);
                if (!createResult.Success || createResult.Data == null)
                {
                    if (createResult.ErrorMessage?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        await DisplayAlert("Duplicate Product",
                            $"A product named \"{name}\" already exists. Please use a different name.", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", createResult.ErrorMessage ?? "Failed to create product", "OK");
                    }
                    SaveToolbarItem.IsEnabled = true;
                    return;
                }

                var newProductId = createResult.Data.Id;

                // Apply lookup enrichment if a result was selected
                if (_selectedLookupResult != null)
                {
                    // Build DataSources from DTO fields (matches Blazor web app pattern)
                    var dataSources = new Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(_selectedLookupResult.PluginDisplayName)
                        && !string.IsNullOrEmpty(_selectedLookupResult.ExternalId))
                    {
                        dataSources[_selectedLookupResult.PluginDisplayName] = _selectedLookupResult.ExternalId;
                    }

                    if (dataSources.Count > 0)
                    {
                        var applyRequest = new ApplyLookupResultMobileRequest
                        {
                            DataSources = dataSources,
                            Name = _selectedLookupResult.Name,
                            BrandName = _selectedLookupResult.Brand,
                            Barcode = _selectedLookupResult.Barcode,
                            ImageUrl = _selectedLookupResult.ImageUrl,
                            ThumbnailUrl = _selectedLookupResult.ThumbnailUrl,
                            AttributionMarkdown = _selectedLookupResult.AttributionMarkdown
                        };

                        var applyResult = await _apiClient.ApplyLookupResultAsync(newProductId, applyRequest);
                        if (!applyResult.Success)
                        {
                            Console.WriteLine($"[ProductEdit] Apply lookup failed: {applyResult.ErrorMessage}");
                        }
                    }
                }

                // Upload pending barcodes
                foreach (var barcode in _pendingBarcodes)
                {
                    var barcodeResult = await _apiClient.AddProductBarcodeAsync(newProductId, barcode);
                    if (!barcodeResult.Success)
                        Console.WriteLine($"[ProductEdit] Add barcode failed: {barcodeResult.ErrorMessage}");
                }

                // Upload pending images
                foreach (var photo in _pendingImages)
                {
                    try
                    {
                        using var stream = await photo.OpenReadAsync();
                        var imgResult = await _apiClient.UploadProductImageAsync(newProductId, stream, photo.FileName);
                        if (!imgResult.Success)
                            Console.WriteLine($"[ProductEdit] Upload image failed: {imgResult.ErrorMessage}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ProductEdit] Upload image error: {ex.Message}");
                    }
                }

                await Shell.Current.GoToAsync("..");
                return;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }

        SaveToolbarItem.IsEnabled = true;
    }

    private void ShowFormLoading(bool isLoading)
    {
        LoadingIndicator.IsVisible = isLoading;
        LoadingIndicator.IsRunning = isLoading;
        ContentScroll.IsVisible = !isLoading;
    }
}

public class LookupResultDisplayModel
{
    public ProductLookupResultDto Dto { get; }

    private readonly string _serverBaseUrl;

    public LookupResultDisplayModel(ProductLookupResultDto dto, string serverBaseUrl)
    {
        Dto = dto;
        _serverBaseUrl = serverBaseUrl.TrimEnd('/');
    }

    public string Name => Dto.Name;
    public string? Brand => Dto.Brand;
    public string PluginDisplayName => Dto.PluginDisplayName;
    public string? ThumbnailUrl
    {
        get
        {
            var url = Dto.ThumbnailUrl;
            if (string.IsNullOrEmpty(url)) return null;
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_serverBaseUrl))
                url = $"{_serverBaseUrl}{(url.StartsWith('/') ? "" : "/")}{url}";
            return url;
        }
    }
    public decimal? Price => Dto.Price;
    public bool HasPrice => Dto.Price.HasValue && Dto.Price.Value > 0;
}
