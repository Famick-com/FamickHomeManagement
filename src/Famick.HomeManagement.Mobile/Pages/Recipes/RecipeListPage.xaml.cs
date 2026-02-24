using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Recipes;

public partial class RecipeListPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly TokenStorage _tokenStorage;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;
    private string _currentSortBy = "Name";

    public ObservableCollection<RecipeSummary> Recipes { get; } = new();

    public RecipeListPage(ShoppingApiClient apiClient, TokenStorage tokenStorage)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        BindingContext = this;
        SortPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var token = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {
            var loginPage = Application.Current?.Handler?.MauiContext?.Services.GetService<LoginPage>();
            if (loginPage != null)
            {
                await Navigation.PushModalAsync(new NavigationPage(loginPage)).ConfigureAwait(false);
            }
            return;
        }

        await LoadRecipesAsync().ConfigureAwait(false);
    }

    private async Task LoadRecipesAsync()
    {
        MainThread.BeginInvokeOnMainThread(ShowLoading);

        try
        {
            var result = await _apiClient.GetRecipesAsync(_currentSearchTerm, _currentSortBy);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    Recipes.Clear();
                    foreach (var recipe in result.Data)
                    {
                        Recipes.Add(recipe);
                    }

                    if (Recipes.Count == 0)
                        ShowEmpty();
                    else
                        ShowContent();
                }
                else
                {
                    ShowError(result.ErrorMessage ?? "Failed to load recipes");
                }
            });

            // Load thumbnails in background after list renders
            if (result.Success && result.Data != null)
                _ = LoadThumbnailsAsync(result.Data);
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError($"Connection error: {ex.Message}"));
        }
    }

    private async Task LoadThumbnailsAsync(List<RecipeSummary> recipes)
    {
        foreach (var recipe in recipes)
        {
            if (string.IsNullOrEmpty(recipe.PrimaryImageUrl)) continue;

            var source = await _apiClient.LoadImageAsync(recipe.PrimaryImageUrl);
            if (source != null)
                MainThread.BeginInvokeOnMainThread(() => recipe.ThumbnailSource = source);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new Timer(_ =>
        {
            _currentSearchTerm = e.NewTextValue ?? string.Empty;
            _ = LoadRecipesAsync();
        }, null, 400, Timeout.Infinite);
    }

    private void OnSortChanged(object? sender, EventArgs e)
    {
        _currentSortBy = SortPicker.SelectedIndex switch
        {
            0 => "Name",
            1 => "UpdatedAt",
            2 => "CreatedAt",
            _ => "Name"
        };
        _ = LoadRecipesAsync();
    }

    private async void OnRecipeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not RecipeSummary selected)
            return;

        RecipeCollection.SelectedItem = null;

        await Shell.Current.GoToAsync(nameof(RecipeDetailPage), new Dictionary<string, object>
        {
            { "RecipeId", selected.Id.ToString() }
        });
    }

    private async void OnAddRecipeClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RecipeEditPage), new Dictionary<string, object>
        {
            { "RecipeId", string.Empty }
        });
    }

    private async void OnEditSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: RecipeSummary recipe })
        {
            await Shell.Current.GoToAsync(nameof(RecipeEditPage), new Dictionary<string, object>
            {
                { "RecipeId", recipe.Id.ToString() }
            });
        }
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: RecipeSummary recipe })
        {
            var confirm = await DisplayAlertAsync("Delete Recipe",
                $"Are you sure you want to delete \"{recipe.Name}\"?", "Delete", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.DeleteRecipeAsync(recipe.Id);
            if (result.Success)
            {
                Recipes.Remove(recipe);
                if (Recipes.Count == 0) ShowEmpty();
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to delete recipe", "OK");
            }
        }
    }

    private void OnRefreshClicked(object? sender, EventArgs e)
    {
        _ = LoadRecipesAsync();
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        RetryButton.IsEnabled = false;
        RetryButton.Text = "Retrying...";
        try
        {
            await LoadRecipesAsync();
        }
        finally
        {
            RetryButton.IsEnabled = true;
            RetryButton.Text = "Retry";
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadRecipesAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private void ShowLoading()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        RefreshContainer.IsVisible = false;
        EmptyState.IsVisible = false;
        ErrorFrame.IsVisible = false;
    }

    private void ShowContent()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        RefreshContainer.IsVisible = true;
        EmptyState.IsVisible = false;
        ErrorFrame.IsVisible = false;
    }

    private void ShowEmpty()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        RefreshContainer.IsVisible = false;
        EmptyState.IsVisible = true;
        ErrorFrame.IsVisible = false;
    }

    private void ShowError(string message)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        RefreshContainer.IsVisible = false;
        EmptyState.IsVisible = false;
        ErrorFrame.IsVisible = true;
        ErrorLabel.Text = message;
    }
}
