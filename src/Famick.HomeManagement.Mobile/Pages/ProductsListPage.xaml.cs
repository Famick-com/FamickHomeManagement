using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class ProductsListPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly ObservableCollection<ProductListDisplayModel> _displayItems = new();

    private string? _activeFilter; // null = All, "active", "inactive", "low_stock"
    private CancellationTokenSource? _searchDebounce;

    private int _currentPage;
    private bool _hasMorePages;
    private bool _isLoadingMore;

    public bool IsRefreshing { get; set; }

    public ProductsListPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        BindingContext = this;
        _apiClient = apiClient;
        ProductsCollection.ItemsSource = _displayItems;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        WeakReferenceMessenger.Default.Register<BleScannerBarcodeMessage>(this, async (recipient, message) =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _searchDebounce?.Cancel();
                SearchEntry.Text = message.Value;
                await LoadProductsAsync();
            });
        });

        await LoadProductsAsync();
    }

    #region Data Loading

    private async Task LoadProductsAsync()
    {
        ShowLoading(true);

        try
        {
            _displayItems.Clear();
            _currentPage = 0;
            _hasMorePages = true;
            await LoadPageAsync(1);
        }
        finally
        {
            ShowLoading(false);
            IsRefreshing = false;
            ProductsRefreshView.IsRefreshing = false;
        }
    }

    private async Task LoadPageAsync(int page)
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
            lowStock: lowStock,
            page: page);

        if (result.Success && result.Data != null)
        {
            _currentPage = page;
            _hasMorePages = result.Data.HasNextPage;

            foreach (var product in result.Data.Items)
            {
                _displayItems.Add(new ProductListDisplayModel(product, _apiClient.BaseUrl));
            }
        }
        else
        {
            _hasMorePages = false;
        }
    }

    #endregion

    #region Infinite Scroll

    private async void OnLoadMoreProducts(object? sender, EventArgs e)
    {
        if (_isLoadingMore || !_hasMorePages) return;

        _isLoadingMore = true;
        ShowLoadMore(true);

        try
        {
            await LoadPageAsync(_currentPage + 1);
        }
        finally
        {
            _isLoadingMore = false;
            ShowLoadMore(false);
        }
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

    private void ShowLoadMore(bool isLoading)
    {
        LoadMoreIndicator.IsVisible = isLoading;
        LoadMoreIndicator.IsRunning = isLoading;
    }

    #endregion
}

public class ProductListDisplayModel
{
    private readonly ProductDto _dto;
    private readonly string _serverBaseUrl;

    public ProductListDisplayModel(ProductDto dto, string serverBaseUrl)
    {
        _dto = dto;
        _serverBaseUrl = serverBaseUrl.TrimEnd('/');
    }

    public Guid Id => _dto.Id;
    public string Name => _dto.Name;
    public string? ProductGroupName => _dto.ProductGroupName;
    public bool HasProductGroup => !string.IsNullOrEmpty(_dto.ProductGroupName);
    public bool IsBelowMinStock => _dto.IsBelowMinStock;
    public bool IsInactive => !_dto.IsActive;
    public bool HasImage => !string.IsNullOrEmpty(_dto.PrimaryImageUrl);

    public string StockDisplay => $"{_dto.TotalStockAmount:F1} {_dto.QuantityUnitStockName}";

    public ImageSource? ImageSource
    {
        get
        {
            var url = _dto.PrimaryImageUrl;
            if (string.IsNullOrEmpty(url)) return null;

            // Resolve relative URLs against the server base URL
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_serverBaseUrl))
                url = $"{_serverBaseUrl}{(url.StartsWith('/') ? "" : "/")}{url}";

            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                ? Microsoft.Maui.Controls.ImageSource.FromUri(uri)
                : null;
        }
    }
}
