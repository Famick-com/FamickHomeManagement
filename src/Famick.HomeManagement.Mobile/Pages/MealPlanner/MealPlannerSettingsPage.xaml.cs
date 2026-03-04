using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Famick.HomeManagement.Mobile.Pages.MealPlanner;

public partial class MealPlannerSettingsPage : ContentPage
{
    private static readonly string[] ColorPresets =
    [
        "#FFA726", "#66BB6A", "#42A5F5", "#5C6BC0",
        "#AB47BC", "#EF5350", "#26A69A", "#EC407A"
    ];

    private static readonly string[] ColorNames =
    [
        "Orange", "Green", "Blue", "Indigo",
        "Purple", "Red", "Teal", "Pink"
    ];

    private int? _currentPlanningStyle;
    private List<MealTypeMobile> _mealTypes = new();

    public MealPlannerSettingsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private ShoppingApiClient? GetApiClient()
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        return services?.GetService<ShoppingApiClient>();
    }

    private async Task LoadDataAsync()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        StyleContainer.IsVisible = false;
        MealTypesContainer.IsVisible = false;
        AddMealTypeButton.IsVisible = false;

        try
        {
            var apiClient = GetApiClient();
            if (apiClient == null) return;

            var onboardingResult = await apiClient.GetMealPlannerOnboardingAsync();
            if (onboardingResult.Success && onboardingResult.Data != null)
            {
                _currentPlanningStyle = onboardingResult.Data.PlanningStyle;
            }

            var mealTypesResult = await apiClient.GetMealTypesAsync();
            if (mealTypesResult.Success && mealTypesResult.Data != null)
            {
                _mealTypes = mealTypesResult.Data.OrderBy(mt => mt.SortOrder).ToList();
            }

            BuildPlanningStyleUI();
            BuildMealTypesUI();

            StyleContainer.IsVisible = true;
            MealTypesContainer.IsVisible = true;
            AddMealTypeButton.IsVisible = _mealTypes.Count < 10;
        }
        catch
        {
            // Silently fail
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private void BuildPlanningStyleUI()
    {
        StyleStack.Children.Clear();

        var options = new[] { (Value: 0, Label: "Day by Day"), (Value: 1, Label: "Week at a Glance") };

        for (int i = 0; i < options.Length; i++)
        {
            var option = options[i];
            var isSelected = _currentPlanningStyle == option.Value;

            var row = new Grid
            {
                Padding = new Thickness(16, 14),
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(24))
                }
            };

            var label = new Label
            {
                Text = option.Label,
                FontSize = 15,
                VerticalOptions = LayoutOptions.Center
            };
            label.SetAppThemeColor(Label.TextColorProperty,
                Color.FromArgb("#000000"), Color.FromArgb("#FFFFFF"));
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var radio = new Ellipse
            {
                WidthRequest = 20,
                HeightRequest = 20,
                StrokeThickness = 2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            if (isSelected)
            {
                radio.Stroke = new SolidColorBrush(Color.FromArgb("#1976D2"));
                radio.Fill = new SolidColorBrush(Color.FromArgb("#1976D2"));
            }
            else
            {
                radio.SetAppTheme(Microsoft.Maui.Controls.Shapes.Shape.StrokeProperty,
                    new SolidColorBrush(Color.FromArgb("#CCCCCC")),
                    new SolidColorBrush(Color.FromArgb("#555555")));
                radio.Fill = new SolidColorBrush(Colors.Transparent);
            }

            Grid.SetColumn(radio, 1);
            row.Children.Add(radio);

            var tapGesture = new TapGestureRecognizer();
            var capturedValue = option.Value;
            tapGesture.Tapped += async (_, _) => await OnPlanningStyleTapped(capturedValue);
            row.GestureRecognizers.Add(tapGesture);

            StyleStack.Children.Add(row);

            if (i < options.Length - 1)
            {
                var separator = new BoxView { HeightRequest = 1 };
                separator.SetAppThemeColor(BoxView.BackgroundColorProperty,
                    Color.FromArgb("#E8E8E8"), Color.FromArgb("#3A3A3A"));
                StyleStack.Children.Add(separator);
            }
        }
    }

    private async Task OnPlanningStyleTapped(int style)
    {
        if (_currentPlanningStyle == style) return;

        _currentPlanningStyle = style;
        BuildPlanningStyleUI();

        try
        {
            var apiClient = GetApiClient();
            if (apiClient == null) return;

            await apiClient.SaveMealPlannerOnboardingAsync(new SaveOnboardingMobileRequest
            {
                PlanningStyle = style
            });
        }
        catch
        {
            // Silently fail
        }
    }

    private void BuildMealTypesUI()
    {
        MealTypesStack.Children.Clear();

        for (int i = 0; i < _mealTypes.Count; i++)
        {
            var mealType = _mealTypes[i];

            var row = new Grid
            {
                Padding = new Thickness(16, 12),
                ColumnSpacing = 10,
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(12)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            // Color dot
            var colorDot = new Ellipse
            {
                WidthRequest = 12,
                HeightRequest = 12,
                Fill = new SolidColorBrush(Color.FromArgb(mealType.Color ?? "#999999")),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(colorDot, 0);
            row.Children.Add(colorDot);

            // Name + default badge
            var nameLayout = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
            var nameLabel = new Label
            {
                Text = mealType.Name,
                FontSize = 15,
                VerticalOptions = LayoutOptions.Center
            };
            nameLabel.SetAppThemeColor(Label.TextColorProperty,
                Color.FromArgb("#000000"), Color.FromArgb("#FFFFFF"));
            nameLayout.Children.Add(nameLabel);

            if (mealType.IsDefault)
            {
                var badge = new Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                    Stroke = Colors.Transparent,
                    BackgroundColor = Color.FromArgb("#E3F2FD"),
                    Padding = new Thickness(6, 2),
                    VerticalOptions = LayoutOptions.Center
                };
                badge.Content = new Label
                {
                    Text = "Default",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#1976D2")
                };
                nameLayout.Children.Add(badge);
            }

            Grid.SetColumn(nameLayout, 1);
            row.Children.Add(nameLayout);

            // Edit button
            var editButton = new ImageButton
            {
                Source = new FontImageSource
                {
                    Glyph = "\u270E",
                    Size = 16,
                    Color = Color.FromArgb("#1976D2")
                },
                BackgroundColor = Colors.Transparent,
                WidthRequest = 36,
                HeightRequest = 36,
                VerticalOptions = LayoutOptions.Center
            };
            var capturedMealType = mealType;
            editButton.Clicked += async (_, _) => await OnEditMealTypeTapped(capturedMealType);
            Grid.SetColumn(editButton, 2);
            row.Children.Add(editButton);

            // Delete button (hidden for default types)
            if (!mealType.IsDefault)
            {
                var deleteButton = new ImageButton
                {
                    Source = new FontImageSource
                    {
                        Glyph = "\u2716",
                        Size = 14,
                        Color = Color.FromArgb("#EF5350")
                    },
                    BackgroundColor = Colors.Transparent,
                    WidthRequest = 36,
                    HeightRequest = 36,
                    VerticalOptions = LayoutOptions.Center
                };
                deleteButton.Clicked += async (_, _) => await OnDeleteMealTypeTapped(capturedMealType);
                Grid.SetColumn(deleteButton, 3);
                row.Children.Add(deleteButton);
            }

            MealTypesStack.Children.Add(row);

            if (i < _mealTypes.Count - 1)
            {
                var separator = new BoxView { HeightRequest = 1 };
                separator.SetAppThemeColor(BoxView.BackgroundColorProperty,
                    Color.FromArgb("#E8E8E8"), Color.FromArgb("#3A3A3A"));
                MealTypesStack.Children.Add(separator);
            }
        }
    }

    private async void OnAddMealTypeTapped(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Add Meal Type", "Enter a name for the new meal type:", "Add", "Cancel", "e.g. Brunch");
        if (string.IsNullOrWhiteSpace(name)) return;

        var color = await PickColorAsync("Pick a Color");
        if (color == null) return;

        var nextSortOrder = _mealTypes.Count > 0 ? _mealTypes.Max(mt => mt.SortOrder) + 1 : 0;

        try
        {
            var apiClient = GetApiClient();
            if (apiClient == null) return;

            var result = await apiClient.CreateMealTypeAsync(name.Trim(), nextSortOrder, color);
            if (result.Success)
            {
                await LoadDataAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create meal type.", "OK");
            }
        }
        catch
        {
            await DisplayAlert("Error", "An unexpected error occurred.", "OK");
        }
    }

    private async Task OnEditMealTypeTapped(MealTypeMobile mealType)
    {
        var name = await DisplayPromptAsync("Edit Meal Type", "Update the name:", "Save", "Cancel", initialValue: mealType.Name);
        if (string.IsNullOrWhiteSpace(name)) return;

        var color = await PickColorAsync("Pick a Color", mealType.Color);
        if (color == null) return;

        try
        {
            var apiClient = GetApiClient();
            if (apiClient == null) return;

            var result = await apiClient.UpdateMealTypeAsync(mealType.Id, name.Trim(), mealType.SortOrder, color);
            if (result.Success)
            {
                await LoadDataAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update meal type.", "OK");
            }
        }
        catch
        {
            await DisplayAlert("Error", "An unexpected error occurred.", "OK");
        }
    }

    private async Task OnDeleteMealTypeTapped(MealTypeMobile mealType)
    {
        var confirmed = await DisplayAlert("Delete Meal Type", $"Are you sure you want to delete \"{mealType.Name}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        try
        {
            var apiClient = GetApiClient();
            if (apiClient == null) return;

            var result = await apiClient.DeleteMealTypeAsync(mealType.Id);
            if (result.Success)
            {
                await LoadDataAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete meal type.", "OK");
            }
        }
        catch
        {
            await DisplayAlert("Error", "An unexpected error occurred.", "OK");
        }
    }

    private async Task<string?> PickColorAsync(string title, string? currentColor = null)
    {
        var options = new string[ColorPresets.Length];
        for (int i = 0; i < ColorPresets.Length; i++)
        {
            var marker = ColorPresets[i].Equals(currentColor, StringComparison.OrdinalIgnoreCase) ? " (current)" : "";
            options[i] = $"{ColorNames[i]}{marker}";
        }

        var choice = await DisplayActionSheet(title, "Cancel", null, options);
        if (choice == null || choice == "Cancel") return null;

        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == choice) return ColorPresets[i];
        }

        return currentColor ?? ColorPresets[0];
    }
}
