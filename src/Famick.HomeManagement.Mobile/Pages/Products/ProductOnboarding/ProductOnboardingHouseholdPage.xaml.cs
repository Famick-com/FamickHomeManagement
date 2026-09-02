using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;

public partial class ProductOnboardingHouseholdPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    public ProductOnboardingHouseholdPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        var answers = BuildAnswers();

        var services = Application.Current?.Handler?.MauiContext?.Services;
        if (services == null) return;

        // Allergies and dietary restrictions are health information about the household,
        // and collecting them is switched off server-side. Skipping the step means the app
        // does not ask for it at all — the server would discard it anyway, but a question
        // asked is a question collected as far as the privacy manifest is concerned.
        //
        // Fails closed: if the server cannot be reached, the step is skipped rather than
        // shown, because the answer we could not get is the one that says "do not ask".
        if (!await DietaryProfilesEnabledAsync(services))
        {
            var skipTo = services.GetRequiredService<ProductOnboardingGetStartedPage>();
            skipTo.SetAnswers(answers);
            await Navigation.PushAsync(skipTo);
            return;
        }

        var nextPage = services.GetRequiredService<ProductOnboardingDietaryPage>();
        nextPage.SetAnswers(answers);
        await Navigation.PushAsync(nextPage);
    }

    private static async Task<bool> DietaryProfilesEnabledAsync(IServiceProvider services)
    {
        try
        {
            var oauth = services.GetService<OAuthService>();
            if (oauth == null) return false;

            var config = await oauth.GetAuthConfigurationAsync();
            return config.Success && config.Data?.FeatureFlags?.DietaryProfilesEnabled == true;
        }
        catch
        {
            return false;
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private ProductOnboardingAnswersDto BuildAnswers()
    {
        return new ProductOnboardingAnswersDto
        {
            HasBaby = HasBabySwitch.IsToggled,
            HasPets = HasPetsSwitch.IsToggled,
            TrackHouseholdSupplies = TrackHouseholdSwitch.IsToggled,
            TrackPersonalCare = TrackPersonalCareSwitch.IsToggled,
            TrackPharmacy = TrackPharmacySwitch.IsToggled
        };
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
