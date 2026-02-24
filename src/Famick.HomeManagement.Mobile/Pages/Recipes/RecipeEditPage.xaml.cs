using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Recipes;

[QueryProperty(nameof(RecipeId), "RecipeId")]
public partial class RecipeEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private RecipeDetail? _recipe;
    private int _servings = 1;
    private bool _isEditMode;

    public string RecipeId { get; set; } = string.Empty;

    public RecipeEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _isEditMode = !string.IsNullOrEmpty(RecipeId) && Guid.TryParse(RecipeId, out _);

        if (_isEditMode)
        {
            TitleLabel.Text = "Edit Recipe";
            PhotosSection.IsVisible = true;
            await LoadRecipeAsync().ConfigureAwait(false);
        }
        else
        {
            TitleLabel.Text = "New Recipe";
            PhotosSection.IsVisible = false;
        }
    }

    private async Task LoadRecipeAsync()
    {
        if (!Guid.TryParse(RecipeId, out var id)) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            ContentScroll.IsVisible = false;
        });

        try
        {
            var result = await _apiClient.GetRecipeAsync(id);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    _recipe = result.Data;
                    PopulateForm();
                }

                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ContentScroll.IsVisible = true;
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ContentScroll.IsVisible = true;
                _ = DisplayAlertAsync("Error", $"Failed to load recipe: {ex.Message}", "OK");
            });
        }
    }

    private void PopulateForm()
    {
        if (_recipe == null) return;

        NameEntry.Text = _recipe.Name;
        SourceEntry.Text = _recipe.Source;
        _servings = _recipe.Servings;
        ServingsLabel.Text = _servings.ToString();
        AttributionEntry.Text = _recipe.Attribution;
        NotesEditor.Text = _recipe.Notes;
        RenderImages();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        await SaveRecipeAsync(navigateToSteps: false);
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        await SaveRecipeAsync(navigateToSteps: true);
    }

    private async Task SaveRecipeAsync(bool navigateToSteps)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlertAsync("Validation", "Recipe name is required.", "OK");
            return;
        }

        SaveToolbarItem.IsEnabled = false;
        NextButton.IsEnabled = false;

        try
        {
            if (_isEditMode && _recipe != null)
            {
                var request = new UpdateRecipeRequest
                {
                    Name = name,
                    Source = SourceEntry.Text?.Trim(),
                    Servings = _servings,
                    Notes = NotesEditor.Text?.Trim(),
                    Attribution = AttributionEntry.Text?.Trim()
                };

                var result = await _apiClient.UpdateRecipeAsync(_recipe.Id, request);
                if (result.Success && result.Data != null)
                {
                    _recipe = result.Data;

                    if (navigateToSteps)
                    {
                        await Shell.Current.GoToAsync(nameof(RecipeStepsPage), new Dictionary<string, object>
                        {
                            { "RecipeId", _recipe.Id.ToString() }
                        });
                    }
                }
                else
                {
                    await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to update recipe", "OK");
                }
            }
            else
            {
                var request = new CreateRecipeRequest
                {
                    Name = name,
                    Source = SourceEntry.Text?.Trim(),
                    Servings = _servings,
                    Notes = NotesEditor.Text?.Trim(),
                    Attribution = AttributionEntry.Text?.Trim()
                };

                var result = await _apiClient.CreateRecipeAsync(request);
                if (result.Success && result.Data != null)
                {
                    _recipe = result.Data;
                    _isEditMode = true;
                    RecipeId = _recipe.Id.ToString();

                    await Shell.Current.GoToAsync(nameof(RecipeStepsPage), new Dictionary<string, object>
                    {
                        { "RecipeId", _recipe.Id.ToString() }
                    });
                }
                else
                {
                    await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to create recipe", "OK");
                }
            }
        }
        finally
        {
            SaveToolbarItem.IsEnabled = true;
            NextButton.IsEnabled = true;
        }
    }

    private void OnServingsDecrement(object? sender, EventArgs e)
    {
        if (_servings > 1)
        {
            _servings--;
            ServingsLabel.Text = _servings.ToString();
        }
    }

    private void OnServingsIncrement(object? sender, EventArgs e)
    {
        _servings++;
        ServingsLabel.Text = _servings.ToString();
    }

    private void RenderImages()
    {
        ImageGallery.Children.Clear();
        if (_recipe == null) return;

        foreach (var image in _recipe.Images.OrderBy(i => i.SortOrder))
        {
            var img = new Image
            {
                Aspect = Aspect.AspectFill,
                WidthRequest = 80,
                HeightRequest = 80
            };

            // Use pre-loaded source or load asynchronously for auth'd URLs
            if (image.LoadedImageSource != null)
            {
                img.Source = image.LoadedImageSource;
            }
            else if (!string.IsNullOrEmpty(image.ExternalThumbnailUrl) || !string.IsNullOrEmpty(image.ExternalUrl))
            {
                img.Source = image.ThumbnailDisplayUrl;
            }
            else
            {
                // Load through authenticated HttpClient
                var url = image.ThumbnailDisplayUrl;
                _ = Task.Run(async () =>
                {
                    var source = await _apiClient.LoadImageAsync(url);
                    if (source != null)
                    {
                        image.LoadedImageSource = source;
                        MainThread.BeginInvokeOnMainThread(() => img.Source = source);
                    }
                });
            }

            var imgBorder = new Border
            {
                WidthRequest = 80,
                HeightRequest = 80,
                Margin = new Thickness(0, 0, 8, 8),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = image.IsPrimary ? Color.FromArgb("#1976D2") : Colors.Transparent,
                StrokeThickness = image.IsPrimary ? 2 : 0,
                Content = img
            };

            var imageId = image.Id;
            var isPrimary = image.IsPrimary;
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (_, _) =>
            {
                var action = isPrimary
                    ? await DisplayActionSheetAsync("Image Options", "Cancel", "Delete")
                    : await DisplayActionSheetAsync("Image Options", "Cancel", "Delete", "Set as Primary");
                if (action == "Delete")
                {
                    var result = await _apiClient.DeleteRecipeImageAsync(_recipe.Id, imageId);
                    if (result.Success)
                    {
                        _recipe.Images.RemoveAll(i => i.Id == imageId);
                        RenderImages();
                    }
                }
                else if (action == "Set as Primary")
                {
                    var result = await _apiClient.SetPrimaryImageAsync(_recipe.Id, imageId);
                    if (result.Success)
                    {
                        foreach (var img2 in _recipe.Images) img2.IsPrimary = img2.Id == imageId;
                        RenderImages();
                    }
                }
            };
            imgBorder.GestureRecognizers.Add(tapGesture);
            ImageGallery.Children.Add(imgBorder);
        }
    }

    private async void OnAddPhotoClicked(object? sender, EventArgs e)
    {
        if (_recipe == null) return;

        var action = await DisplayActionSheetAsync("Add Photo", "Cancel", null, "Take Photo", "Choose from Library");
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
                var photos = await MediaPicker.Default.PickPhotosAsync();
                photo = photos?.FirstOrDefault();
            }

            if (photo == null) return;

            using var stream = await photo.OpenReadAsync();
            var result = await _apiClient.UploadRecipeImageAsync(_recipe.Id, stream, photo.FileName);
            if (result.Success && result.Data != null)
            {
                _recipe.Images.Add(result.Data);
                RenderImages();
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to upload photo", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to add photo: {ex.Message}", "OK");
        }
    }
}
