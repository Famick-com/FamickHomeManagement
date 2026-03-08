using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Equipment;

public partial class EquipmentListPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;
    private Guid? _currentCategoryId;
    private List<EquipmentCategoryItem> _categories = new();

    public ObservableCollection<EquipmentSummaryItem> Equipment { get; } = new();

    public EquipmentListPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        EquipmentCollection.ItemsSource = Equipment;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await LoadCategoriesAsync();
            await LoadEquipmentAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EquipmentListPage] OnAppearing error: {ex}");
            MainThread.BeginInvokeOnMainThread(() => ShowEmpty());
        }
    }

    private async Task LoadCategoriesAsync()
    {
        var result = await _apiClient.GetEquipmentCategoriesAsync();
        if (result.Success && result.Data != null)
        {
            _categories = result.Data;
            MainThread.BeginInvokeOnMainThread(RenderCategoryChips);
        }
    }

    private void RenderCategoryChips()
    {
        // Keep "All" button, remove others
        while (CategoryChips.Children.Count > 1)
            CategoryChips.Children.RemoveAt(CategoryChips.Children.Count - 1);

        foreach (var cat in _categories)
        {
            var catId = cat.Id;
            var btn = new Button
            {
                Text = cat.Name,
                CornerRadius = 16,
                Padding = new Thickness(12, 6),
                FontSize = 13,
                BackgroundColor = _currentCategoryId == catId
                    ? Color.FromArgb("#1976D2")
                    : (Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0")),
                TextColor = _currentCategoryId == catId
                    ? Colors.White
                    : (Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#424242"))
            };
            btn.Clicked += async (_, _) =>
            {
                _currentCategoryId = _currentCategoryId == catId ? null : catId;
                UpdateFilterChips();
                await LoadEquipmentAsync();
            };
            CategoryChips.Children.Add(btn);
        }

        UpdateFilterChips();
    }

    private async Task LoadEquipmentAsync()
    {
        ShowLoading();

        var result = await _apiClient.GetEquipmentListAsync(
            string.IsNullOrEmpty(_currentSearchTerm) ? null : _currentSearchTerm,
            _currentCategoryId);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Equipment.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var item in result.Data)
                    Equipment.Add(item);

                if (Equipment.Count > 0)
                    ShowContent();
                else
                    ShowEmpty();
            }
            else
            {
                ShowEmpty();
            }
        });
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new Timer(_ =>
        {
            _currentSearchTerm = e.NewTextValue ?? string.Empty;
            _ = LoadEquipmentAsync();
        }, null, 400, Timeout.Infinite);
    }

    private async void OnFilterAllClicked(object? sender, EventArgs e)
    {
        _currentCategoryId = null;
        UpdateFilterChips();
        await LoadEquipmentAsync();
    }

    private void UpdateFilterChips()
    {
        var activeColor = Color.FromArgb("#1976D2");
        var inactiveLight = Color.FromArgb("#E0E0E0");
        var inactiveDark = Color.FromArgb("#424242");
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var inactiveColor = isDark ? inactiveDark : inactiveLight;

        FilterAll.BackgroundColor = _currentCategoryId == null ? activeColor : inactiveColor;
        FilterAll.TextColor = _currentCategoryId == null ? Colors.White
            : (isDark ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#424242"));

        for (int i = 1; i < CategoryChips.Children.Count; i++)
        {
            if (CategoryChips.Children[i] is Button btn)
            {
                var catIndex = i - 1;
                if (catIndex < _categories.Count)
                {
                    var isActive = _currentCategoryId == _categories[catIndex].Id;
                    btn.BackgroundColor = isActive ? activeColor : inactiveColor;
                    btn.TextColor = isActive ? Colors.White
                        : (isDark ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#424242"));
                }
            }
        }
    }

    private async void OnEquipmentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is EquipmentSummaryItem item)
        {
            EquipmentCollection.SelectedItem = null;
            await Shell.Current.GoToAsync(nameof(EquipmentDetailPage),
                new Dictionary<string, object> { ["EquipmentId"] = item.Id.ToString() });
        }
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: EquipmentSummaryItem item })
        {
            var confirmed = await DisplayAlert("Delete Equipment",
                $"Are you sure you want to delete \"{item.Name}\"?", "Delete", "Cancel");
            if (!confirmed) return;

            var result = await _apiClient.DeleteEquipmentAsync(item.Id);
            if (result.Success)
            {
                await LoadEquipmentAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete equipment", "OK");
            }
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadCategoriesAsync();
        await LoadEquipmentAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private async void OnAddEquipmentClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(EquipmentEditPage));
    }

    private void ShowLoading() { LoadingIndicator.IsVisible = true; RefreshContainer.IsVisible = false; EmptyState.IsVisible = false; }
    private void ShowContent() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = true; EmptyState.IsVisible = false; }
    private void ShowEmpty() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = false; EmptyState.IsVisible = true; }
}
