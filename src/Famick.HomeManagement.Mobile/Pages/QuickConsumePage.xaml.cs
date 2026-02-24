using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

/// <summary>
/// Quick consume page for scanning and consuming inventory items.
/// Supports deep link activation from OS shortcuts and widgets.
/// </summary>
public partial class QuickConsumePage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ProductDto? _currentProduct;
    private List<StockEntryDisplayModel> _stockEntries = new();
    private StockEntryDisplayModel? _selectedEntry;
    private string? _lastScannedBarcode;
    private bool _isInitialized;

    public QuickConsumePage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        WeakReferenceMessenger.Default.Register<BleScannerBarcodeMessage>(this, async (recipient, message) =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _lastScannedBarcode = message.Value;
                await ProcessBarcodeAsync(message.Value);
            });
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Only auto-scan on first appearance
        if (!_isInitialized)
        {
            _isInitialized = true;
            await StartScanAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private async Task StartScanAsync()
    {
        ShowState(PageState.Scanning);

        try
        {
            // Small delay to ensure page is fully rendered
            await Task.Delay(200);

            var scannerPage = new BarcodeScannerPage();
            await Navigation.PushAsync(scannerPage);
            var barcode = await scannerPage.ScanAsync();

            if (string.IsNullOrEmpty(barcode))
            {
                // User cancelled scanning
                await Navigation.PopAsync();
                return;
            }

            _lastScannedBarcode = barcode;
            await ProcessBarcodeAsync(barcode);
        }
        catch (Exception ex)
        {
            ShowError($"Scanner error: {ex.Message}");
        }
    }

    private async Task ProcessBarcodeAsync(string barcode)
    {
        ShowState(PageState.Scanning);

        try
        {
            // Look up product by barcode
            var productResult = await _apiClient.GetProductByBarcodeAsync(barcode);

            if (!productResult.Success || productResult.Data == null)
            {
                ShowNotFound(barcode);
                return;
            }

            _currentProduct = productResult.Data;

            // Get stock entries for this product
            var stockResult = await _apiClient.GetStockByProductAsync(_currentProduct.Id);

            if (!stockResult.Success || stockResult.Data == null || stockResult.Data.Count == 0)
            {
                ShowNotFound(barcode, "Product exists but has no stock.");
                return;
            }

            // Sort by expiry (FEFO) - nulls last, then by date ascending
            var sortedEntries = stockResult.Data
                .OrderBy(e => e.BestBeforeDate == null ? 1 : 0)
                .ThenBy(e => e.BestBeforeDate)
                .ToList();

            _stockEntries = sortedEntries.Select(e => new StockEntryDisplayModel(e)).ToList();

            ShowProductFound();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to look up product: {ex.Message}");
        }
    }

    private void ShowProductFound()
    {
        if (_currentProduct == null) return;

        ProductNameLabel.Text = _currentProduct.Name;
        ProductLocationLabel.Text = $"Default Location: {_currentProduct.LocationName}";
        TotalStockLabel.Text = $"Total in stock: {_currentProduct.TotalStockAmount:F1} {_currentProduct.QuantityUnitStockName}";

        // Load product image
        ProductImage.IsVisible = false;
        var primaryImage = _currentProduct.Images?.FirstOrDefault(i => i.IsPrimary)
            ?? _currentProduct.Images?.FirstOrDefault();
        if (primaryImage != null)
        {
            var imageUrl = primaryImage.ThumbnailDisplayUrl;
            if (!string.IsNullOrEmpty(imageUrl))
            {
                ProductImage.Source = ImageSource.FromUri(new Uri(imageUrl));
                ProductImage.IsVisible = true;
            }
        }

        if (_stockEntries.Count == 1)
        {
            // Single entry - auto-select and show consume actions
            _selectedEntry = _stockEntries[0];
            UpdateSelectedEntryDisplay();
            StockSelectionSection.IsVisible = false;
            SelectedEntryCard.IsVisible = true;
            ConsumeActionsSection.IsVisible = true;
        }
        else
        {
            // Multiple entries - show selection list
            StockEntriesCollection.ItemsSource = _stockEntries;
            StockSelectionSection.IsVisible = true;
            SelectedEntryCard.IsVisible = false;
            ConsumeActionsSection.IsVisible = false;
        }

        PartialAmountSection.IsVisible = false;
        ShowState(PageState.ProductFound);
    }

    private void UpdateSelectedEntryDisplay()
    {
        if (_selectedEntry == null) return;

        SelectedExpiryLabel.Text = _selectedEntry.ExpiryDisplayText;
        SelectedExpiryLabel.TextColor = _selectedEntry.ExpiryTextColor;
        SelectedLocationLabel.Text = $"Location: {_selectedEntry.LocationName ?? "Unknown"}";
        SelectedAmountLabel.Text = $"{_selectedEntry.Amount:F1}";
        SelectedUnitLabel.Text = _selectedEntry.QuantityUnitName;
        AmountUnitLabel.Text = _selectedEntry.QuantityUnitName;
        ConsumeOneButton.Text = $"Consume 1 {_selectedEntry.QuantityUnitName}";
    }

    private void OnStockEntrySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is StockEntryDisplayModel selected)
        {
            _selectedEntry = selected;
            UpdateSelectedEntryDisplay();
            SelectedEntryCard.IsVisible = true;
            ConsumeActionsSection.IsVisible = true;
        }
    }

    private async void OnConsumeEntireClicked(object? sender, EventArgs e)
    {
        if (_selectedEntry == null || _currentProduct == null) return;

        await ConsumeAsync(1);
    }

    private void OnConsumePartClicked(object? sender, EventArgs e)
    {
        if (_selectedEntry == null) return;

        AmountEntry.Text = "1";
        ConsumeActionsSection.IsVisible = false;
        PartialAmountSection.IsVisible = true;
        AmountEntry.Focus();
    }

    private void OnCancelPartialClicked(object? sender, EventArgs e)
    {
        PartialAmountSection.IsVisible = false;
        ConsumeActionsSection.IsVisible = true;
    }

    private async void OnConfirmPartialClicked(object? sender, EventArgs e)
    {
        if (_selectedEntry == null) return;

        if (!decimal.TryParse(AmountEntry.Text, out var amount) || amount <= 0)
        {
            await DisplayAlert("Invalid Amount", "Please enter a valid amount greater than 0.", "OK");
            return;
        }

        if (amount > _selectedEntry.Amount)
        {
            await DisplayAlert("Invalid Amount",
                $"Amount cannot exceed available stock ({_selectedEntry.Amount:F1} {_selectedEntry.QuantityUnitName}).",
                "OK");
            return;
        }

        await ConsumeAsync(amount);
    }

    private async Task ConsumeAsync(decimal amount)
    {
        if (_selectedEntry == null || _currentProduct == null) return;

        try
        {
            // Show loading
            ConsumeActionsSection.IsVisible = false;
            PartialAmountSection.IsVisible = false;

            // Consume the specific stock entry
            var result = await _apiClient.ConsumeStockEntryAsync(_selectedEntry.Id, amount);

            if (result.Success)
            {
                // Haptic feedback
                try
                {
                    HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                }
                catch { /* Ignore haptic errors */ }

                // Refresh widget data in background
                _ = _apiClient.RefreshWidgetDataAsync();

                ShowSuccess(_currentProduct.Name, amount, _selectedEntry.QuantityUnitName);
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Failed to consume stock");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
    }

    private void ShowNotFound(string barcode, string? additionalMessage = null)
    {
        NotFoundBarcodeLabel.Text = $"Barcode: {barcode}";
        if (!string.IsNullOrEmpty(additionalMessage))
        {
            NotFoundBarcodeLabel.Text += $"\n{additionalMessage}";
        }
        ShowState(PageState.NotFound);
    }

    private void ShowSuccess(string productName, decimal amount, string unit)
    {
        SuccessDetailLabel.Text = $"Removed {amount:F1} {unit} of {productName}";
        ShowState(PageState.Success);
    }

    private void ShowError(string message)
    {
        ErrorMessageLabel.Text = message;
        ShowState(PageState.Error);
    }

    private void ShowState(PageState state)
    {
        ScanningState.IsVisible = state == PageState.Scanning;
        ProductFoundState.IsVisible = state == PageState.ProductFound;
        NotFoundState.IsVisible = state == PageState.NotFound;
        SuccessState.IsVisible = state == PageState.Success;
        ErrorState.IsVisible = state == PageState.Error;
    }

    private async void OnScanAnotherClicked(object? sender, EventArgs e)
    {
        // Reset state
        _currentProduct = null;
        _stockEntries.Clear();
        _selectedEntry = null;
        ProductImage.IsVisible = false;
        ProductImage.Source = null;

        await StartScanAsync();
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private enum PageState
    {
        Scanning,
        ProductFound,
        NotFound,
        Success,
        Error
    }
}

/// <summary>
/// Display model for stock entries with computed display properties.
/// </summary>
public class StockEntryDisplayModel
{
    public Guid Id { get; }
    public Guid ProductId { get; }
    public decimal Amount { get; }
    public DateTime? BestBeforeDate { get; }
    public string? LocationName { get; }
    public string QuantityUnitName { get; }
    public bool IsExpired { get; }
    public int? DaysUntilExpiry { get; }

    public StockEntryDisplayModel(StockEntryDto dto)
    {
        Id = dto.Id;
        ProductId = dto.ProductId;
        Amount = dto.Amount;
        BestBeforeDate = dto.BestBeforeDate;
        LocationName = dto.LocationName;
        QuantityUnitName = dto.QuantityUnitName;
        IsExpired = dto.IsExpired;
        DaysUntilExpiry = dto.DaysUntilExpiry;
    }

    public string ExpiryDisplayText
    {
        get
        {
            if (!BestBeforeDate.HasValue)
                return "No expiry date";

            if (IsExpired)
                return $"EXPIRED ({BestBeforeDate.Value:MMM d, yyyy})";

            if (DaysUntilExpiry <= 0)
                return "Expires today";

            if (DaysUntilExpiry == 1)
                return "Expires tomorrow";

            if (DaysUntilExpiry <= 7)
                return $"Expires in {DaysUntilExpiry} days";

            return $"Expires {BestBeforeDate.Value:MMM d, yyyy}";
        }
    }

    public Color ExpiryTextColor
    {
        get
        {
            if (!BestBeforeDate.HasValue)
                return Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.LightGray
                    : Colors.Gray;

            if (IsExpired)
                return Colors.Red;

            if (DaysUntilExpiry <= 3)
                return Colors.OrangeRed;

            if (DaysUntilExpiry <= 7)
                return Colors.Orange;

            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#A5D6A7")
                : Color.FromArgb("#2E7D32");
        }
    }
}
