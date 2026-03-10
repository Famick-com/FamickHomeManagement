using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;

public partial class ProductOnboardingDietaryPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ProductOnboardingAnswersDto _answers = new();

    private readonly List<string> _dietaryOptions = new()
    {
        "Vegetarian", "Vegan", "Pescatarian", "GlutenFree", "DairyFree",
        "NutFree", "Kosher", "Halal", "LowSodium", "LowCarb", "Keto", "Paleo"
    };

    private readonly List<string> _allergenOptions = new()
    {
        "Milk", "Eggs", "Fish", "Shellfish", "TreeNuts", "Peanuts",
        "Wheat", "Soybeans", "Sesame", "Gluten", "Corn", "Sulfites",
        "Mustard", "Celery", "Lupin", "Mollusks"
    };

    private readonly Dictionary<string, string> _dietaryDisplayNames = new()
    {
        { "Vegetarian", "Vegetarian" }, { "Vegan", "Vegan" },
        { "Pescatarian", "Pescatarian" }, { "GlutenFree", "Gluten-Free" },
        { "DairyFree", "Dairy-Free" }, { "NutFree", "Nut-Free" },
        { "Kosher", "Kosher" }, { "Halal", "Halal" },
        { "LowSodium", "Low Sodium" }, { "LowCarb", "Low Carb" },
        { "Keto", "Keto" }, { "Paleo", "Paleo" }
    };

    private readonly Dictionary<string, string> _allergenDisplayNames = new()
    {
        { "Milk", "Milk" }, { "Eggs", "Eggs" }, { "Fish", "Fish" },
        { "Shellfish", "Shellfish" }, { "TreeNuts", "Tree Nuts" },
        { "Peanuts", "Peanuts" }, { "Wheat", "Wheat" },
        { "Soybeans", "Soybeans" }, { "Sesame", "Sesame" },
        { "Gluten", "Gluten" }, { "Corn", "Corn" },
        { "Sulfites", "Sulfites" }, { "Mustard", "Mustard" },
        { "Celery", "Celery" }, { "Lupin", "Lupin" },
        { "Mollusks", "Mollusks" }
    };

    private readonly HashSet<string> _selectedDietary = new();
    private readonly HashSet<string> _selectedAllergens = new();

    public ProductOnboardingDietaryPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    public void SetAnswers(ProductOnboardingAnswersDto answers)
    {
        _answers = answers;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BuildChips();
    }

    private void BuildChips()
    {
        DietaryChipsLayout.Children.Clear();
        foreach (var option in _dietaryOptions)
        {
            var displayName = _dietaryDisplayNames.GetValueOrDefault(option, option);
            var chip = CreateChip(displayName, option, _selectedDietary);
            DietaryChipsLayout.Children.Add(chip);
        }

        AllergenChipsLayout.Children.Clear();
        foreach (var option in _allergenOptions)
        {
            var displayName = _allergenDisplayNames.GetValueOrDefault(option, option);
            var chip = CreateChip(displayName, option, _selectedAllergens);
            AllergenChipsLayout.Children.Add(chip);
        }
    }

    private Border CreateChip(string displayName, string value, HashSet<string> selectedSet)
    {
        var isSelected = selectedSet.Contains(value);

        var label = new Label
        {
            Text = displayName,
            FontSize = 13,
            VerticalOptions = LayoutOptions.Center,
            TextColor = isSelected
                ? Colors.White
                : Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#CCCCCC") : Color.FromArgb("#333333")
        };

        var chip = new Border
        {
            Padding = new Thickness(14, 8),
            Margin = new Thickness(0, 0, 8, 8),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
            Stroke = isSelected ? Color.FromArgb("#1976D2") : Color.FromArgb("#BDBDBD"),
            BackgroundColor = isSelected ? Color.FromArgb("#1976D2") : Colors.Transparent,
            Content = label
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (_, _) =>
        {
            if (selectedSet.Contains(value))
                selectedSet.Remove(value);
            else
                selectedSet.Add(value);

            BuildChips();
        };
        chip.GestureRecognizers.Add(tapGesture);

        return chip;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        _answers.DietaryPreferences = _selectedDietary.ToList();
        _answers.Allergens = _selectedAllergens.ToList();

        var services = Application.Current?.Handler?.MauiContext?.Services;
        var nextPage = services?.GetRequiredService<ProductOnboardingCookingStylePage>();
        if (nextPage != null)
        {
            nextPage.SetAnswers(_answers);
            await Navigation.PushAsync(nextPage);
        }
    }

    private void SetLoading(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = isLoading;
            LoadingIndicator.IsRunning = isLoading;
        });
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
        });
    }

    private void HideError()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorLabel.IsVisible = false;
        });
    }
}
