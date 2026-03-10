using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;

public partial class ProductOnboardingIntroPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    public ProductOnboardingIntroPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    private async void OnGetStartedClicked(object? sender, EventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var nextPage = services?.GetRequiredService<ProductOnboardingHouseholdPage>();
        if (nextPage != null)
        {
            await Navigation.PushAsync(nextPage);
        }
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Skip Grocery Setup",
            "You can always run this later from Settings. Skip for now?",
            "Skip", "Cancel");

        if (!confirm) return;

        try
        {
            var request = new ProductOnboardingCompleteRequest
            {
                Answers = new ProductOnboardingAnswersDto(),
                SelectedMasterProductIds = new List<Guid>()
            };

            await _apiClient.CompleteProductOnboardingAsync(request);
        }
        catch
        {
            // Best-effort skip
        }

        await Navigation.PopToRootAsync();
    }
}
