using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Onboarding;

public partial class WelcomePage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly OnboardingService _onboardingService;

    public WelcomePage(ShoppingApiClient apiClient, OnboardingService onboardingService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _onboardingService = onboardingService;
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        // Validate inputs
        var householdName = HouseholdNameEntry.Text?.Trim();
        var email = EmailEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(householdName))
        {
            ShowError("Please enter a household name");
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Please enter your email");
            return;
        }

        if (!IsValidEmail(email))
        {
            ShowError("Please enter a valid email address");
            return;
        }

        SetLoading(true);
        HideError();

        try
        {
            var result = await _apiClient.StartRegistrationAsync(householdName, email);

            if (result.Success)
            {
                // Save pending verification state
                _onboardingService.SetPendingVerification(email);

                // Navigate to email verification page
                await Navigation.PushAsync(new EmailVerificationPage(
                    _apiClient,
                    _onboardingService,
                    email,
                    householdName));
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Registration failed. Please try again.");
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

    private void OnSignInClicked(object? sender, EventArgs e)
    {
        var services = Application.Current!.Handler!.MauiContext!.Services;
        var apiSettings = services.GetRequiredService<ApiSettings>();

        // Configure for cloud and mark server as configured so LoginPage shows
        apiSettings.ConfigureForCloud(null);
        _onboardingService.MarkOnboardingCompleted();

        // Transition to main app (AppShell) which will show LoginPage via DashboardPage auth check
        App.TransitionToMainApp();
    }

    private async void OnQrCodeClicked(object? sender, EventArgs e)
    {
        // Request camera permission before opening scanner
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Camera Permission Required",
                "Camera access is needed to scan QR codes. Please enable camera access in Settings.",
                "OK");
            return;
        }

        await Navigation.PushAsync(new QrScannerPage(
            Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<ApiSettings>(),
            Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<ShoppingApiClient>()));
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        NextButton.IsEnabled = !isLoading;
        HouseholdNameEntry.IsEnabled = !isLoading;
        EmailEntry.IsEnabled = !isLoading;
        QrCodeButton.IsEnabled = !isLoading;
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
