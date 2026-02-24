using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Onboarding;

public partial class CreatePasswordPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly OnboardingService _onboardingService;
    private readonly ApiSettings _apiSettings;
    private readonly TokenStorage _tokenStorage;
    private readonly OAuthService _oauthService;
    private readonly string _verificationToken;
    private readonly string _email;
    private readonly string _householdName;
    private readonly List<Button> _oauthButtons = new();
    private bool _requireLegalConsent;
    private bool _consentChecked;

    public CreatePasswordPage(
        ShoppingApiClient apiClient,
        OnboardingService onboardingService,
        ApiSettings apiSettings,
        TokenStorage tokenStorage,
        OAuthService oauthService,
        string verificationToken,
        string email,
        string householdName)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _onboardingService = onboardingService;
        _apiSettings = apiSettings;
        _tokenStorage = tokenStorage;
        _oauthService = oauthService;
        _verificationToken = verificationToken;
        _email = email;
        _householdName = householdName;

        // Set the title to the household name
        Title = string.IsNullOrEmpty(householdName) ? "Create Account" : householdName;

        SubtitleLabel.Text = $"Creating account for {_householdName}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSetupStatusAsync();
        await LoadAuthConfigurationAsync();
    }

    private async Task LoadSetupStatusAsync()
    {
        try
        {
            var result = await _apiClient.GetSetupStatusAsync();
            if (result.Success && result.Data != null)
            {
                _requireLegalConsent = result.Data.RequireLegalConsent;
                ConsentSection.IsVisible = _requireLegalConsent;
                UpdateCreateAccountButtonState();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load setup status: {ex.Message}");
        }
    }

    private async Task LoadAuthConfigurationAsync()
    {
        try
        {
            var result = await _oauthService.GetAuthConfigurationAsync();

            if (result.Success && result.Data != null)
            {
                var enabledProviders = result.Data.Providers
                    .Where(p => p.IsEnabled)
                    .ToList();

                if (enabledProviders.Count > 0)
                {
                    OAuthButtonsContainer.Clear();
                    _oauthButtons.Clear();

                    foreach (var provider in enabledProviders)
                    {
                        var button = CreateProviderButton(provider);
                        _oauthButtons.Add(button);
                        OAuthButtonsContainer.Add(button);
                    }

                    OAuthSection.IsVisible = true;
                }
                else
                {
                    OAuthSection.IsVisible = false;
                }
            }
            else
            {
                // Hide OAuth section if config fetch fails
                OAuthSection.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load auth config: {ex.Message}");
            OAuthSection.IsVisible = false;
        }
    }

    private Button CreateProviderButton(ExternalAuthProvider provider)
    {
        var button = new Button
        {
            Text = provider.DisplayName,
            HeightRequest = 45,
            CornerRadius = 22,
            Margin = new Thickness(5),
            MinimumWidthRequest = 140
        };

        // Apply provider-specific styling
        switch (provider.Provider.ToUpperInvariant())
        {
            case "GOOGLE":
                button.BackgroundColor = Colors.White;
                button.TextColor = Color.FromArgb("#4285F4");
                button.BorderColor = Color.FromArgb("#DADCE0");
                button.BorderWidth = 1;
                break;

            case "APPLE":
                button.SetAppThemeColor(
                    Button.BackgroundColorProperty,
                    Colors.Black,
                    Colors.White);
                button.SetAppThemeColor(
                    Button.TextColorProperty,
                    Colors.White,
                    Colors.Black);
                break;

            case "OIDC":
            default:
                button.SetAppThemeColor(
                    Button.BackgroundColorProperty,
                    Color.FromArgb("#1976D2"),
                    Color.FromArgb("#1565C0"));
                button.TextColor = Colors.White;
                break;
        }

        button.Clicked += async (s, e) => await OnProviderButtonClicked(provider);

        return button;
    }

    private void OnConsentCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        _consentChecked = e.Value;
        UpdateCreateAccountButtonState();
    }

    private void UpdateCreateAccountButtonState()
    {
        // If consent is required but not checked, disable buttons
        if (_requireLegalConsent && !_consentChecked)
        {
            CreateAccountButton.IsEnabled = false;
            foreach (var button in _oauthButtons)
            {
                button.IsEnabled = false;
            }
        }
        else
        {
            CreateAccountButton.IsEnabled = true;
            foreach (var button in _oauthButtons)
            {
                button.IsEnabled = true;
            }
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

    private async Task OnProviderButtonClicked(ExternalAuthProvider provider)
    {
        if (_requireLegalConsent && !_consentChecked)
        {
            ShowError("Please accept the Terms of Service and Privacy Policy");
            return;
        }

        SetLoading(true);
        HideError();

        try
        {
            var result = await _oauthService.LoginWithProviderAsync(provider.Provider);

            if (result.Success)
            {
                // Clear pending verification since OAuth signup was successful
                _onboardingService.ClearPendingVerification();

                // Configure for cloud with tenant name
                if (result.LoginResponse?.Tenant?.Name != null)
                {
                    _apiSettings.ConfigureForCloud(result.LoginResponse.Tenant.Name);
                }
                else
                {
                    _apiSettings.ConfigureForCloud(_householdName);
                }

                // Transition to main app
                App.TransitionToMainApp();
            }
            else if (result.WasCancelled)
            {
                // User cancelled - no error message needed
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Authentication failed. Please try again.");
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

    private async void OnCreateAccountClicked(object? sender, EventArgs e)
    {
        if (_requireLegalConsent && !_consentChecked)
        {
            ShowError("Please accept the Terms of Service and Privacy Policy");
            return;
        }

        // Validate inputs
        var firstName = FirstNameEntry.Text?.Trim();
        var lastName = LastNameEntry.Text?.Trim();
        var password = PasswordEntry.Text;
        var confirmPassword = ConfirmPasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(firstName))
        {
            ShowError("Please enter your first name");
            return;
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            ShowError("Please enter your last name");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter a password");
            return;
        }

        if (password.Length < 8)
        {
            ShowError("Password must be at least 8 characters");
            return;
        }

        if (password != confirmPassword)
        {
            ShowError("Passwords do not match");
            return;
        }

        SetLoading(true);
        HideError();

        try
        {
            var result = await _apiClient.CompleteRegistrationAsync(
                _verificationToken,
                firstName,
                lastName,
                password);

            Console.WriteLine($"[CreatePasswordPage] Result - Success: {result.Success}, Data null: {result.Data == null}");

            if (result.Success && result.Data != null)
            {
                Console.WriteLine("[CreatePasswordPage] Saving tokens...");
                // Save tokens
                await _tokenStorage.SetTokensAsync(
                    result.Data.AccessToken,
                    result.Data.RefreshToken);

                Console.WriteLine("[CreatePasswordPage] Configuring for cloud...");
                // Configure for cloud with tenant name
                _apiSettings.ConfigureForCloud(result.Data.Tenant?.Name ?? _householdName);

                Console.WriteLine("[CreatePasswordPage] Clearing onboarding state...");
                // Clear onboarding state
                _onboardingService.ClearPendingVerification();
                _onboardingService.MarkOnboardingCompleted();

                Console.WriteLine("[CreatePasswordPage] Transitioning to main app...");
                // Transition from onboarding NavigationPage to main AppShell
                App.TransitionToMainApp();
                Console.WriteLine("[CreatePasswordPage] TransitionToMainApp called");
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Registration failed. Please try again.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        CreateAccountButton.IsEnabled = !isLoading;
        FirstNameEntry.IsEnabled = !isLoading;
        LastNameEntry.IsEnabled = !isLoading;
        PasswordEntry.IsEnabled = !isLoading;
        ConfirmPasswordEntry.IsEnabled = !isLoading;

        // Disable OAuth buttons during loading
        foreach (var button in _oauthButtons)
        {
            button.IsEnabled = !isLoading;
        }
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
