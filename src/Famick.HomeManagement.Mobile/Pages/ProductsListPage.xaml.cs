using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class ProductsListPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    private List<ProductDto> _allProducts = new();
    private string? _activeFilter; // null = All, "active", "inactive", "low_stock"
    private CancellationTokenSource? _searchDebounce;

    public bool IsRefreshing { get; set; }

    public ProductsListPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        BindingContext = this;
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProductsAsync();
    }

    #region Data Loading

    private async Task LoadProductsAsync()
    {
        ShowLoading(true);

        try
        {
            var searchTerm = SearchEntry.Text?.Trim();

            bool? isActive = _activeFilter switch
            {
                "active" => true,
                "inactive" => false,
                _ => null
            };
            bool? lowStock = _activeFilter == "low_stock" ? true : null;

            var result = await _apiClient.GetProductsAsync(
                searchTerm: string.IsNullOrEmpty(searchTerm) ? null : searchTerm,
                isActive: isActive,
                lowStock: lowStock);

            if (result.Success && result.Data != null)
            {
                _allProducts = result.Data;
            }
            else
            {
                _allProducts = new();
            }

            UpdateList();
        }
        catch (Exception)
        {
            _allProducts = new();
            UpdateList();
        }
        finally
        {
            ShowLoading(false);
            IsRefreshing = false;
            ProductsRefreshView.IsRefreshing = false;
        }
    }

    private void UpdateList()
    {
        var displayItems = _allProducts
            .OrderBy(p => p.Name)
            .Select(p => new ProductListDisplayModel(p))
            .ToList();

        ProductsCollection.ItemsSource = displayItems;
    }

    #endregion

    #region Search

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await LoadProductsAsync();
                    });
                }
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private async void OnSearchSubmitted(object? sender, EventArgs e)
    {
        _searchDebounce?.Cancel();
        await LoadProductsAsync();
    }

    #endregion

    #region Filters

    private void OnFilterAllClicked(object? sender, EventArgs e) => SetFilter(null);
    private void OnFilterActiveClicked(object? sender, EventArgs e) => SetFilter("active");
    private void OnFilterInactiveClicked(object? sender, EventArgs e) => SetFilter("inactive");
    private void OnFilterLowStockClicked(object? sender, EventArgs e) => SetFilter("low_stock");

    private void SetFilter(string? filter)
    {
        if (_activeFilter == filter)
            _activeFilter = null;
        else
            _activeFilter = filter;

        UpdateFilterButtonStyles();
        _ = LoadProductsAsync();
    }

    private void UpdateFilterButtonStyles()
    {
        var activeColor = Color.FromArgb("#1976D2");
        var inactiveLight = Color.FromArgb("#E0E0E0");
        var inactiveDark = Color.FromArgb("#424242");
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var inactiveColor = isDark ? inactiveDark : inactiveLight;
        var activeTextColor = Colors.White;
        var inactiveTextColor = isDark ? Color.FromArgb("#EEEEEE") : Color.FromArgb("#333333");

        FilterAllButton.BackgroundColor = _activeFilter == null ? activeColor : inactiveColor;
        FilterAllButton.TextColor = _activeFilter == null ? activeTextColor : inactiveTextColor;

        FilterActiveButton.BackgroundColor = _activeFilter == "active" ? activeColor : inactiveColor;
        FilterActiveButton.TextColor = _activeFilter == "active" ? activeTextColor : inactiveTextColor;

        FilterInactiveButton.BackgroundColor = _activeFilter == "inactive" ? activeColor : inactiveColor;
        FilterInactiveButton.TextColor = _activeFilter == "inactive" ? activeTextColor : inactiveTextColor;

        FilterLowStockButton.BackgroundColor = _activeFilter == "low_stock" ? activeColor : inactiveColor;
        FilterLowStockButton.TextColor = _activeFilter == "low_stock" ? activeTextColor : inactiveTextColor;
    }

    #endregion

    #region Navigation

    private async void OnAddProductClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ProductEditPage));
    }

    private async void OnProductSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProductListDisplayModel item) return;

        // Clear selection so user can tap again
        ProductsCollection.SelectedItem = null;

        await Shell.Current.GoToAsync(nameof(ProductDetailPage),
            new Dictionary<string, object> { ["ProductId"] = item.Id.ToString() });
    }

    #endregion

    #region Refresh

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadProductsAsync();
    }

    #endregion

    #region Helpers

    private void ShowLoading(bool isLoading)
    {
        LoadingIndicator.IsVisible = isLoading;
        LoadingIndicator.IsRunning = isLoading;
    }

    #endregion
}

public class ProductListDisplayModel
{
    private readonly ProductDto _dto;

    public ProductListDisplayModel(ProductDto dto)
    {
        _dto = dto;
    }

    public Guid Id => _dto.Id;
    public string Name => _dto.Name;
    public string? ProductGroupName => _dto.ProductGroupName;
    public bool HasProductGroup => !string.IsNullOrEmpty(_dto.ProductGroupName);
    public bool IsBelowMinStock => _dto.IsBelowMinStock;
    public bool IsInactive => !_dto.IsActive;
    public bool HasImage => !string.IsNullOrEmpty(_dto.PrimaryImageUrl);

    public string StockDisplay => $"{_dto.TotalStockAmount:F1} {_dto.QuantityUnitStockName}";

    public ImageSource? ImageSource => !string.IsNullOrEmpty(_dto.PrimaryImageUrl)
        ? Microsoft.Maui.Controls.ImageSource.FromUri(new Uri(_dto.PrimaryImageUrl))
        : null;
}
