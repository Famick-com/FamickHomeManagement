using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Chores;

public partial class ChoresListPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;
    private ChoreFilter _currentFilter = ChoreFilter.All;

    public ObservableCollection<ChoreSummaryItem> Chores { get; } = new();

    public ChoresListPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        ChoresCollection.ItemsSource = Chores;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadChoresAsync();
    }

    private async Task LoadChoresAsync()
    {
        ShowLoading();

        ApiResult<List<ChoreSummaryItem>> result;

        switch (_currentFilter)
        {
            case ChoreFilter.Overdue:
                result = await _apiClient.GetOverdueChoreItemsAsync();
                break;
            case ChoreFilter.DueSoon:
                result = await _apiClient.GetChoresDueSoonItemsAsync();
                break;
            default:
                result = await _apiClient.GetChoresAsync(
                    string.IsNullOrEmpty(_currentSearchTerm) ? null : _currentSearchTerm);
                break;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Chores.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var chore in result.Data)
                    Chores.Add(chore);

                if (Chores.Count > 0)
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
            _ = LoadChoresAsync();
        }, null, 400, Timeout.Infinite);
    }

    private async void OnFilterAllClicked(object? sender, EventArgs e)
    {
        _currentFilter = ChoreFilter.All;
        UpdateFilterChips();
        await LoadChoresAsync();
    }

    private async void OnFilterOverdueClicked(object? sender, EventArgs e)
    {
        _currentFilter = ChoreFilter.Overdue;
        UpdateFilterChips();
        await LoadChoresAsync();
    }

    private async void OnFilterDueSoonClicked(object? sender, EventArgs e)
    {
        _currentFilter = ChoreFilter.DueSoon;
        UpdateFilterChips();
        await LoadChoresAsync();
    }

    private void UpdateFilterChips()
    {
        var activeColor = Color.FromArgb("#1976D2");
        var inactiveColor = Color.FromArgb("#E0E0E0");

        FilterAll.BackgroundColor = _currentFilter == ChoreFilter.All ? activeColor : inactiveColor;
        FilterAll.TextColor = _currentFilter == ChoreFilter.All ? Colors.White : Color.FromArgb("#424242");
        FilterOverdue.BackgroundColor = _currentFilter == ChoreFilter.Overdue ? activeColor : inactiveColor;
        FilterOverdue.TextColor = _currentFilter == ChoreFilter.Overdue ? Colors.White : Color.FromArgb("#424242");
        FilterDueSoon.BackgroundColor = _currentFilter == ChoreFilter.DueSoon ? activeColor : inactiveColor;
        FilterDueSoon.TextColor = _currentFilter == ChoreFilter.DueSoon ? Colors.White : Color.FromArgb("#424242");
    }

    private async void OnChoreSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ChoreSummaryItem chore)
        {
            ChoresCollection.SelectedItem = null;
            await Shell.Current.GoToAsync(nameof(ChoreDetailPage),
                new Dictionary<string, object> { ["ChoreId"] = chore.Id.ToString() });
        }
    }

    private async void OnMarkDoneSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ChoreSummaryItem chore })
        {
            var result = await _apiClient.ExecuteChoreAsync(chore.Id);
            if (result.Success)
            {
                await LoadChoresAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to mark chore as done", "OK");
            }
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadChoresAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private async void OnAddChoreClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ChoreEditPage));
    }

    private void ShowLoading() { LoadingIndicator.IsVisible = true; RefreshContainer.IsVisible = false; EmptyState.IsVisible = false; }
    private void ShowContent() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = true; EmptyState.IsVisible = false; }
    private void ShowEmpty() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = false; EmptyState.IsVisible = true; }

    private enum ChoreFilter { All, Overdue, DueSoon }
}
