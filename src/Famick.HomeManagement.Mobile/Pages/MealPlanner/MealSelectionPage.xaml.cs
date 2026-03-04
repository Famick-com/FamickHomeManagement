using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.MealPlanner;

[QueryProperty(nameof(PlanId), "PlanId")]
[QueryProperty(nameof(MealTypeId), "MealTypeId")]
[QueryProperty(nameof(DayOfWeek), "DayOfWeek")]
[QueryProperty(nameof(Version), "Version")]
public partial class MealSelectionPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;
    private MealPlanMobile? _currentPlan;

    private enum Tab { Meals, Recipes, Note }
    private Tab _activeTab = Tab.Meals;

    public Guid PlanId { get; set; }
    public Guid MealTypeId { get; set; }
    public int DayOfWeek { get; set; }
    public uint Version { get; set; }

    public ObservableCollection<MealSummaryMobile> Meals { get; } = new();
    public ObservableCollection<RecipeSummary> Recipes { get; } = new();

    public MealSelectionPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        MealsCollection.ItemsSource = Meals;
        RecipesCollection.ItemsSource = Recipes;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPlanAsync();
        await LoadDataAsync();
    }

    private async Task LoadPlanAsync()
    {
        try
        {
            var response = await _apiClient.GetMealPlanByIdAsync(PlanId);
            if (response.Success && response.Data != null)
                _currentPlan = response.Data;
        }
        catch
        {
            // Non-critical; batch link prompt will just be skipped
        }
    }

    #region Tab Switching

    private async void OnTabMealsClicked(object? sender, EventArgs e)
    {
        if (_activeTab == Tab.Meals) return;
        _activeTab = Tab.Meals;
        _currentSearchTerm = string.Empty;
        SearchEntry.Text = string.Empty;
        UpdateTabUI();
        await LoadDataAsync();
    }

    private async void OnTabRecipesClicked(object? sender, EventArgs e)
    {
        if (_activeTab == Tab.Recipes) return;
        _activeTab = Tab.Recipes;
        _currentSearchTerm = string.Empty;
        SearchEntry.Text = string.Empty;
        UpdateTabUI();
        await LoadDataAsync();
    }

    private void OnTabNoteClicked(object? sender, EventArgs e)
    {
        if (_activeTab == Tab.Note) return;
        _activeTab = Tab.Note;
        UpdateTabUI();
    }

    private void UpdateTabUI()
    {
        // Tab chip colors
        SetTabActive(TabMeals, _activeTab == Tab.Meals);
        SetTabActive(TabRecipes, _activeTab == Tab.Recipes);
        SetTabActive(TabNote, _activeTab == Tab.Note);

        // Visibility
        SearchRow.IsVisible = _activeTab != Tab.Note;
        NewMealButton.IsVisible = _activeTab == Tab.Meals;
        QuickNoteSection.IsVisible = _activeTab == Tab.Note;
        ListHeader.IsVisible = _activeTab != Tab.Note;
        MealsCollection.IsVisible = false;
        RecipesCollection.IsVisible = false;
        LoadingIndicator.IsVisible = false;

        SearchEntry.Placeholder = _activeTab == Tab.Recipes ? "Search recipes..." : "Search meals...";
        ListHeader.Text = _activeTab == Tab.Recipes ? "Select a recipe to add" : "Select a meal to add";
    }

    private static void SetTabActive(Button tab, bool active)
    {
        tab.BackgroundColor = active ? Color.FromArgb("#1976D2") : Color.FromArgb("#E0E0E0");
        tab.TextColor = active ? Colors.White : Color.FromArgb("#424242");
    }

    #endregion

    #region Data Loading

    private async Task LoadDataAsync()
    {
        if (_activeTab == Tab.Note) return;

        LoadingIndicator.IsVisible = true;
        MealsCollection.IsVisible = false;
        RecipesCollection.IsVisible = false;

        if (_activeTab == Tab.Meals)
            await LoadMealsAsync();
        else
            await LoadRecipesAsync();
    }

    private async Task LoadMealsAsync()
    {
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

    private async Task LoadRecipesAsync()
    {
        var result = await _apiClient.GetRecipesAsync(
            string.IsNullOrEmpty(_currentSearchTerm) ? null : _currentSearchTerm);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Recipes.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var recipe in result.Data)
                    Recipes.Add(recipe);
            }
            LoadingIndicator.IsVisible = false;
            RecipesCollection.IsVisible = true;
        });
    }

    #endregion

    #region Search

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new Timer(_ =>
        {
            _currentSearchTerm = e.NewTextValue ?? string.Empty;
            _ = LoadDataAsync();
        }, null, 400, Timeout.Infinite);
    }

    #endregion

    #region Selection Handlers

    private async void OnMealSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MealSummaryMobile meal) return;
        MealsCollection.SelectedItem = null;

        var request = new CreateMealPlanEntryRequest
        {
            MealId = meal.Id,
            MealTypeId = MealTypeId,
            DayOfWeek = DayOfWeek
        };

        // Check for existing batch source for the same meal
        var existingBatch = _currentPlan?.Entries
            .FirstOrDefault(entry => entry.MealId == meal.Id && entry.IsBatchSource);
        if (existingBatch != null)
        {
            var dayName = GetDayName(existingBatch.DayOfWeek);
            var link = await DisplayAlert("Link to Batch?",
                $"This meal has a batch source on {dayName}. Link to it?",
                "Yes, link", "No, standalone");
            if (link)
                request.BatchSourceEntryId = existingBatch.Id;
        }

        var result = await _apiClient.AddMealPlanEntryAsync(PlanId, request, Version);
        if (result.Success)
        {
            await Navigation.PopAsync();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to add meal", "OK");
        }
    }

    private async void OnRecipeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not RecipeSummary recipe) return;
        RecipesCollection.SelectedItem = null;

        // Auto-create a meal from this recipe, then add it to the plan
        var createMealRequest = new CreateMealMobileRequest
        {
            Name = recipe.Name,
            Items = new List<CreateMealItemMobileRequest>
            {
                new()
                {
                    ItemType = 0, // Recipe
                    RecipeId = recipe.Id,
                    SortOrder = 0
                }
            }
        };

        var createResult = await _apiClient.CreateMealAsync(createMealRequest);
        if (!createResult.Success || createResult.Data == null)
        {
            await DisplayAlert("Error", createResult.ErrorMessage ?? "Failed to create meal from recipe", "OK");
            return;
        }

        var entryRequest = new CreateMealPlanEntryRequest
        {
            MealId = createResult.Data.Id,
            MealTypeId = MealTypeId,
            DayOfWeek = DayOfWeek
        };

        var addResult = await _apiClient.AddMealPlanEntryAsync(PlanId, entryRequest, Version);
        if (addResult.Success)
        {
            await Navigation.PopAsync();
        }
        else
        {
            await DisplayAlert("Error", addResult.ErrorMessage ?? "Failed to add meal to plan", "OK");
        }
    }

    #endregion

    private async void OnNewMealTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(MealEditPage));
    }

    private async void OnSaveNoteTapped(object? sender, EventArgs e)
    {
        var note = NoteEntry.Text;
        if (string.IsNullOrWhiteSpace(note)) return;

        var request = new CreateMealPlanEntryRequest
        {
            InlineNote = note,
            MealTypeId = MealTypeId,
            DayOfWeek = DayOfWeek
        };

        var result = await _apiClient.AddMealPlanEntryAsync(PlanId, request, Version);
        if (result.Success)
        {
            await Navigation.PopAsync();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to add note", "OK");
        }
    }

    private static string GetDayName(int dayOfWeek) => dayOfWeek switch
    {
        0 => "Sunday",
        1 => "Monday",
        2 => "Tuesday",
        3 => "Wednesday",
        4 => "Thursday",
        5 => "Friday",
        6 => "Saturday",
        _ => ""
    };
}
