using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.StorageBins;

public partial class StorageBinListPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;
    private List<StorageBinSummaryItem> _allBins = new();
    private GroupMode _currentGroupMode = GroupMode.All;

    public ObservableCollection<StorageBinGroup> BinGroups { get; } = new();

    private enum GroupMode { All, ByLocation, ByCategory }

    public StorageBinListPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        BinsCollection.ItemsSource = BinGroups;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await LoadBinsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StorageBinListPage] OnAppearing error: {ex}");
            MainThread.BeginInvokeOnMainThread(() => ShowEmpty());
        }
    }

    private async Task LoadBinsAsync()
    {
        ShowLoading();

        var result = await _apiClient.GetStorageBinsAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (result.Success && result.Data != null)
            {
                _allBins = result.Data;
                ApplyFilterAndGrouping();
            }
            else
            {
                ShowEmpty();
            }
        });
    }

    private void ApplyFilterAndGrouping()
    {
        var filtered = _allBins.AsEnumerable();

        if (!string.IsNullOrEmpty(_currentSearchTerm))
        {
            var term = _currentSearchTerm.ToLowerInvariant();
            filtered = filtered.Where(b =>
                (b.ShortCode?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (b.DescriptionPreview?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (b.LocationName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (b.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = filtered.ToList();

        BinGroups.Clear();

        if (list.Count == 0)
        {
            ShowEmpty();
            return;
        }

        switch (_currentGroupMode)
        {
            case GroupMode.ByLocation:
                var byLocation = list
                    .GroupBy(b => b.LocationName ?? "No Location")
                    .OrderBy(g => g.Key);
                foreach (var group in byLocation)
                    BinGroups.Add(new StorageBinGroup(group.Key, group));
                break;

            case GroupMode.ByCategory:
                var byCategory = list
                    .GroupBy(b => b.Category ?? "Uncategorized")
                    .OrderBy(g => g.Key);
                foreach (var group in byCategory)
                    BinGroups.Add(new StorageBinGroup(group.Key, group));
                break;

            default:
                BinGroups.Add(new StorageBinGroup("All Storage Bins", list));
                break;
        }

        ShowContent();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new Timer(_ =>
        {
            _currentSearchTerm = e.NewTextValue ?? string.Empty;
            MainThread.BeginInvokeOnMainThread(ApplyFilterAndGrouping);
        }, null, 400, Timeout.Infinite);
    }

    private void OnGroupAllClicked(object? sender, EventArgs e)
    {
        _currentGroupMode = GroupMode.All;
        UpdateGroupChips();
        ApplyFilterAndGrouping();
    }

    private void OnGroupByLocationClicked(object? sender, EventArgs e)
    {
        _currentGroupMode = GroupMode.ByLocation;
        UpdateGroupChips();
        ApplyFilterAndGrouping();
    }

    private void OnGroupByCategoryClicked(object? sender, EventArgs e)
    {
        _currentGroupMode = GroupMode.ByCategory;
        UpdateGroupChips();
        ApplyFilterAndGrouping();
    }

    private void UpdateGroupChips()
    {
        var activeColor = Color.FromArgb("#1976D2");
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var inactiveColor = isDark ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");
        var activeText = Colors.White;
        var inactiveText = isDark ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#424242");

        GroupAll.BackgroundColor = _currentGroupMode == GroupMode.All ? activeColor : inactiveColor;
        GroupAll.TextColor = _currentGroupMode == GroupMode.All ? activeText : inactiveText;

        GroupByLocation.BackgroundColor = _currentGroupMode == GroupMode.ByLocation ? activeColor : inactiveColor;
        GroupByLocation.TextColor = _currentGroupMode == GroupMode.ByLocation ? activeText : inactiveText;

        GroupByCategory.BackgroundColor = _currentGroupMode == GroupMode.ByCategory ? activeColor : inactiveColor;
        GroupByCategory.TextColor = _currentGroupMode == GroupMode.ByCategory ? activeText : inactiveText;
    }

    private async void OnBinSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is StorageBinSummaryItem item)
        {
            BinsCollection.SelectedItem = null;
            await Shell.Current.GoToAsync(nameof(StorageBinDetailPage),
                new Dictionary<string, object> { ["StorageBinId"] = item.Id.ToString() });
        }
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: StorageBinSummaryItem item })
        {
            var confirmed = await DisplayAlert("Delete Storage Bin",
                $"Are you sure you want to delete bin \"{item.ShortCode}\"?", "Delete", "Cancel");
            if (!confirmed) return;

            var result = await _apiClient.DeleteStorageBinAsync(item.Id);
            if (result.Success)
            {
                await LoadBinsAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete storage bin", "OK");
            }
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadBinsAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private async void OnAddBinClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(StorageBinEditPage));
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        try
        {
            var scannerPage = new BarcodeScannerPage();
            await Navigation.PushAsync(scannerPage);
            var scannedValue = await scannerPage.ScanAsync();

            if (string.IsNullOrEmpty(scannedValue)) return;

            // Extract short code from URL or use raw value
            var shortCode = ExtractShortCode(scannedValue);

            var result = await _apiClient.GetStorageBinByCodeAsync(shortCode);
            if (result.Success && result.Data != null)
            {
                await Shell.Current.GoToAsync(nameof(StorageBinDetailPage),
                    new Dictionary<string, object> { ["StorageBinId"] = result.Data.Id.ToString() });
            }
            else
            {
                await DisplayAlert("Not Found", $"No storage bin found for code \"{shortCode}\"", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Scan failed: {ex.Message}", "OK");
        }
    }

    private static string ExtractShortCode(string value)
    {
        // Try to parse as URL: https://app.famick.com/storage/{tenantId}/{shortCode}
        // or https://host/storage/{tenantId}/{shortCode}
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.AbsolutePath.StartsWith("/storage/"))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 3)
                return segments[2];
        }

        // Raw short code
        return value;
    }

    private async void OnPrintLabelsClicked(object? sender, EventArgs e)
    {
        try
        {
            var popup = new StorageBinLabelPopup();
            var result = await this.ShowPopupAsync<StorageBinLabelPopupResult>(popup, PopupOptions.Empty, CancellationToken.None);

            if (result is StorageBinLabelPopupResult labelResult)
            {
                var request = new GenerateLabelSheetMobileRequest
                {
                    SheetCount = labelResult.SheetCount,
                    LabelFormat = labelResult.LabelFormat,
                    RepeatToFill = labelResult.RepeatToFill,
                    BinIds = labelResult.BinIds
                };

                var apiResult = await _apiClient.GenerateStorageBinLabelSheetAsync(request);
                if (apiResult.Success && apiResult.Data != null)
                {
                    var path = Path.Combine(FileSystem.CacheDirectory, "storage-bin-labels.pdf");
                    await File.WriteAllBytesAsync(path, apiResult.Data);
                    await Launcher.Default.OpenAsync(new OpenFileRequest(
                        "Storage Bin Labels",
                        new ReadOnlyFile(path, "application/pdf")));
                }
                else
                {
                    await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to generate labels", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to print labels: {ex.Message}", "OK");
        }
    }

    private void ShowLoading() { LoadingIndicator.IsVisible = true; RefreshContainer.IsVisible = false; EmptyState.IsVisible = false; }
    private void ShowContent() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = true; EmptyState.IsVisible = false; }
    private void ShowEmpty() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = false; EmptyState.IsVisible = true; }
}
