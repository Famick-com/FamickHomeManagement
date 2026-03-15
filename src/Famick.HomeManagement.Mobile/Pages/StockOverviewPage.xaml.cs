using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class StockOverviewPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    private List<StockOverviewItemDto> _allItems = new();
    private List<StockOverviewDisplayModel> _displayItems = new();
    private string? _activeFilter; // null = All, "expired", "due_soon", "below_min"
    private CancellationTokenSource? _searchDebounce;
    private bool _hasCheckedOnboarding;

    public bool IsRefreshing { get; set; }

    public StockOverviewPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        BindingContext = this;
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Check product onboarding on first visit
        if (!_hasCheckedOnboarding)
        {
            _hasCheckedOnboarding = true;
            try
            {
                var result = await _apiClient.GetProductOnboardingStateAsync();

                System.Diagnostics.Debug.WriteLine(
                    $"[ProductOnboarding] StockOverview API result: Success={result.Success}, " +
                    $"HasData={result.Data != null}, " +
                    $"HasCompleted={result.Data?.HasCompletedOnboarding}, " +
                    $"Error={result.ErrorMessage}");

                if (result.Success && result.Data != null && !result.Data.HasCompletedOnboarding)
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    var onboardingPage = services?.GetRequiredService<ProductOnboardingIntroPage>();
                    if (onboardingPage != null)
                    {
                        await Navigation.PushAsync(onboardingPage);
                        return; // Don't load data yet; it will load when user comes back
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProductOnboarding] StockOverview check failed: {ex.Message}");
            }
        }

        await LoadDataAsync();
    }

    #region Data Loading

    private async Task LoadDataAsync(string? searchTerm = null)
    {
        ShowLoading(true);

        try
        {
            var result = await _apiClient.GetStockOverviewAsync(
                searchTerm: searchTerm);

            if (result.Success && result.Data != null)
            {
                _allItems = result.Data;
                UpdateStatistics();
                ApplyFilter();
            }
            else
            {
                _allItems = new();
                UpdateStatistics();
                ApplyFilter();
            }
        }
        catch (Exception)
        {
            _allItems = new();
            UpdateStatistics();
            ApplyFilter();
        }
        finally
        {
            ShowLoading(false);
            IsRefreshing = false;
            StockRefreshView.IsRefreshing = false;
        }
    }

    private void UpdateStatistics()
    {
        TotalItemsCount.Text = _allItems.Count.ToString();
        ExpiredCount.Text = _allItems.Count(i => i.IsExpired).ToString();
        DueSoonCount.Text = _allItems.Count(i => i.IsDueSoon && !i.IsExpired).ToString();
        BelowMinCount.Text = _allItems.Count(i => i.IsBelowMinStock).ToString();
    }

    private void ApplyFilter()
    {
        IEnumerable<StockOverviewItemDto> filtered = _activeFilter switch
        {
            "expired" => _allItems.Where(i => i.IsExpired),
            "due_soon" => _allItems.Where(i => i.IsDueSoon && !i.IsExpired),
            "below_min" => _allItems.Where(i => i.IsBelowMinStock),
            _ => _allItems
        };

        _displayItems = filtered
            .OrderBy(i => i.NextDueDate ?? DateTime.MaxValue)
            .Select(i => new StockOverviewDisplayModel(i))
            .ToList();

        ProductsCollection.ItemsSource = _displayItems;
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
                        var query = SearchEntry.Text?.Trim();
                        await LoadDataAsync(string.IsNullOrEmpty(query) ? null : query);
                    });
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when debounce is cancelled
            }
        }, token);
    }

    private async void OnSearchSubmitted(object? sender, EventArgs e)
    {
        _searchDebounce?.Cancel();
        var query = SearchEntry.Text?.Trim();
        await LoadDataAsync(string.IsNullOrEmpty(query) ? null : query);
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        var scannerPage = new BarcodeScannerPage();
        await Navigation.PushAsync(scannerPage);
        var barcode = await scannerPage.ScanAsync();

        if (!string.IsNullOrEmpty(barcode))
        {
            SearchEntry.Text = barcode;
            await LoadDataAsync(barcode);
        }
    }

    #endregion

    #region Filters

    private void OnFilterAllClicked(object? sender, EventArgs e) => SetFilter(null);
    private void OnFilterExpiredClicked(object? sender, EventArgs e) => SetFilter("expired");
    private void OnFilterDueSoonClicked(object? sender, EventArgs e) => SetFilter("due_soon");
    private void OnFilterBelowMinClicked(object? sender, EventArgs e) => SetFilter("below_min");

    private void OnExpiredCardTapped(object? sender, TappedEventArgs e) => SetFilter("expired");
    private void OnDueSoonCardTapped(object? sender, TappedEventArgs e) => SetFilter("due_soon");
    private void OnBelowMinCardTapped(object? sender, TappedEventArgs e) => SetFilter("below_min");

    private void SetFilter(string? filter)
    {
        // Toggle: if tapping the active filter, go back to All
        if (_activeFilter == filter)
            _activeFilter = null;
        else
            _activeFilter = filter;

        UpdateFilterButtonStyles();
        ApplyFilter();
    }

    private void UpdateFilterButtonStyles()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var activeColor = isDark ? Color.FromArgb("#42A5F5") : Color.FromArgb("#1976D2");
        var inactiveColor = isDark ? Color.FromArgb("#4A4A4A") : Color.FromArgb("#E0E0E0");
        var activeTextColor = isDark ? Colors.Black : Colors.White;
        var inactiveTextColor = isDark ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#333333");

        FilterAllButton.BackgroundColor = _activeFilter == null ? activeColor : inactiveColor;
        FilterAllButton.TextColor = _activeFilter == null ? activeTextColor : inactiveTextColor;

        FilterExpiredButton.BackgroundColor = _activeFilter == "expired" ? activeColor : inactiveColor;
        FilterExpiredButton.TextColor = _activeFilter == "expired" ? activeTextColor : inactiveTextColor;

        FilterDueSoonButton.BackgroundColor = _activeFilter == "due_soon" ? activeColor : inactiveColor;
        FilterDueSoonButton.TextColor = _activeFilter == "due_soon" ? activeTextColor : inactiveTextColor;

        FilterBelowMinButton.BackgroundColor = _activeFilter == "below_min" ? activeColor : inactiveColor;
        FilterBelowMinButton.TextColor = _activeFilter == "below_min" ? activeTextColor : inactiveTextColor;
    }

    #endregion

    #region Quick Actions

    private async void OnConsumeOneClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not StockOverviewDisplayModel item) return;

        try
        {
            var result = await _apiClient.QuickConsumeAsync(new QuickConsumeRequest
            {
                ProductId = item.ProductId,
                Amount = 1
            });
            if (result.Success)
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                await LoadDataAsync(SearchEntry.Text?.Trim());
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to consume", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to consume: {ex.Message}", "OK");
        }
    }

    private async void OnSpoilClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not StockOverviewDisplayModel item) return;

        try
        {
            if (item.StockEntryCount > 1)
            {
                var entriesResult = await _apiClient.GetStockByProductAsync(item.ProductId);
                if (!entriesResult.Success || entriesResult.Data == null || entriesResult.Data.Count == 0)
                {
                    await DisplayAlert("Error", entriesResult.ErrorMessage ?? "Failed to load stock entries", "OK");
                    return;
                }

                var entries = entriesResult.Data;
                var options = entries.Select(entry =>
                {
                    var dateText = entry.BestBeforeDate.HasValue
                        ? $"Expires {entry.BestBeforeDate.Value:MMM d}"
                        : "No expiry";
                    return $"{entry.Amount} {item.QuantityUnitName} — {dateText}";
                }).ToList();

                options.Add("Spoil All");

                var choice = await DisplayActionSheet(
                    $"Spoil which \"{item.ProductName}\" entry?",
                    "Cancel", null, options.ToArray());

                if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                    return;

                if (choice == "Spoil All")
                {
                    var result = await _apiClient.QuickConsumeAsync(new QuickConsumeRequest
                    {
                        ProductId = item.ProductId,
                        ConsumeAll = true,
                        Spoiled = true
                    });
                    if (!result.Success)
                    {
                        await DisplayAlert("Error", result.ErrorMessage ?? "Failed to mark as spoiled", "OK");
                        return;
                    }
                }
                else
                {
                    var selectedIndex = options.IndexOf(choice);
                    if (selectedIndex >= 0 && selectedIndex < entries.Count)
                    {
                        var selectedEntry = entries[selectedIndex];
                        var result = await _apiClient.ConsumeStockEntryAsync(
                            selectedEntry.Id, selectedEntry.Amount, spoiled: true);
                        if (!result.Success)
                        {
                            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to mark as spoiled", "OK");
                            return;
                        }
                    }
                }

                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                await LoadDataAsync(SearchEntry.Text?.Trim());
            }
            else
            {
                var confirmed = await DisplayAlert("Spoil Item",
                    $"Mark all \"{item.ProductName}\" stock as spoiled?", "Spoil", "Cancel");
                if (!confirmed) return;

                var result = await _apiClient.QuickConsumeAsync(new QuickConsumeRequest
                {
                    ProductId = item.ProductId,
                    ConsumeAll = true,
                    Spoiled = true
                });
                if (result.Success)
                {
                    HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                    await LoadDataAsync(SearchEntry.Text?.Trim());
                }
                else
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to mark as spoiled", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to mark as spoiled: {ex.Message}", "OK");
        }
    }

    private async void OnAddOneClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not StockOverviewDisplayModel item) return;

        try
        {
            ApiResult<bool> result;

            if (item.TracksBestBeforeDate)
            {
                var popup = new BestBeforeDatePopup(item.ProductName, item.DefaultBestBeforeDays);
                var popupResult = await this.ShowPopupAsync<BestBeforeDateResult>(popup, PopupOptions.Empty, CancellationToken.None);

                if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null)
                    return;

                var dateResult = popupResult.Result;

                if (dateResult.HasDate)
                {
                    result = await _apiClient.QuickAddStockAsync(item.ProductId, 1, dateResult.Date);
                }
                else
                {
                    result = await _apiClient.QuickAddStockAsync(item.ProductId, 1);
                }
            }
            else
            {
                result = await _apiClient.QuickAddStockAsync(item.ProductId, 1);
            }

            if (result.Success)
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                await LoadDataAsync(SearchEntry.Text?.Trim());
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to add stock", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add stock: {ex.Message}", "OK");
        }
    }

    #endregion

    #region Refresh

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        var query = SearchEntry.Text?.Trim();
        await LoadDataAsync(string.IsNullOrEmpty(query) ? null : query);
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

