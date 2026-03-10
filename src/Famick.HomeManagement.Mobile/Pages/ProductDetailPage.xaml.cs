using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

[QueryProperty(nameof(ProductId), "ProductId")]
public partial class ProductDetailPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ProductDto? _product;

    public string ProductId { get; set; } = string.Empty;

    public ProductDetailPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProductAsync();
    }

    private async Task LoadProductAsync()
    {
        if (!Guid.TryParse(ProductId, out var id))
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError("Invalid product ID"));
            return;
        }

        MainThread.BeginInvokeOnMainThread(ShowLoading);

        try
        {
            var result = await _apiClient.GetProductByIdAsync(id);
            if (result.Success && result.Data != null)
            {
                _product = result.Data;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RenderProduct();
                    ShowContent();
                });
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowError(result.ErrorMessage ?? "Failed to load product"));
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                ShowError($"Connection error: {ex.Message}"));
        }
    }

    private void RenderProduct()
    {
        if (_product == null) return;

        // Image
        var imageUrl = _product.PrimaryImageUrl;
        if (!string.IsNullOrEmpty(imageUrl))
        {
            // Resolve relative URLs (e.g. master product static SVGs) against server base
            if (!imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                imageUrl = $"{_apiClient.BaseUrl}{(imageUrl.StartsWith('/') ? "" : "/")}{imageUrl}";

            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                ProductImage.Source = ImageSource.FromUri(uri);
            ImageSection.IsVisible = true;
        }
        else
        {
            ImageSection.IsVisible = false;
        }

        // Title
        TitleLabel.Text = _product.Name;

        // Status badges
        ActiveBadge.IsVisible = _product.IsActive;
        InactiveBadge.IsVisible = !_product.IsActive;
        LowStockBadge.IsVisible = _product.IsBelowMinStock;

        // Description
        if (!string.IsNullOrWhiteSpace(_product.Description))
        {
            DescriptionLabel.Text = _product.Description;
            DescriptionSection.IsVisible = true;
        }
        else
        {
            DescriptionSection.IsVisible = false;
        }

        // Stock info
        TotalStockLabel.Text = $"{_product.TotalStockAmount:F1} {_product.QuantityUnitStockName}";
        MinStockLabel.Text = $"{_product.MinStockAmount:F1} {_product.QuantityUnitStockName}";

        // Stock by location
        StockByLocationList.Children.Clear();
        if (_product.StockByLocation.Count > 0)
        {
            foreach (var loc in _product.StockByLocation)
            {
                var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
                StockByLocationList.Children.Add(new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label
                        {
                            Text = loc.LocationName,
                            FontSize = 13,
                            TextColor = isDark ? Color.FromArgb("#BBBBBB") : Color.FromArgb("#666666"),
                            VerticalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = $"{loc.Amount:F1} ({loc.EntryCount} entries)",
                            FontSize = 13,
                            TextColor = isDark ? Colors.White : Colors.Black,
                            VerticalOptions = LayoutOptions.Center
                        }
                    }
                });
            }
        }

        // Details
        LocationLabel.Text = _product.LocationName;

        if (!string.IsNullOrEmpty(_product.ProductGroupName))
        {
            ProductGroupLabel.Text = _product.ProductGroupName;
            ProductGroupRow.IsVisible = true;
        }
        else
        {
            ProductGroupRow.IsVisible = false;
        }

        PurchaseUnitLabel.Text = _product.QuantityUnitPurchaseName;
        StockUnitLabel.Text = _product.QuantityUnitStockName;

        if (_product.TracksBestBeforeDate && _product.DefaultBestBeforeDays > 0)
        {
            BestBeforeDaysLabel.Text = _product.DefaultBestBeforeDays.ToString();
            BestBeforeRow.IsVisible = true;
        }
        else
        {
            BestBeforeRow.IsVisible = false;
        }

        // Parent/child
        var hasParentChild = _product.ParentProductId.HasValue || _product.IsParentProduct;
        ParentChildSection.IsVisible = hasParentChild;

        if (_product.ParentProductId.HasValue && !string.IsNullOrEmpty(_product.ParentProductName))
        {
            ParentLabel.Text = _product.ParentProductName;
            ParentRow.IsVisible = true;
        }
        else
        {
            ParentRow.IsVisible = false;
        }

        if (_product.IsParentProduct && _product.ChildProductCount > 0)
        {
            ChildCountLabel.Text = _product.ChildProductCount.ToString();
            ChildCountRow.IsVisible = true;
        }
        else
        {
            ChildCountRow.IsVisible = false;
        }

        // Barcodes
        BarcodesList.Children.Clear();
        if (_product.Barcodes.Count > 0)
        {
            BarcodesSection.IsVisible = true;
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            foreach (var barcode in _product.Barcodes)
            {
                var label = new Label
                {
                    Text = barcode.Barcode,
                    FontSize = 14,
                    FontFamily = "OpenSansRegular",
                    TextColor = isDark ? Colors.White : Colors.Black
                };
                BarcodesList.Children.Add(label);
            }
        }
        else
        {
            BarcodesSection.IsVisible = false;
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_product == null) return;
        await Shell.Current.GoToAsync(nameof(ProductEditPage),
            new Dictionary<string, object> { ["ProductId"] = _product.Id.ToString() });
    }

    private async void OnParentProductTapped(object? sender, TappedEventArgs e)
    {
        if (_product?.ParentProductId == null) return;
        await Shell.Current.GoToAsync(nameof(ProductDetailPage),
            new Dictionary<string, object> { ["ProductId"] = _product.ParentProductId.Value.ToString() });
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadProductAsync();
    }

    private void ShowLoading()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;
        ErrorFrame.IsVisible = false;
    }

    private void ShowContent()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = true;
        ErrorFrame.IsVisible = false;
    }

    private void ShowError(string message)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = false;
        ErrorFrame.IsVisible = true;
        ErrorLabel.Text = message;
    }
}
