using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Recipes;

[QueryProperty(nameof(RecipeId), "RecipeId")]
public partial class RecipeDetailPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private RecipeDetail? _recipe;
    private int _scaledServings;

    public string RecipeId { get; set; } = string.Empty;

    public RecipeDetailPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRecipeAsync().ConfigureAwait(false);
    }

    private async Task LoadRecipeAsync()
    {
        if (!Guid.TryParse(RecipeId, out var id))
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError("Invalid recipe ID"));
            return;
        }

        MainThread.BeginInvokeOnMainThread(ShowLoading);

        try
        {
            var result = await _apiClient.GetRecipeAsync(id);

            if (result.Success && result.Data != null)
            {
                _recipe = result.Data;
                _scaledServings = _recipe.Servings;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RenderRecipe();
                    ShowContentView();
                });
                await LoadImagesAsync();
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowError(result.ErrorMessage ?? "Failed to load recipe"));
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError($"Connection error: {ex.Message}"));
        }
    }

    private void RenderRecipe()
    {
        if (_recipe == null) return;

        TitleLabel.Text = _recipe.Name;
        RecipeNameLabel.Text = _recipe.Name;
        UpdateServingsDisplay();
        StepCountLabel.Text = _recipe.Steps.Count.ToString();

        // Primary image (loaded asynchronously via LoadImagesAsync)
        ImageContainer.IsVisible = !string.IsNullOrEmpty(_recipe.PrimaryImageUrl);

        // Source
        if (!string.IsNullOrEmpty(_recipe.Source))
        {
            SourceLabel.Text = _recipe.Source;
            SourceContainer.IsVisible = true;
        }

        // Attribution
        if (!string.IsNullOrEmpty(_recipe.Attribution))
        {
            AttributionLabel.Text = _recipe.Attribution;
            AttributionContainer.IsVisible = true;
        }

        // Steps
        RenderSteps();

        // Nested recipes
        if (_recipe.NestedRecipes.Count > 0)
        {
            RenderNestedRecipes();
            NestedSection.IsVisible = true;
            NestedDivider.IsVisible = true;
        }

        // Notes
        if (!string.IsNullOrEmpty(_recipe.Notes))
        {
            NotesLabel.Text = _recipe.Notes;
            NotesSection.IsVisible = true;
            NotesDivider.IsVisible = true;
        }
    }

    private async Task LoadImagesAsync()
    {
        if (_recipe == null) return;

        // Load primary image
        var primaryUrl = _recipe.PrimaryImageUrl;
        if (!string.IsNullOrEmpty(primaryUrl))
        {
            var imageSource = await _apiClient.LoadImageAsync(primaryUrl);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (imageSource != null)
                {
                    PrimaryImage.Source = imageSource;
                    ImageContainer.IsVisible = true;
                }
                else
                {
                    ImageContainer.IsVisible = false;
                }
            });
        }

        // Load step images
        if (_recipe.Steps.Count > 0)
        {
            foreach (var step in _recipe.Steps)
            {
                if (string.IsNullOrEmpty(step.DisplayImageUrl) || !string.IsNullOrEmpty(step.ImageExternalUrl))
                    continue;

                var stepImage = await _apiClient.LoadImageAsync(step.DisplayImageUrl);
                if (stepImage != null)
                {
                    // Store loaded image for re-render
                    step.LoadedImageSource = stepImage;
                }
            }
            MainThread.BeginInvokeOnMainThread(RenderSteps);
        }
    }

    private void UpdateServingsDisplay()
    {
        if (_recipe == null) return;
        ServingsLabel.Text = _scaledServings.ToString();

        var isScaled = _scaledServings != _recipe.Servings;
        OriginalServingsLabel.IsVisible = isScaled;
        if (isScaled)
            OriginalServingsLabel.Text = $"(original: {_recipe.Servings})";
    }

    private decimal GetScaleFactor()
    {
        if (_recipe == null || _recipe.Servings <= 0) return 1m;
        return (decimal)_scaledServings / _recipe.Servings;
    }

    private void OnDecrementServings(object? sender, EventArgs e)
    {
        if (_scaledServings <= 1) return;
        _scaledServings--;
        UpdateServingsDisplay();
        RenderSteps();
    }

    private void OnIncrementServings(object? sender, EventArgs e)
    {
        _scaledServings++;
        UpdateServingsDisplay();
        RenderSteps();
    }

    private void RenderSteps()
    {
        StepsList.Children.Clear();

        if (_recipe == null || _recipe.Steps.Count == 0)
        {
            NoStepsLabel.IsVisible = true;
            return;
        }

        NoStepsLabel.IsVisible = false;
        var orderedSteps = _recipe.Steps.OrderBy(s => s.StepOrder).ToList();

        for (int i = 0; i < orderedSteps.Count; i++)
        {
            var step = orderedSteps[i];
            var stepCard = CreateStepCard(step, i + 1);
            StepsList.Children.Add(stepCard);
        }
    }

    private View CreateStepCard(RecipeStep step, int stepNumber)
    {
        var card = new Border
        {
            Padding = new Thickness(12),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Stroke = Colors.Transparent,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F5F5F5")
        };

        var layout = new VerticalStackLayout { Spacing = 6 };

        // Step header
        var headerText = !string.IsNullOrEmpty(step.Title)
            ? $"Step {stepNumber}: {step.Title}"
            : $"Step {stepNumber}";
        layout.Children.Add(new Label
        {
            Text = headerText,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        });

        // Step image — use pre-loaded source for auth'd URLs, or external URL directly
        var stepImageSource = step.LoadedImageSource
            ?? (!string.IsNullOrEmpty(step.ImageExternalUrl) ? ImageSource.FromUri(new Uri(step.ImageExternalUrl)) : null);
        if (stepImageSource != null)
        {
            var imgBorder = new Border
            {
                HeightRequest = 150,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                Content = new Image
                {
                    Source = stepImageSource,
                    Aspect = Aspect.AspectFill,
                    HeightRequest = 150
                }
            };
            layout.Children.Add(imgBorder);
        }

        // Instructions
        if (!string.IsNullOrEmpty(step.Instructions))
        {
            layout.Children.Add(new Label
            {
                Text = step.Instructions,
                FontSize = 14,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#BDBDBD") : Color.FromArgb("#424242")
            });
        }

        // Ingredients
        if (step.Ingredients.Count > 0)
        {
            layout.Children.Add(new Label
            {
                Text = "Ingredients:",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 4, 0, 0),
                TextColor = Colors.Gray
            });

            var scaleFactor = GetScaleFactor();
            var isScaled = _recipe != null && _scaledServings != _recipe.Servings;

            foreach (var ingredient in step.Ingredients.OrderBy(ing => ing.SortOrder))
            {
                var text = FormatIngredient(ingredient, scaleFactor, isScaled);
                layout.Children.Add(new Label
                {
                    Text = $"  \u2022 {text}",
                    FontSize = 13,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#BDBDBD") : Color.FromArgb("#555555")
                });
            }
        }

        // Video URL
        if (!string.IsNullOrEmpty(step.VideoUrl))
        {
            var videoLabel = new Label
            {
                Text = "Watch Video",
                FontSize = 13,
                TextColor = Color.FromArgb("#1976D2"),
                TextDecorations = TextDecorations.Underline
            };
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (_, _) =>
            {
                try { await Launcher.OpenAsync(new Uri(step.VideoUrl)); }
                catch { /* ignore invalid URLs */ }
            };
            videoLabel.GestureRecognizers.Add(tapGesture);
            layout.Children.Add(videoLabel);
        }

        card.Content = layout;
        return card;
    }

    private static string FormatIngredient(RecipeIngredient ingredient, decimal scaleFactor, bool isScaled)
    {
        if (!isScaled || ingredient.Amount <= 0)
            return ingredient.DisplayText;

        var scaledAmount = ingredient.Amount * scaleFactor;
        var parts = new List<string>();
        parts.Add(scaledAmount.ToString("G"));
        if (!string.IsNullOrEmpty(ingredient.QuantityUnitName)) parts.Add(ingredient.QuantityUnitName);
        parts.Add(ingredient.ProductName);
        if (!string.IsNullOrEmpty(ingredient.Note)) parts.Add($"({ingredient.Note})");
        parts.Add($"(was {ingredient.Amount:G})");
        return string.Join(" ", parts);
    }

    private void RenderNestedRecipes()
    {
        NestedList.Children.Clear();
        if (_recipe == null) return;

        foreach (var nested in _recipe.NestedRecipes)
        {
            var tapGesture = new TapGestureRecognizer();
            var nestedId = nested.RecipeId;
            tapGesture.Tapped += async (_, _) =>
            {
                await Shell.Current.GoToAsync(nameof(RecipeDetailPage), new Dictionary<string, object>
                {
                    { "RecipeId", nestedId.ToString() }
                });
            };

            var card = new Border
            {
                Padding = new Thickness(12, 10),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F5F5F5"),
                Content = CreateNestedRecipeGrid(nested.RecipeName)
            };
            card.GestureRecognizers.Add(tapGesture);
            NestedList.Children.Add(card);
        }
    }

    private static Grid CreateNestedRecipeGrid(string recipeName)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        var nameLabel = new Label
        {
            Text = recipeName,
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center
        };
        grid.Children.Add(nameLabel);

        var arrowLabel = new Label
        {
            Text = "\u203A",
            FontSize = 20,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(arrowLabel, 1);
        grid.Children.Add(arrowLabel);

        return grid;
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_recipe == null) return;

        await Shell.Current.GoToAsync(nameof(RecipeEditPage), new Dictionary<string, object>
        {
            { "RecipeId", _recipe.Id.ToString() }
        });
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (_recipe == null) return;

        try
        {
            var result = await _apiClient.GenerateRecipeShareAsync(_recipe.Id);
            if (result.Success && result.Data != null)
            {
                // ShareUrl from API may be relative — ensure it's absolute
                var shareUrl = result.Data.ShareUrl;
                if (!shareUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    shareUrl = $"{_apiClient.BaseUrl}{shareUrl}";

                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = _recipe.Name,
                    Text = $"Check out this recipe: {_recipe.Name}",
                    Uri = shareUrl
                });
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to generate share link", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to share: {ex.Message}", "OK");
        }
    }

    private async void OnAddToShoppingListClicked(object? sender, EventArgs e)
    {
        if (_recipe == null) return;

        AddToListButton.IsEnabled = false;
        try
        {
            // Fetch fulfillment and shopping lists in parallel
            var fulfillmentTask = _apiClient.GetRecipeFulfillmentAsync(_recipe.Id, _scaledServings);
            var listsTask = _apiClient.GetShoppingListsAsync();
            await Task.WhenAll(fulfillmentTask, listsTask);

            var fulfillmentResult = fulfillmentTask.Result;
            var listsResult = listsTask.Result;

            if (!fulfillmentResult.Success || fulfillmentResult.Data == null)
            {
                await DisplayAlertAsync("Error", fulfillmentResult.ErrorMessage ?? "Failed to load ingredients", "OK");
                return;
            }

            if (!listsResult.Success || listsResult.Data == null || listsResult.Data.Count == 0)
            {
                await DisplayAlertAsync("No Lists", "Create a shopping list first.", "OK");
                return;
            }

            // Show fulfillment summary
            var items = fulfillmentResult.Data.Ingredients;
            var missingCount = items.Count(i => !i.IsSufficient);
            var totalCount = items.Count;

            var proceed = await DisplayAlertAsync(
                "Add to Shopping List",
                $"{totalCount} ingredients ({missingCount} missing, {totalCount - missingCount} in stock).\nAdd missing items to a shopping list?",
                "Choose List", "Cancel");

            if (!proceed) return;

            // Pick shopping list
            var lists = listsResult.Data;
            var listNames = lists.Select(l => l.Name).ToArray();
            var selectedName = await DisplayActionSheetAsync("Select Shopping List", "Cancel", null, listNames);

            if (string.IsNullOrEmpty(selectedName) || selectedName == "Cancel") return;

            var selectedList = lists.First(l => l.Name == selectedName);

            var addResult = await _apiClient.AddRecipeToShoppingListAsync(_recipe.Id, new AddToShoppingListRequest
            {
                ShoppingListId = selectedList.Id,
                Servings = _scaledServings != _recipe.Servings ? _scaledServings : null
            });

            if (addResult.Success)
            {
                await DisplayAlertAsync("Success", $"Ingredients added to {selectedName}.", "OK");
            }
            else
            {
                await DisplayAlertAsync("Error", addResult.ErrorMessage ?? "Failed to add to list", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed: {ex.Message}", "OK");
        }
        finally
        {
            AddToListButton.IsEnabled = true;
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadRecipeAsync();
    }

    private void ShowLoading()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;
        ErrorFrame.IsVisible = false;
    }

    private void ShowContentView()
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
