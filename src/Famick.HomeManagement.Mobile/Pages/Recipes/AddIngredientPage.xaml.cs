using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Recipes;

[QueryProperty(nameof(RecipeId), "RecipeId")]
[QueryProperty(nameof(StepId), "StepId")]
public partial class AddIngredientPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private List<QuantityUnitSummary> _quantityUnits = new();
    private List<ProductSearchResult> _searchResults = new();
    private ProductSearchResult? _selectedProduct;
    private CancellationTokenSource? _searchDebounce;
    private Guid? _defaultLocationId;
    private Guid? _defaultQuantityUnitId;
    private string _lastSearchTerm = string.Empty;

    // Sentinel ID for the "Create product" option
    private static readonly Guid CreateProductSentinel = Guid.Empty;

    public string RecipeId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;

    public AddIngredientPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.WhenAll(LoadQuantityUnitsAsync(), LoadDefaultsAsync());
    }

    private async Task LoadQuantityUnitsAsync()
    {
        var result = await _apiClient.GetQuantityUnitsAsync();
        if (result.Success && result.Data != null)
        {
            _quantityUnits = result.Data;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UnitPicker.Items.Clear();
                foreach (var unit in _quantityUnits)
                {
                    UnitPicker.Items.Add(unit.Name);
                }
            });
        }
    }

    private async Task LoadDefaultsAsync()
    {
        var locationsResult = await _apiClient.GetLocationsAsync();
        if (locationsResult.Success && locationsResult.Data?.Count > 0)
        {
            _defaultLocationId = locationsResult.Data[0].Id;
        }

        var unitsResult = await _apiClient.GetQuantityUnitsAsync();
        if (unitsResult.Success && unitsResult.Data?.Count > 0)
        {
            _defaultQuantityUnitId = unitsResult.Data[0].Id;
        }
    }

    #region Product Search

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;
        var searchText = e.NewTextValue?.Trim();

        if (string.IsNullOrEmpty(searchText))
        {
            SearchResultsView.IsVisible = false;
            SearchingIndicator.IsVisible = false;
            SearchingIndicator.IsRunning = false;
            return;
        }

        _ = DebounceSearchAsync(searchText, token);
    }

    private async Task DebounceSearchAsync(string searchText, CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            if (token.IsCancellationRequested) return;
            await SearchProductsAsync(searchText);
        }
        catch (TaskCanceledException)
        {
            // Debounce cancelled, ignore
        }
    }

    private async void OnSearchButtonPressed(object? sender, EventArgs e)
    {
        var searchText = ProductSearchEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(searchText))
        {
            await SearchProductsAsync(searchText);
        }
    }

    private async Task SearchProductsAsync(string searchTerm)
    {
        _lastSearchTerm = searchTerm;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SearchingIndicator.IsVisible = true;
            SearchingIndicator.IsRunning = true;
        });

        var result = await _apiClient.SearchProductsForRecipeAsync(searchTerm);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SearchingIndicator.IsVisible = false;
            SearchingIndicator.IsRunning = false;

            var products = new List<ProductSearchResult>();

            if (result.Success && result.Data != null)
            {
                products.AddRange(result.Data);
            }

            // Add "Create product" option if no exact match
            var exactMatch = products.Any(p =>
                p.Name.Equals(searchTerm, StringComparison.OrdinalIgnoreCase));
            if (!exactMatch)
            {
                products.Insert(0, new ProductSearchResult
                {
                    Id = CreateProductSentinel,
                    Name = searchTerm.Trim(),
                    ProductGroupName = null
                });
            }

            _searchResults = products;
            SearchResultsView.ItemsSource = _searchResults;
            SearchResultsView.IsVisible = true;
        });
    }

    private async void OnProductSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProductSearchResult product) return;

        // Reset selection to allow re-selection
        SearchResultsView.SelectedItem = null;

        // Handle "Create product" option
        if (product.Id == CreateProductSentinel)
        {
            await CreateAndSelectProductAsync(product.Name);
            return;
        }

        SelectProduct(product);
    }

    private async Task CreateAndSelectProductAsync(string productName)
    {
        // Try loading defaults if not yet available
        if (!_defaultLocationId.HasValue || !_defaultQuantityUnitId.HasValue)
        {
            await LoadDefaultsAsync();
        }

        if (!_defaultLocationId.HasValue || !_defaultQuantityUnitId.HasValue)
        {
            await DisplayAlertAsync("Error", "Cannot create product — missing default location or unit. Please add a location and quantity unit first.", "OK");
            return;
        }

        var request = new CreateProductRequest
        {
            Name = productName,
            LocationId = _defaultLocationId.Value,
            QuantityUnitIdStock = _defaultQuantityUnitId.Value,
            QuantityUnitIdPurchase = _defaultQuantityUnitId.Value,
            QuantityUnitFactorPurchaseToStock = 1,
            MinStockAmount = 0,
            DefaultBestBeforeDays = 0
        };

        var result = await _apiClient.CreateProductAsync(request);
        if (result.Success && result.Data != null)
        {
            var created = new ProductSearchResult
            {
                Id = result.Data.Id,
                Name = result.Data.Name,
                ProductGroupName = null
            };
            SelectProduct(created);
        }
        else
        {
            await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to create product", "OK");
        }
    }

    private void SelectProduct(ProductSearchResult product)
    {
        _selectedProduct = product;

        // Show selected product card, hide search
        SelectedProductName.Text = product.Name;
        SelectedProductGroup.Text = product.ProductGroupName ?? string.Empty;
        SelectedProductGroup.IsVisible = !string.IsNullOrEmpty(product.ProductGroupName);
        SelectedProductCard.IsVisible = true;
        SearchResultsView.IsVisible = false;
        ProductSearchEntry.IsVisible = false;

        // Show detail form
        DetailForm.IsVisible = true;

        UpdateAddButtonState();
    }

    private void OnChangeProductClicked(object? sender, EventArgs e)
    {
        _selectedProduct = null;
        SelectedProductCard.IsVisible = false;
        DetailForm.IsVisible = false;
        ProductSearchEntry.IsVisible = true;
        ProductSearchEntry.Text = string.Empty;
        ProductSearchEntry.Focus();
        UpdateAddButtonState();
    }

    #endregion

    #region Form

    private void OnFormFieldChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateAddButtonState();
    }

    private void OnUnitPickerChanged(object? sender, EventArgs e)
    {
        UpdateAddButtonState();
    }

    private void UpdateAddButtonState()
    {
        var hasProduct = _selectedProduct != null;
        var hasAmount = decimal.TryParse(AmountEntry.Text, out var amount) && amount > 0;
        var hasUnit = UnitPicker.SelectedIndex >= 0;
        AddButton.IsEnabled = hasProduct && hasAmount && hasUnit;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (_selectedProduct == null) return;
        if (!Guid.TryParse(RecipeId, out var recipeId)) return;
        if (!Guid.TryParse(StepId, out var stepId)) return;
        if (!decimal.TryParse(AmountEntry.Text, out var amount) || amount <= 0) return;
        if (UnitPicker.SelectedIndex < 0 || UnitPicker.SelectedIndex >= _quantityUnits.Count) return;

        AddButton.IsEnabled = false;

        try
        {
            var quantityUnitId = _quantityUnits[UnitPicker.SelectedIndex].Id;

            var request = new CreateRecipeIngredientRequest
            {
                ProductId = _selectedProduct.Id,
                Amount = amount,
                QuantityUnitId = quantityUnitId,
                Note = string.IsNullOrWhiteSpace(NoteEntry.Text) ? null : NoteEntry.Text.Trim()
            };

            var result = await _apiClient.CreateRecipeIngredientAsync(recipeId, stepId, request);
            if (result.Success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to add ingredient", "OK");
            }
        }
        finally
        {
            AddButton.IsEnabled = true;
        }
    }

    #endregion
}
