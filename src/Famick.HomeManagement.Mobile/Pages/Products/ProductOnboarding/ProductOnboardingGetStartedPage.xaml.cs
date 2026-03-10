using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;

public partial class ProductOnboardingGetStartedPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ProductOnboardingAnswersDto _answers = new();

    public ProductOnboardingGetStartedPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    public void SetAnswers(ProductOnboardingAnswersDto answers)
    {
        _answers = answers;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await SaveOnboardingAsync();
    }

    private async Task SaveOnboardingAsync()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;

        try
        {
            var request = new ProductOnboardingCompleteRequest
            {
                Answers = _answers,
                SelectedMasterProductIds = new List<Guid>()
            };

            var result = await _apiClient.CompleteProductOnboardingAsync(request);
            if (!result.Success)
            {
                ErrorLabel.Text = result.ErrorMessage ?? "Failed to save preferences.";
                ErrorLabel.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Error: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private async void OnTakeInventoryClicked(object? sender, EventArgs e)
    {
        await Navigation.PopToRootAsync();
        await Shell.Current.GoToAsync("//InventorySessionPage");
    }

    private async void OnLaterClicked(object? sender, EventArgs e)
    {
        await Navigation.PopToRootAsync();
    }
}