/// <summary>
/// Display model wrapping StockOverviewItemDto with computed display properties.
/// </summary>
public class StockOverviewDisplayModel
{
    private readonly StockOverviewItemDto _dto;

    public StockOverviewDisplayModel(StockOverviewItemDto dto)
    {
        _dto = dto;
    }

    public Guid ProductId => _dto.ProductId;
    public string ProductName => _dto.ProductName;
    public string? ProductGroupName => _dto.ProductGroupName;
    public bool HasProductGroup => !string.IsNullOrEmpty(_dto.ProductGroupName);
    public decimal TotalAmount => _dto.TotalAmount;
    public string QuantityUnitName => _dto.QuantityUnitName;
    public bool IsExpired => _dto.IsExpired;
    public bool IsDueSoon => _dto.IsDueSoon;
    public bool IsDueSoonOnly => _dto.IsDueSoon && !_dto.IsExpired;
    public bool IsBelowMinStock => _dto.IsBelowMinStock;
    public string? PrimaryImageUrl => _dto.PrimaryImageUrl;
    public bool HasImage => !string.IsNullOrEmpty(_dto.PrimaryImageUrl);
    public bool TracksBestBeforeDate => _dto.TracksBestBeforeDate;
    public int DefaultBestBeforeDays => _dto.DefaultBestBeforeDays;
    public int StockEntryCount => _dto.StockEntryCount;

