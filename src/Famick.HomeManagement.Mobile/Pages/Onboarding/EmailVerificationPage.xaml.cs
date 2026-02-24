using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Onboarding;

public partial class EmailVerificationPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly OnboardingService _onboardingService;
    private readonly string _email;
    private readonly string _householdName;

    public EmailVerificationPage(
        ShoppingApiClient apiClient,
        OnboardingService onboardingService,
        string email,
        string householdName)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _onboardingService = onboardingService;
        _email = email;
        _householdName = householdName;

        // Set the title to the household name
        Title = string.IsNullOrEmpty(householdName) ? "Verify Email" : householdName;

        DescriptionLabel.Text = $"We sent a verification link to:\n{MaskEmail(email)}";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Check if we have a token from deep link
        var pendingToken = _onboardingService.GetPendingVerificationToken();
        if (!string.IsNullOrEmpty(pendingToken))
        {
            VerificationCodeEntry.Text = pendingToken;
            // Auto-verify
            OnVerifyClicked(this, EventArgs.Empty);
        }
    }

    private async void OnResendClicked(object? sender, EventArgs e)
    {
        SetLoading(true);
        ShowStatus("Sending email...", null);

        try
        {
            var result = await _apiClient.ResendVerificationEmailAsync(_email);

            if (result.Success)
            {
                ShowStatus("Email sent! Check your inbox.", true);
            }
            else
            {
                ShowStatus(result.ErrorMessage ?? "Failed to resend email.", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", false);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnChangeEmailClicked(object? sender, EventArgs e)
    {
        // Clear pending verification and go back to welcome page
        _onboardingService.ClearPendingVerification();
        await Navigation.PopAsync();
    }

    private async void OnVerifyClicked(object? sender, EventArgs e)
    {
        var token = VerificationCodeEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            ShowStatus("Please paste the verification token from the email", false);
            return;
        }

        SetLoading(true);
        ShowStatus("Verifying...", null);

        try
        {
            var result = await _apiClient.VerifyEmailAsync(token);

            if (result.Success && result.Data != null && result.Data.Verified)
            {
                ShowStatus("Email verified!", true);

                // Navigate to create password page
                await Navigation.PushAsync(new CreatePasswordPage(
                    _apiClient,
                    _onboardingService,
                    Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<ApiSettings>(),
                    Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<TokenStorage>(),
                    Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<OAuthService>(),
                    token,
                    result.Data.Email,
                    result.Data.HouseholdName));
            }
            else
            {
                ShowStatus(result.ErrorMessage ?? "Verification failed. Please try again.", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", false);
        }
        finally
        {
            SetLoading(false);
        }
    }

    /// <summary>
    /// Called from App.xaml.cs when a deep link verification token is received
    /// </summary>
    public void HandleVerificationToken(string token)
    {
        VerificationCodeEntry.Text = token;
        OnVerifyClicked(this, EventArgs.Empty);
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "***@***";

        var localPart = parts[0];
        var domain = parts[1];

        var maskedLocal = localPart.Length <= 1
            ? "*"
            : localPart[0] + new string('*', Math.Min(localPart.Length - 1, 5));

        return $"{maskedLocal}@{domain}";
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        ResendButton.IsEnabled = !isLoading;
        ChangeEmailButton.IsEnabled = !isLoading;
        VerifyButton.IsEnabled = !isLoading;
        VerificationCodeEntry.IsEnabled = !isLoading;
    }

    private void ShowStatus(string message, bool? success)
    {
        StatusLabel.Text = message;
        StatusLabel.IsVisible = true;
        StatusLabel.TextColor = success switch
        {
            true => Colors.Green,
            false => Colors.Red,
            null => Colors.Gray
        };
    }
}
