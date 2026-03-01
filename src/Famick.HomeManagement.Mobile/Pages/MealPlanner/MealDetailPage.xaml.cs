using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.MealPlanner;

[QueryProperty(nameof(MealId), "MealId")]
public partial class MealDetailPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    public Guid MealId { get; set; }

    public MealDetailPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMealAsync();
    }

    private async Task LoadMealAsync()
    {
        LoadingIndicator.IsVisible = true;
        ContentArea.IsVisible = false;

        var result = await _apiClient.GetMealAsync(MealId);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (result.Success && result.Data != null)
            {
                var meal = result.Data;
                MealNameLabel.Text = meal.Name;
                MealNotesLabel.Text = meal.Notes;
                MealNotesLabel.IsVisible = !string.IsNullOrEmpty(meal.Notes);
                FavoriteLabel.IsVisible = meal.IsFavorite;
                Title = meal.Name;

                ItemsList.Children.Clear();
                foreach (var item in meal.Items.OrderBy(i => i.SortOrder))
                {
                    var icon = item.ItemType switch
                    {
                        0 => "📖",  // Recipe
                        1 => "🛒",  // Product
                        2 => "📝",  // Freetext
                        _ => "•"
                    };

                    var label = new Label
                    {
                        FontSize = 15,
                        TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                            ? Colors.White : Colors.Black,
                    };
                    label.FormattedText = new FormattedString();
                    label.FormattedText.Spans.Add(new Span { Text = $"{icon} " });
                    label.FormattedText.Spans.Add(new Span
                    {
                        Text = item.DisplayName,
                        FontAttributes = item.ItemType == 2 ? FontAttributes.Italic : FontAttributes.None
                    });

                    if (item.ItemType == 1 && item.ProductQuantity.HasValue)
                    {
                        label.FormattedText.Spans.Add(new Span
                        {
                            Text = $" ({item.ProductQuantity:0.##} {item.ProductQuantityUnitName})",
                            TextColor = Color.FromArgb("#888888"),
                            FontSize = 13
                        });
                    }

                    ItemsList.Children.Add(label);
                }

                LoadingIndicator.IsVisible = false;
                ContentArea.IsVisible = true;
            }
            else
            {
                LoadingIndicator.IsVisible = false;
            }
        });

        // Load nutrition in background
        _ = LoadNutritionAsync();
    }

    private async Task LoadNutritionAsync()
    {
        try
        {
            var result = await _apiClient.GetMealNutritionAsync(MealId);
            if (result.Success && result.Data != null)
            {
                var nutrition = result.Data;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CaloriesLabel.Text = $"{nutrition.TotalCalories:F0}";
                    ProteinLabel.Text = $"{nutrition.TotalProteinGrams:F1}g";
                    CarbsLabel.Text = $"{nutrition.TotalCarbsGrams:F1}g";
                    FatLabel.Text = $"{nutrition.TotalFatGrams:F1}g";
                    NutritionSection.IsVisible = true;
                });
            }
        }
        catch { /* Nutrition is optional */ }
    }
}
