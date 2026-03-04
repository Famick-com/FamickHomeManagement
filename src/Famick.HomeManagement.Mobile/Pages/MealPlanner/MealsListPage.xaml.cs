using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.MealPlanner;

public partial class MealsListPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;
    private bool _favoritesOnly;

    public ObservableCollection<MealSummaryMobile> Meals { get; } = new();

    public MealsListPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        MealsCollection.ItemsSource = Meals;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMealsAsync();
    }

    private async Task LoadMealsAsync()
    {
        ShowLoading();

        var result = await _apiClient.GetMealsAsync(
            string.IsNullOrEmpty(_currentSearchTerm) ? null : _currentSearchTerm,
            _favoritesOnly ? true : null);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Meals.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var meal in result.Data)
                    Meals.Add(meal);

                if (Meals.Count > 0)
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
            _ = LoadMealsAsync();
        }, null, 400, Timeout.Infinite);
    }

    private async void OnFilterAllClicked(object? sender, EventArgs e)
    {
        _favoritesOnly = false;
        UpdateFilterChips();
        await LoadMealsAsync();
    }

    private async void OnFilterFavoritesClicked(object? sender, EventArgs e)
    {
        _favoritesOnly = true;
        UpdateFilterChips();
        await LoadMealsAsync();
    }

    private void UpdateFilterChips()
    {
        FilterAll.BackgroundColor = !_favoritesOnly
            ? Color.FromArgb("#1976D2") : Color.FromArgb("#E0E0E0");
        FilterAll.TextColor = !_favoritesOnly
            ? Colors.White : Color.FromArgb("#424242");
        FilterFavorites.BackgroundColor = _favoritesOnly
            ? Color.FromArgb("#1976D2") : Color.FromArgb("#E0E0E0");
        FilterFavorites.TextColor = _favoritesOnly
            ? Colors.White : Color.FromArgb("#424242");
    }

    private async void OnMealSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MealSummaryMobile meal)
        {
            MealsCollection.SelectedItem = null;
            await Shell.Current.GoToAsync(nameof(MealDetailPage),
                new Dictionary<string, object> { ["MealId"] = meal.Id });
        }
    }

    private async void OnAddMealClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(MealEditPage));
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: MealSummaryMobile meal })
        {
            var confirmed = await DisplayAlert("Delete Meal", $"Delete \"{meal.Name}\"?", "Delete", "Cancel");
            if (!confirmed) return;

            var result = await _apiClient.DeleteMealAsync(meal.Id);
            if (result.Success)
            {
                Meals.Remove(meal);
                if (!Meals.Any()) ShowEmpty();
            }
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadMealsAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private void ShowLoading() { LoadingIndicator.IsVisible = true; RefreshContainer.IsVisible = false; EmptyState.IsVisible = false; }
    private void ShowContent() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = true; EmptyState.IsVisible = false; }
    private void ShowEmpty() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = false; EmptyState.IsVisible = true; }
}