    public ImageSource? ImageSource => !string.IsNullOrEmpty(_dto.PrimaryImageUrl)
        ? Microsoft.Maui.Controls.ImageSource.FromUri(new Uri(_dto.PrimaryImageUrl))
        : null;

    public string ExpiryDisplayText
    {
        get
        {
            if (!_dto.NextDueDate.HasValue)
                return "";

            if (_dto.IsExpired)
                return $"Expired {_dto.NextDueDate.Value:MMM d}";

            if (_dto.DaysUntilDue <= 0)
                return "Expires today";

            if (_dto.DaysUntilDue == 1)
                return "Expires tomorrow";

            if (_dto.DaysUntilDue <= 7)
                return $"Expires in {_dto.DaysUntilDue}d";

            return $"Expires {_dto.NextDueDate.Value:MMM d}";
        }
    }

    public Color ExpiryTextColor
    {
        get
        {
            if (!_dto.NextDueDate.HasValue)
                return Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.LightGray : Colors.Gray;

            if (_dto.IsExpired)
                return Colors.Red;

            if (_dto.DaysUntilDue <= 3)
                return Colors.OrangeRed;

            if (_dto.DaysUntilDue <= 7)
                return Colors.Orange;

            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#A5D6A7")
                : Color.FromArgb("#2E7D32");
        }
    }
}
