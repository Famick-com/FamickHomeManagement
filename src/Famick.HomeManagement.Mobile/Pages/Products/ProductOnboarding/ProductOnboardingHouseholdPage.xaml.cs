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
        var nextPage = services?.GetRequiredService<ProductOnboardingDietaryPage>();
        if (nextPage != null)
        {
            nextPage.SetAnswers(answers);
            await Navigation.PushAsync(nextPage);
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
