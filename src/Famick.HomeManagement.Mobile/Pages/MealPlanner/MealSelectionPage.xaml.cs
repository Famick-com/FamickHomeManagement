using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.MealPlanner;

[QueryProperty(nameof(PlanId), "PlanId")]
[QueryProperty(nameof(MealTypeId), "MealTypeId")]
[QueryProperty(nameof(DayOfWeek), "DayOfWeek")]
[QueryProperty(nameof(OnEntryAdded), "OnEntryAdded")]
public partial class MealSelectionPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;

    public Guid PlanId { get; set; }
    public Guid MealTypeId { get; set; }
    public int DayOfWeek { get; set; }
    public Action<MealPlanEntryMobile>? OnEntryAdded { get; set; }

    public ObservableCollection<MealSummaryMobile> Meals { get; } = new();

    public MealSelectionPage(ShoppingApiClient apiClient)
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
        LoadingIndicator.IsVisible = true;
        MealsCollection.IsVisible = false;

        var result = await _apiClient.GetMealsAsync(
            string.IsNullOrEmpty(_currentSearchTerm) ? null : _currentSearchTerm);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Meals.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var meal in result.Data)
                    Meals.Add(meal);
            }
            LoadingIndicator.IsVisible = false;
            MealsCollection.IsVisible = true;
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

    private async void OnMealSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MealSummaryMobile meal)
        {
            MealsCollection.SelectedItem = null;

            var request = new CreateMealPlanEntryRequest
            {
                MealId = meal.Id,
                MealTypeId = MealTypeId,
                DayOfWeek = DayOfWeek
            };

            var result = await _apiClient.AddMealPlanEntryAsync(PlanId, request);
            if (result.Success && result.Data != null)
            {
                OnEntryAdded?.Invoke(result.Data);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to add meal", "OK");
            }
        }
    }
}
