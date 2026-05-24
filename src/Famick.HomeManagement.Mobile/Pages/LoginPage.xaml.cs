using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Pages.Contacts;
using Famick.HomeManagement.Mobile.Pages.Onboarding;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly ApiSettings _apiSettings;
    private readonly TokenStorage _tokenStorage;
    private readonly TenantStorage _tenantStorage;
    private readonly ShoppingApiClient _apiClient;
    private readonly OnboardingService _onboardingService;
    private readonly OAuthService _oauthService;
    private readonly List<View> _oauthButtons = new();
    // Phase 4 chunk 4.F — render mode: when true, password is hidden until
    // /check resolves and the user taps Continue. Server-driven via
    // ClientFeatureFlags.TwoStepLoginV2 fetched in LoadAuthConfigurationAsync.
    private bool _twoStepMode;
    private bool _checkEndpointAvailable;

    public LoginPage(
        ApiSettings apiSettings,
        TokenStorage tokenStorage,
        TenantStorage tenantStorage,
        ShoppingApiClient apiClient,
        OnboardingService onboardingService,
        OAuthService oauthService)
    {
        InitializeComponent();
        _apiSettings = apiSettings;
        _tokenStorage = tokenStorage;
        _tenantStorage = tenantStorage;
        _apiClient = apiClient;
        _onboardingService = onboardingService;
        _oauthService = oauthService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Show tenant name for self-hosted users only
        var tenantName = _apiSettings.TenantName;
        if (!string.IsNullOrEmpty(tenantName) && _apiSettings.Mode == ServerMode.SelfHosted)
        {
            TenantNameLabel.Text = tenantName;
            TenantFrame.IsVisible = true;
        }
        else
        {
            TenantFrame.IsVisible = false;
        }

        // Show server settings link for self-hosted users
        if (_apiSettings.Mode == ServerMode.SelfHosted)
        {
            ServerSettingsSection.IsVisible = true;
            ServerInfoLabel.Text = $"Server: {GetDisplayUrl(_apiSettings.SelfHostedUrl)}";
            CreateAccountSection.IsVisible = false;
        }
        else
        {
            ServerSettingsSection.IsVisible = false;
            // Show "Create Account" for cloud servers
            CreateAccountSection.IsVisible = _apiSettings.IsCloudServer();
        }

        // Load OAuth configuration
        await LoadAuthConfigurationAsync();
    }

    private async Task LoadAuthConfigurationAsync()
    {
        try
        {
            var result = await _oauthService.GetAuthConfigurationAsync();

            if (result.Success && result.Data != null)
            {
                // Phase 4 chunk 4.F — server-driven UI mode + /check availability.
                _twoStepMode = result.Data.FeatureFlags.TwoStepLoginV2;
                _checkEndpointAvailable = result.Data.FeatureFlags.CheckEndpointEnabled;
                ApplyTwoStepModeUi();

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

    private View CreateProviderButton(ExternalAuthProvider provider)
    {
        var providerKey = provider.Provider.ToUpperInvariant();
        var isIconOnly = providerKey is "GOOGLE" or "APPLE";

        if (isIconOnly)
        {
            var imageButton = new ImageButton
            {
                Source = GetProviderImageSource(providerKey),
                HeightRequest = 50,
                WidthRequest = 50,
                CornerRadius = 25,
                Margin = new Thickness(5),
                Padding = new Thickness(12),
                Aspect = Aspect.AspectFit
            };

            switch (providerKey)
            {
                case "GOOGLE":
                    imageButton.BackgroundColor = Colors.White;
                    imageButton.BorderColor = Color.FromArgb("#DADCE0");
                    imageButton.BorderWidth = 1;
                    break;

                case "APPLE":
                    imageButton.BackgroundColor = Colors.Black;
                    break;
            }

            imageButton.Clicked += async (s, e) => await OnProviderButtonClicked(provider);
            return imageButton;
        }

        var button = new Button
        {
            Text = provider.DisplayName,
            HeightRequest = 45,
            CornerRadius = 22,
            Margin = new Thickness(5),
            MinimumWidthRequest = 140
        };

        button.SetAppThemeColor(
            Button.BackgroundColorProperty,
            Color.FromArgb("#1976D2"),
            Color.FromArgb("#1565C0"));
        button.TextColor = Colors.White;

        button.Clicked += async (s, e) => await OnProviderButtonClicked(provider);
        return button;
    }

    private static string GetProviderImageSource(string providerKey)
    {
        return providerKey switch
        {
            "GOOGLE" => "google_logo",
            "APPLE" => "apple_logo",
            _ => "dotnet_bot"
        };
    }

    private async Task OnProviderButtonClicked(ExternalAuthProvider provider)
    {
        SetLoading(true);
        HideError();

        try
        {
            var result = await _oauthService.LoginWithProviderAsync(provider.Provider);

            if (result.Success)
            {
                // Check if user must change password (unlikely for OAuth but handle it)
                if (result.MustChangePassword)
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    if (services != null)
                    {
                        var forcePage = services.GetRequiredService<ForceChangePasswordPage>();
                        await Navigation.PushAsync(forcePage);
                    }
                    return;
                }

                // Phase 4 follow-up — parity with OnLoginClicked. OAuth flows
                // can also produce must_accept_terms claims (cloud-mode only
                // today, but the gate has been live since Phase 2). Without
                // this check, OAuth users who need to accept new terms would
                // land on the dashboard with a stale-terms session.
                if (result.LoginResponse?.MustAcceptTerms == true)
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    if (services != null)
                    {
                        var termsPage = services.GetRequiredService<AcceptTermsPage>();
                        await Navigation.PushAsync(termsPage);
                    }
                    return;
                }

                // Phase 4 chunk 4.G + follow-up — if the server delivered a
                // different LocalServer URL than we last saw, push the
                // confirmation prompt BEFORE dismissing the login modal.
                // Prompt's confirm-handler does the dashboard transition.
                if (result.LocalServerChange is not null)
                {
                    await Navigation.PushAsync(new LocalServerChangePromptPage(
                        _tokenStorage,
                        result.LocalServerChange.OldUrl,
                        result.LocalServerChange.NewUrl));
                    return;
                }

                // Dismiss the modal login page -- DashboardPage.OnAppearing handles the rest
                if (Navigation.ModalStack.Count > 0)
                    await Navigation.PopModalAsync();
                else if (App.PendingSharedContact != null)
                    await Shell.Current.GoToAsync(nameof(ImportContactPage));
                else
                    await Shell.Current.GoToAsync("//DashboardPage");
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

    private static string GetDisplayUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return url;
        }
    }

    /// <summary>
    /// Phase 4 chunk 4.F — show only email + Continue + OAuth buttons until
    /// the user taps Continue. The OAuth-button section is unchanged: tapping
    /// Apple / Google / OIDC short-circuits /check entirely, per the design
    /// doc's "When /check is Skipped" rules.
    /// </summary>
    private void ApplyTwoStepModeUi()
    {
        if (_twoStepMode)
        {
            PasswordSection.IsVisible = false;
            LoginButton.IsVisible = false;
            ContinueButton.IsVisible = true;
        }
        else
        {
            PasswordSection.IsVisible = true;
            LoginButton.IsVisible = true;
            ContinueButton.IsVisible = false;
        }
    }

    /// <summary>
    /// Phase 4 chunk 4.F — Continue flow. Validates the email, optionally
    /// calls POST /check to learn account type, then reveals the password
    /// section + Sign In button. On rate-limit, surfaces the error and
    /// leaves the user on Page 1.
    /// </summary>
    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EmailEntry.Text))
        {
            ShowError("Please enter your email");
            return;
        }

        var email = EmailEntry.Text.Trim();
        SetLoading(true);
        HideError();

        try
        {
            // /check is informational — if the endpoint isn't available
            // (older server, flag off), skip the call and just reveal the
            // password section. Authentication still works the same way.
            if (_checkEndpointAvailable)
            {
                var check = await _apiClient.CheckEmailAsync(email);
                if (!check.Success)
                {
                    ShowError(check.ErrorMessage ?? "Could not check account");
                    return;
                }

                // Phase 4 always returns "cloud" — chunk 4.D adds the column
                // that lights up the "self" branch. When that ships we'll
                // route here: cloud → keep current ApiSettings.Mode, self →
                // ensure self-hosted config is in place before showing the
                // password section.
                _ = check.Data?.AccountType;
            }

            // Reveal password page (progressive disclosure within this
            // ContentPage; not a Navigation.PushAsync — keeps the email field
            // and OAuth buttons visible above the password section).
            PasswordSection.IsVisible = true;
            LoginButton.IsVisible = true;
            ContinueButton.IsVisible = false;
            PasswordEntry.Focus();
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(EmailEntry.Text))
        {
            ShowError("Please enter your email");
            return;
        }

        if (string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            ShowError("Please enter your password");
            return;
        }

        SetLoading(true);
        HideError();

        try
        {
            var result = await _apiClient.LoginAsync(EmailEntry.Text.Trim(), PasswordEntry.Text);

            if (result.Success && result.Data != null)
            {
                await _tokenStorage.SetTokensAsync(result.Data.AccessToken, result.Data.RefreshToken);
                // Phase 4 chunk 4.G — change-detection on the local-server URL.
                var serverChange = LocalServerChangeDetector.ObserveLogin(result.Data.LocalServer);

                // Get tenant name - try from login response first, then fetch separately
                var tenantName = result.Data.Tenant?.Name;
                if (string.IsNullOrEmpty(tenantName))
                {
                    var tenantResult = await _apiClient.GetTenantAsync();
                    if (tenantResult.Success && tenantResult.Data != null)
                    {
                        tenantName = tenantResult.Data.Name;
                    }
                }

                // Update tenant name in settings and storage
                if (!string.IsNullOrEmpty(tenantName))
                {
                    _apiSettings.TenantName = tenantName;
                }
                await _tenantStorage.SetTenantNameAsync(tenantName);

                // Store subscription tier for client-side feature gating
                if (result.Data.Tenant != null)
                {
                    _tenantStorage.SetSubscriptionState(
                        result.Data.Tenant.SubscriptionTier,
                        result.Data.Tenant.IsTrialActive,
                        result.Data.Tenant.IsExpired);
                }

                // Mark onboarding as complete and server as configured
                _onboardingService.MarkOnboardingCompleted();
                _apiSettings.MarkServerConfigured();

                // Check if user must change password before accessing the app
                if (result.Data.MustChangePassword)
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    if (services != null)
                    {
                        var forcePage = services.GetRequiredService<ForceChangePasswordPage>();
                        forcePage.UserEmail = EmailEntry.Text?.Trim();
                        await Navigation.PushAsync(forcePage);
                    }
                    return;
                }

                // Check if user must accept terms before accessing the app
                if (result.Data.MustAcceptTerms)
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    if (services != null)
                    {
                        var termsPage = services.GetRequiredService<AcceptTermsPage>();
                        await Navigation.PushAsync(termsPage);
                    }
                    return;
                }

                // Phase 4 chunk 4.G — if the server delivered a different
                // LocalServer URL than we last saw, push the confirmation
                // prompt BEFORE dismissing the login modal. The prompt's
                // confirm-handler does the dashboard transition. Pushing
                // here (still on the LoginPage navigation stack) avoids the
                // pop/push race that swallowed the modal in earlier builds.
                if (serverChange is not null)
                {
                    await Navigation.PushAsync(new LocalServerChangePromptPage(
                        _tokenStorage, serverChange.OldUrl, serverChange.NewUrl));
                    return;
                }

                // Dismiss the modal login page -- DashboardPage.OnAppearing handles the rest
                if (Navigation.ModalStack.Count > 0)
                    await Navigation.PopModalAsync();
                else if (App.PendingSharedContact != null)
                    await Shell.Current.GoToAsync(nameof(ImportContactPage));
                else
                    await Shell.Current.GoToAsync("//DashboardPage");
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Login failed. Please check your credentials.");
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
        var services = Application.Current?.Handler?.MauiContext?.Services;
        if (services != null)
        {
            var welcomePage = services.GetRequiredService<WelcomePage>();
            await Navigation.PushAsync(welcomePage);
        }
    }

    private async void OnChangeServerTapped(object? sender, EventArgs e)
    {
        // Navigate to server configuration page
        await Navigation.PushAsync(
            Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<ServerConfigPage>());
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        LoginButton.IsEnabled = !isLoading;
        ContinueButton.IsEnabled = !isLoading;
        EmailEntry.IsEnabled = !isLoading;
        PasswordEntry.IsEnabled = !isLoading;

        // Disable OAuth buttons during loading
        foreach (var view in _oauthButtons)
        {
            view.IsEnabled = !isLoading;
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
