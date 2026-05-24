using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class AcceptTermsPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly TokenStorage _tokenStorage;

    public AcceptTermsPage(
        ShoppingApiClient apiClient,
        TokenStorage tokenStorage)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        if (!ConsentCheckBox.IsChecked)
        {
            ShowError("Please check the box to accept the Terms of Service and Privacy Policy.");
            return;
        }

        SetLoading(true);
        HideError();

        try
        {
            var result = await _apiClient.AcceptTermsAsync();

            if (result.Success && result.Data != null)
            {
                // Store the fresh tokens (without must_accept_terms claim)
                await _tokenStorage.SetTokensAsync(result.Data.AccessToken, result.Data.RefreshToken);
                // Phase 4 chunk 4.G + follow-up — change-detection on the
                // local-server URL. On change, push the confirmation prompt
                // BEFORE TransitionToMainApp; prompt's confirm-handler does
                // the dashboard transition. Pushed via PushAsync on the
                // current nav stack (this page is itself PushAsync'd inside
                // the LoginPage modal), not modally — that races on iOS.
                var serverChange = Services.LocalServerChangeDetector.ObserveLogin(result.Data.LocalServer);
                if (serverChange is not null)
                {
                    await Navigation.PushAsync(new Pages.LocalServerChangePromptPage(
                        _tokenStorage, serverChange.OldUrl, serverChange.NewUrl));
                    return;
                }

                // Dismiss modal if presented modally, then transition to main app
                if (Navigation.ModalStack.Count > 0)
                {
                    await Navigation.PopModalAsync();
                }
                App.TransitionToMainApp();
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Failed to accept terms. Please try again.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Connection error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnTermsTapped(object? sender, TappedEventArgs e)
    {
        await Launcher.OpenAsync(new Uri("https://famick.com/terms"));
    }

    private async void OnPrivacyTapped(object? sender, TappedEventArgs e)
    {
        await Launcher.OpenAsync(new Uri("https://famick.com/privacy"));
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        AcceptButton.IsEnabled = !isLoading;
        ConsentCheckBox.IsEnabled = !isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void HideError()
    {
        ErrorLabel.IsVisible = false;
    }
}
