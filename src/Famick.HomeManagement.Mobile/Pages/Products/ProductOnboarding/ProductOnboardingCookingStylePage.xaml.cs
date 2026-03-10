using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;

public partial class ProductOnboardingCookingStylePage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ProductOnboardingAnswersDto _answers = new();

    private readonly List<(string Value, string DisplayName, string Icon)> _cookingStyles = new()
    {
        ("FreshProduce", "Fresh Produce", "\U0001F966"),
        ("DairyAndEggs", "Dairy & Eggs", "\U0001F95A"),
        ("MeatAndSeafood", "Meat & Seafood", "\U0001F969"),
        ("Baking", "Baking", "\U0001F9C1"),
        ("InternationalFoods", "International Foods", "\U0001F30D"),
        ("FrozenFoods", "Frozen Foods", "\U00002744"),
        ("BreakfastStaples", "Breakfast Staples", "\U0001F95E"),
        ("CannedGoodsAndMealPrep", "Canned & Meal Prep", "\U0001F96B"),
        ("Beverages", "Beverages", "\U0001F964"),
        ("CondimentsAndPantry", "Condiments & Pantry", "\U0001F9C2")
    };

    private readonly HashSet<string> _selectedStyles = new();

    public ProductOnboardingCookingStylePage(ShoppingApiClient apiClient)
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
        BuildGrid();
    }

    private void BuildGrid()
    {
        CookingStyleGrid.Children.Clear();
        CookingStyleGrid.RowDefinitions.Clear();

        var rowCount = (_cookingStyles.Count + 1) / 2;
        for (int i = 0; i < rowCount; i++)
        {
            CookingStyleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (int i = 0; i < _cookingStyles.Count; i++)
        {
            var (value, displayName, icon) = _cookingStyles[i];
            var isSelected = _selectedStyles.Contains(value);
            var row = i / 2;
            var col = i % 2;

            var card = CreateCard(value, displayName, icon, isSelected);
            Grid.SetRow(card, row);
            Grid.SetColumn(card, col);
            CookingStyleGrid.Children.Add(card);
        }

        UpdateSelectAllButton();
    }

    private Border CreateCard(string value, string displayName, string icon, bool isSelected)
    {
        var content = new VerticalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = icon,
                    FontSize = 28,
                    HorizontalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = displayName,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = isSelected
                        ? Colors.White
                        : Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#CCCCCC") : Color.FromArgb("#333333")
                }
            }
        };

        var card = new Border
        {
            Padding = new Thickness(12, 16),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Stroke = isSelected ? Color.FromArgb("#1976D2") : Color.FromArgb("#BDBDBD"),
            BackgroundColor = isSelected
                ? Color.FromArgb("#1976D2")
                : Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#2A2A2A") : Colors.White,
            MinimumHeightRequest = 100,
            Content = content
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (_, _) =>
        {
            if (_selectedStyles.Contains(value))
                _selectedStyles.Remove(value);
            else
                _selectedStyles.Add(value);

            BuildGrid();
        };
        card.GestureRecognizers.Add(tapGesture);

        return card;
    }

    private void UpdateSelectAllButton()
    {
        var allSelected = _selectedStyles.Count == _cookingStyles.Count;
        SelectAllButton.Text = allSelected ? "Deselect All" : "Select All";
    }

    private void OnSelectAllClicked(object? sender, EventArgs e)
    {
        if (_selectedStyles.Count == _cookingStyles.Count)
        {
            _selectedStyles.Clear();
        }
        else
        {
            _selectedStyles.Clear();
            foreach (var (value, _, _) in _cookingStyles)
            {
                _selectedStyles.Add(value);
            }
        }

        BuildGrid();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        _answers.CookingStyles = _selectedStyles.ToList();

        var services = Application.Current?.Handler?.MauiContext?.Services;
        var nextPage = services?.GetRequiredService<ProductOnboardingReviewPage>();
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
