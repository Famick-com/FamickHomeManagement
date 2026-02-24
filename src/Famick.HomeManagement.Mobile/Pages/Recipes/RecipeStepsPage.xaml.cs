using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Recipes;

[QueryProperty(nameof(RecipeId), "RecipeId")]
public partial class RecipeStepsPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private RecipeDetail? _recipe;
    private List<QuantityUnitSummary> _quantityUnits = new();
    private readonly HashSet<Guid> _expandedStepIds = new();

    public string RecipeId { get; set; } = string.Empty;

    public RecipeStepsPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadQuantityUnitsAsync();
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

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    _recipe = result.Data;
                    TitleLabel.Text = _recipe.Name;
                    RenderSteps();
                    ShowContent();
                }
                else
                {
                    ShowError(result.ErrorMessage ?? "Failed to load recipe");
                }
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError($"Connection error: {ex.Message}"));
        }
    }

    private async Task LoadQuantityUnitsAsync()
    {
        var result = await _apiClient.GetQuantityUnitsAsync();
        if (result.Success && result.Data != null)
        {
            _quantityUnits = result.Data;
        }
    }

    #region Steps Management

    private void RenderSteps()
    {
        StepEditorList.Children.Clear();

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
            var isFirst = i == 0;
            var isLast = i == orderedSteps.Count - 1;
            var isExpanded = _expandedStepIds.Contains(step.Id);
            StepEditorList.Children.Add(CreateStepEditorCard(step, i + 1, isFirst, isLast, isExpanded));
        }
    }

    private View CreateStepEditorCard(RecipeStep step, int stepNumber, bool isFirst, bool isLast, bool isExpanded)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var card = new Border
        {
            Padding = new Thickness(12),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Stroke = Colors.Transparent,
            BackgroundColor = isDark ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F5F5F5")
        };

        // Header row — always visible via Expander.Header
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(0, 0, 0, 4)
        };

        var headerText = !string.IsNullOrEmpty(step.Title) ? $"Step {stepNumber}: {step.Title}" : $"Step {stepNumber}";
        var chevron = new Label
        {
            Text = isExpanded ? "\u25BE" : "\u25B8",
            FontSize = 14,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center
        };
        var headerLabel = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                chevron,
                new Label { Text = headerText, FontSize = 15, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }
            }
        };
        Grid.SetColumn(headerLabel, 0);
        headerGrid.Children.Add(headerLabel);

        // Move up button
        if (!isFirst)
        {
            var upBtn = new Button
            {
                Text = "\u25B2", FontSize = 12, Padding = new Thickness(6, 2),
                WidthRequest = 32, HeightRequest = 28,
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.Gray
            };
            var stepIdUp = step.Id;
            upBtn.Clicked += async (_, _) => await MoveStepUpAsync(stepIdUp);
            Grid.SetColumn(upBtn, 1);
            headerGrid.Children.Add(upBtn);
        }

        // Move down button
        if (!isLast)
        {
            var downBtn = new Button
            {
                Text = "\u25BC", FontSize = 12, Padding = new Thickness(6, 2),
                WidthRequest = 32, HeightRequest = 28,
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.Gray
            };
            var stepIdDown = step.Id;
            downBtn.Clicked += async (_, _) => await MoveStepDownAsync(stepIdDown);
            Grid.SetColumn(downBtn, 2);
            headerGrid.Children.Add(downBtn);
        }

        // Delete button
        var deleteBtn = new Button
        {
            Text = "\u2715", FontSize = 14, Padding = new Thickness(6, 2),
            WidthRequest = 32, HeightRequest = 28,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#D32F2F")
        };
        var stepIdDel = step.Id;
        deleteBtn.Clicked += async (_, _) => await DeleteStepAsync(stepIdDel);
        Grid.SetColumn(deleteBtn, 3);
        headerGrid.Children.Add(deleteBtn);

        // Expandable content
        var expandContent = new VerticalStackLayout { Spacing = 8 };

        // Title entry
        var titleEntry = new Entry { Text = step.Title, Placeholder = "Step title (optional)" };
        expandContent.Children.Add(titleEntry);

        // Instructions editor
        expandContent.Children.Add(new Label { Text = "Instructions *", FontSize = 13, TextColor = Colors.Gray });
        var instructionsEditor = new Editor
        {
            Text = step.Instructions,
            Placeholder = "Step instructions...",
            HeightRequest = 100,
            AutoSize = EditorAutoSizeOption.TextChanges
        };
        expandContent.Children.Add(instructionsEditor);

        // Media section (image + video)
        expandContent.Children.Add(new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = isDark ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 4)
        });
        expandContent.Children.Add(new Label { Text = "Media", FontSize = 14, FontAttributes = FontAttributes.Bold });

        // Step image — use pre-loaded source for auth'd URLs, or external URL directly
        var stepImgSource = step.LoadedImageSource
            ?? (!string.IsNullOrEmpty(step.ImageExternalUrl) ? ImageSource.FromUri(new Uri(step.ImageExternalUrl)) : null);
        if (stepImgSource != null)
        {
            var stepImage = new Border
            {
                HeightRequest = 120,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Start,
                Content = new Image
                {
                    Source = stepImgSource,
                    HeightRequest = 120,
                    Aspect = Aspect.AspectFill
                }
            };
            expandContent.Children.Add(stepImage);
        }
        else if (!string.IsNullOrEmpty(step.DisplayImageUrl))
        {
            // Load through authenticated client and update in-place
            var img = new Image { HeightRequest = 120, Aspect = Aspect.AspectFill };
            var stepImage = new Border
            {
                HeightRequest = 120,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Start,
                Content = img
            };
            expandContent.Children.Add(stepImage);

            var url = step.DisplayImageUrl;
            _ = Task.Run(async () =>
            {
                var source = await _apiClient.LoadImageAsync(url);
                if (source != null)
                {
                    step.LoadedImageSource = source;
                    MainThread.BeginInvokeOnMainThread(() => img.Source = source);
                }
            });
        }

        var addImageBtn = new Button
        {
            Text = string.IsNullOrEmpty(step.DisplayImageUrl) ? "Add Image" : "Change Image",
            FontSize = 12,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            BackgroundColor = isDark ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0"),
            TextColor = isDark ? Colors.White : Colors.Black,
            HorizontalOptions = LayoutOptions.Start
        };
        var stepIdImg = step.Id;
        addImageBtn.Clicked += async (_, _) => await OnStepImageClickedAsync(stepIdImg);
        expandContent.Children.Add(addImageBtn);

        // Video URL
        expandContent.Children.Add(new Label { Text = "Video URL", FontSize = 13, TextColor = Colors.Gray });
        var videoEntry = new Entry { Text = step.VideoUrl, Placeholder = "Video URL (optional)" };
        expandContent.Children.Add(videoEntry);

        // Save step button
        var saveStepBtn = new Button
        {
            Text = "Save Step",
            FontSize = 13,
            HeightRequest = 34,
            Padding = new Thickness(12, 0),
            BackgroundColor = Color.FromArgb("#1976D2"),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Start
        };
        var stepIdSave = step.Id;
        saveStepBtn.Clicked += async (_, _) =>
        {
            await UpdateStepAsync(stepIdSave, new UpdateRecipeStepRequest
            {
                Title = titleEntry.Text?.Trim(),
                Instructions = instructionsEditor.Text?.Trim() ?? string.Empty,
                VideoUrl = videoEntry.Text?.Trim()
            });
        };
        expandContent.Children.Add(saveStepBtn);

        // Ingredients section
        expandContent.Children.Add(new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = isDark ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 4)
        });

        var ingredientHeaderGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        ingredientHeaderGrid.Children.Add(new Label
        {
            Text = "Ingredients",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        });

        var addIngBtn = new Button
        {
            Text = "+ Add",
            FontSize = 12,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            BackgroundColor = isDark ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0"),
            TextColor = isDark ? Colors.White : Colors.Black
        };
        var stepIdIng = step.Id;
        addIngBtn.Clicked += async (_, _) => await ShowAddIngredientDialogAsync(stepIdIng);
        Grid.SetColumn(addIngBtn, 1);
        ingredientHeaderGrid.Children.Add(addIngBtn);
        expandContent.Children.Add(ingredientHeaderGrid);

        // Ingredient list
        var ingredientList = new VerticalStackLayout { Spacing = 4 };
        foreach (var ingredient in step.Ingredients.OrderBy(ing => ing.SortOrder))
        {
            var ingGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            ingGrid.Children.Add(new Label
            {
                Text = $"\u2022 {ingredient.DisplayText}",
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center
            });

            var removeIngBtn = new Button
            {
                Text = "\u2715",
                FontSize = 12,
                Padding = new Thickness(4, 0),
                WidthRequest = 28,
                HeightRequest = 24,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#D32F2F")
            };
            var ingredientId = ingredient.Id;
            var ingredientStepId = step.Id;
            removeIngBtn.Clicked += async (_, _) => await DeleteIngredientAsync(ingredientStepId, ingredientId);
            Grid.SetColumn(removeIngBtn, 1);
            ingGrid.Children.Add(removeIngBtn);

            ingredientList.Children.Add(ingGrid);
        }

        if (step.Ingredients.Count == 0)
        {
            ingredientList.Children.Add(new Label
            {
                Text = "No ingredients yet.",
                FontSize = 12,
                TextColor = Colors.Gray
            });
        }
        expandContent.Children.Add(ingredientList);

        // CommunityToolkit.Maui Expander
        var expander = new Expander
        {
            Header = headerGrid,
            Content = expandContent,
            IsExpanded = isExpanded
        };

        var capturedStepId = step.Id;
        expander.ExpandedChanged += (_, e) =>
        {
            if (e.IsExpanded)
                _expandedStepIds.Add(capturedStepId);
            else
                _expandedStepIds.Remove(capturedStepId);
            chevron.Text = e.IsExpanded ? "\u25BE" : "\u25B8";
        };

        card.Content = expander;
        return card;
    }

    private async void OnAddStepClicked(object? sender, EventArgs e)
    {
        if (_recipe == null) return;

        var request = new CreateRecipeStepRequest
        {
            Instructions = "New step"
        };

        var result = await _apiClient.CreateRecipeStepAsync(_recipe.Id, request);
        if (result.Success && result.Data != null)
        {
            _recipe.Steps.Add(result.Data);
            _expandedStepIds.Add(result.Data.Id);
            RenderSteps();
        }
        else
        {
            await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to add step", "OK");
        }
    }

    private async Task UpdateStepAsync(Guid stepId, UpdateRecipeStepRequest request)
    {
        if (_recipe == null) return;

        var result = await _apiClient.UpdateRecipeStepAsync(_recipe.Id, stepId, request);
        if (result.Success && result.Data != null)
        {
            var idx = _recipe.Steps.FindIndex(s => s.Id == stepId);
            if (idx >= 0) _recipe.Steps[idx] = result.Data;
            _expandedStepIds.Add(stepId);
            RenderSteps();
        }
        else
        {
            await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to update step", "OK");
        }
    }

    private async Task DeleteStepAsync(Guid stepId)
    {
        if (_recipe == null) return;

        var confirm = await DisplayAlertAsync("Delete Step", "Are you sure you want to delete this step?", "Delete", "Cancel");
        if (!confirm) return;

        var result = await _apiClient.DeleteRecipeStepAsync(_recipe.Id, stepId);
        if (result.Success)
        {
            _recipe.Steps.RemoveAll(s => s.Id == stepId);
            _expandedStepIds.Remove(stepId);
            RenderSteps();
        }
        else
        {
            await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to delete step", "OK");
        }
    }

    private async Task MoveStepUpAsync(Guid stepId)
    {
        if (_recipe == null) return;

        var ordered = _recipe.Steps.OrderBy(s => s.StepOrder).ToList();
        var idx = ordered.FindIndex(s => s.Id == stepId);
        if (idx <= 0) return;

        (ordered[idx], ordered[idx - 1]) = (ordered[idx - 1], ordered[idx]);
        var reorderRequest = new ReorderStepsRequest { StepIds = ordered.Select(s => s.Id).ToList() };

        var result = await _apiClient.ReorderRecipeStepsAsync(_recipe.Id, reorderRequest);
        if (result.Success)
        {
            for (int i = 0; i < ordered.Count; i++)
                ordered[i].StepOrder = i + 1;
            _recipe.Steps = ordered;
            RenderSteps();
        }
    }

    private async Task MoveStepDownAsync(Guid stepId)
    {
        if (_recipe == null) return;

        var ordered = _recipe.Steps.OrderBy(s => s.StepOrder).ToList();
        var idx = ordered.FindIndex(s => s.Id == stepId);
        if (idx < 0 || idx >= ordered.Count - 1) return;

        (ordered[idx], ordered[idx + 1]) = (ordered[idx + 1], ordered[idx]);
        var reorderRequest = new ReorderStepsRequest { StepIds = ordered.Select(s => s.Id).ToList() };

        var result = await _apiClient.ReorderRecipeStepsAsync(_recipe.Id, reorderRequest);
        if (result.Success)
        {
            for (int i = 0; i < ordered.Count; i++)
                ordered[i].StepOrder = i + 1;
            _recipe.Steps = ordered;
            RenderSteps();
        }
    }

    private async Task OnStepImageClickedAsync(Guid stepId)
    {
        if (_recipe == null) return;

        var action = await DisplayActionSheetAsync("Step Image", "Cancel", null, "Take Photo", "Choose from Library");
        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        try
        {
            FileResult? photo = null;
            if (action == "Take Photo")
            {
                if (MediaPicker.Default.IsCaptureSupported)
                    photo = await MediaPicker.Default.CapturePhotoAsync();
                else
                    await DisplayAlertAsync("Error", "Camera not supported on this device.", "OK");
            }
            else
            {
                photo = await MediaPicker.Default.PickPhotoAsync();
            }

            if (photo == null) return;

            using var stream = await photo.OpenReadAsync();
            var result = await _apiClient.UploadStepImageAsync(_recipe.Id, stepId, stream, photo.FileName);
            if (result.Success && result.Data != null)
            {
                var idx = _recipe.Steps.FindIndex(s => s.Id == stepId);
                if (idx >= 0) _recipe.Steps[idx] = result.Data;
                _expandedStepIds.Add(stepId);
                RenderSteps();
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to upload step image", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to add photo: {ex.Message}", "OK");
        }
    }

    #endregion

    #region Ingredients Management

    private async Task ShowAddIngredientDialogAsync(Guid stepId)
    {
        if (_recipe == null) return;

        _expandedStepIds.Add(stepId);
        await Shell.Current.GoToAsync(nameof(AddIngredientPage), new Dictionary<string, object>
        {
            { "RecipeId", _recipe.Id.ToString() },
            { "StepId", stepId.ToString() }
        });
    }

    private async Task DeleteIngredientAsync(Guid stepId, Guid ingredientId)
    {
        if (_recipe == null) return;

        var result = await _apiClient.DeleteRecipeIngredientAsync(_recipe.Id, stepId, ingredientId);
        if (result.Success)
        {
            var step = _recipe.Steps.FirstOrDefault(s => s.Id == stepId);
            step?.Ingredients.RemoveAll(i => i.Id == ingredientId);
            _expandedStepIds.Add(stepId);
            RenderSteps();
        }
        else
        {
            await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to remove ingredient", "OK");
        }
    }

    #endregion

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        if (_recipe == null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        // Navigate to the recipe detail page, removing the wizard pages from the stack
        await Shell.Current.GoToAsync($"../../{nameof(RecipeDetailPage)}", new Dictionary<string, object>
        {
            { "RecipeId", _recipe.Id.ToString() }
        });
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

    private void ShowContent()
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
