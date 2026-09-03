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
    private readonly EmailLookupApi _emailLookupApi;
    private readonly ProxiedEmailCache _proxiedEmailCache;
    private readonly List<View> _oauthButtons = new();
    // Phase 4 chunk 4.F — render mode: when true, password is hidden until
    // /check resolves and the user taps Continue. Server-driven via
    // ClientFeatureFlags.TwoStepLoginV2 fetched in LoadAuthConfigurationAsync.
    private bool _twoStepMode;
    private bool _checkEndpointAvailable;

    /// <summary>
    /// True once a proxied-flow email came back NotFound and the sign-in
    /// switched itself to cloud. Persisted, not page state — LoginPage is
    /// transient and the mode it qualifies outlives it.
    /// </summary>
    private bool FellBackToCloud
    {
        get => _apiSettings.CloudModeWasGuessed;
        set => _apiSettings.CloudModeWasGuessed = value;
    }

    public LoginPage(
        ApiSettings apiSettings,
        TokenStorage tokenStorage,
        TenantStorage tenantStorage,
        ShoppingApiClient apiClient,
        OnboardingService onboardingService,
        OAuthService oauthService,
        EmailLookupApi emailLookupApi,
        ProxiedEmailCache proxiedEmailCache)
    {
        InitializeComponent();
        _apiSettings = apiSettings;
        _tokenStorage = tokenStorage;
        _tenantStorage = tenantStorage;
        _apiClient = apiClient;
        _onboardingService = onboardingService;
        _oauthService = oauthService;
        _emailLookupApi = emailLookupApi;
        _proxiedEmailCache = proxiedEmailCache;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Show tenant name for both self-hosted and proxied — both
        // ultimately point at a self-hosted home server and the tenant
        // label is the household's display name either way.
        var tenantName = _apiSettings.TenantName;
        if (!string.IsNullOrEmpty(tenantName) &&
            (_apiSettings.Mode == ServerMode.SelfHosted || _apiSettings.Mode == ServerMode.Proxied))
        {
            TenantNameLabel.Text = tenantName;
            TenantFrame.IsVisible = true;
        }
        else
        {
            TenantFrame.IsVisible = false;
        }

        // Show server-settings (with "Change server" link) for both
        // self-hosted and proxied users; hide the "Create Account" CTA
        // for both since account creation lives at the home server,
        // not in the mobile app.
        if (_apiSettings.Mode == ServerMode.SelfHosted)
        {
            ServerSettingsSection.IsVisible = true;
            ServerInfoLabel.Text = $"Server: {GetDisplayUrl(_apiSettings.SelfHostedUrl)}";
            CreateAccountSection.IsVisible = false;
        }
        else if (_apiSettings.Mode == ServerMode.Proxied)
        {
            ServerSettingsSection.IsVisible = true;
            ServerInfoLabel.Text = string.IsNullOrEmpty(_apiSettings.ProxiedDisplayName)
                ? "Sign-in service: auth.famick.com"
                : $"Home server: {_apiSettings.ProxiedDisplayName}";
            CreateAccountSection.IsVisible = false;

            // Pre-fill email entry with the most recently used address
            // so a repeat user gets one-tap Continue → cached lookup →
            // password page.
            if (string.IsNullOrEmpty(EmailEntry.Text))
            {
                var last = _proxiedEmailCache.LastUsedEmail;
                if (!string.IsNullOrEmpty(last))
                {
                    EmailEntry.Text = last;
                }
            }

            // Force two-step UI for proxied mode: until the user types
            // their email and taps Continue, we don't have a resolved
            // BaseUrl, so we can't fetch the real auth config from the
            // home server. The Continue handler re-runs
            // LoadAuthConfigurationAsync after the lookup completes.
            _twoStepMode = true;
            ApplyTwoStepModeUi();
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
                // The flag may turn two-step on, never off: proxied mode has no
                // resolved BaseUrl until the lookup runs, so a one-step form has
                // nowhere to post. In that mode this config also comes from the
                // bare AuthProxy origin, not the home server.
                _twoStepMode = result.Data.FeatureFlags.TwoStepLoginV2
                    || _apiSettings.Mode == ServerMode.Proxied;
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
            Color.FromArgb("#518751"),
            Color.FromArgb("#3D6B3D"));
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

                // Two ways to arrive here, and only one of them has a Shell to return to.
                //
                // Signing out from inside the app pushes this page modally over the
                // AppShell, so popping the modal reveals it again. But login is also the
                // root page — after an account deletion, and on a fresh start — and then
                // Shell.Current is null, because MainPage is a NavigationPage. Reaching
                // for it there throws, and the login screen reports it as a connection
                // error even though the sign-in itself succeeded.
                if (Navigation.ModalStack.Count > 0)
                    await Navigation.PopModalAsync();
                else if (Shell.Current is null)
                    App.TransitionToMainApp();
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
    ///
    /// Proxied mode adds an extra step: BEFORE /check can run, we need
    /// to resolve which home server this email belongs to. The cache
    /// short-circuits the round-trip for repeat sign-ins.
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
            // Proxied-mode email-lookup gate. Cache hit → set BaseUrl
            // immediately, no network call. Cache miss → ask AuthProxy
            // and store the result. On any non-Found outcome bail out
            // with a specific error and stay on Page 1.
            if (_apiSettings.Mode == ServerMode.Proxied)
            {
                if (!await TryResolveProxiedHomeServerAsync(email))
                {
                    return;
                }

                // BaseUrl now resolves to the home server's /h/{guid}/
                // proxied URL. Re-fetch the auth config so OAuth /
                // passkey rendering reflects the real home server, not
                // the bare AuthProxy origin we hit in OnAppearing.
                await LoadAuthConfigurationAsync();
            }

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

            // Lock the email once it has resolved to a server — retyping here
            // would check the password against the wrong one. "Change email" is
            // the way back. Covers the cloud fall-back too, otherwise a mistyped
            // address is stuck: the lookup only re-runs in proxied mode.
            if (_apiSettings.Mode == ServerMode.Proxied || FellBackToCloud)
            {
                EmailEntry.IsReadOnly = true;
                ChangeEmailLink.IsVisible = true;
            }

            PasswordEntry.Focus();
        }
        finally
        {
            SetLoading(false);
        }
    }

    /// <summary>
    /// Proxied-mode "go back" — reverts the UI from the password step
    /// to the email-entry step so the user can pick a different
    /// account / home server. Does not invalidate the cache for the
    /// current email — the user might come back to it.
    /// </summary>
    private void OnChangeEmailTapped(object? sender, EventArgs e)
    {
        ReturnToEmailStep();
        HideError();
    }

    /// <summary>
    /// Returns to the email step, undoing a cloud fall-back. Restoring proxied
    /// mode is the part that matters — the lookup is gated on it, so otherwise
    /// a corrected address goes to cloud regardless.
    /// </summary>
    private void ReturnToEmailStep()
    {
        if (FellBackToCloud)
        {
            _apiSettings.ConfigureForProxied();
            FellBackToCloud = false;
            _twoStepMode = true;
        }

        EmailEntry.IsReadOnly = false;
        ChangeEmailLink.IsVisible = false;
        PasswordSection.IsVisible = false;
        LoginButton.IsVisible = false;
        ContinueButton.IsVisible = true;
        PasswordEntry.Text = string.Empty;
        EmailEntry.Focus();
    }

    /// <summary>
    /// Proxied-mode helper: resolves <paramref name="email"/> to a
    /// home server URL by consulting the local cache first and falling
    /// back to <see cref="EmailLookupApi"/> on miss. On success the
    /// resolved URL is written to <see cref="ApiSettings.ProxiedBaseUrl"/>
    /// — the next API call automatically routes through it via
    /// <see cref="DynamicApiHttpHandler"/>.
    /// </summary>
    /// <returns>True when the email resolved; false when the caller
    /// should bail out (error already surfaced).</returns>
    private async Task<bool> TryResolveProxiedHomeServerAsync(string email)
    {
        if (_proxiedEmailCache.TryGet(email, out var cached))
        {
            _apiSettings.ConfigureProxiedHomeServer(cached);
            return true;
        }

        var outcome = await _emailLookupApi.LookupAsync(email);
        switch (outcome.Kind)
        {
            case EmailLookupOutcomeKind.Found:
                _proxiedEmailCache.Set(outcome.Result!);
                _apiSettings.ConfigureProxiedHomeServer(outcome.Result!);
                return true;

            // A definitive negative, and the only outcome allowed to move the
            // sign-in to another server.
            case EmailLookupOutcomeKind.NotFound:
                _apiSettings.ConfigureForCloud(tenantName: null);
                FellBackToCloud = true;
                return true;

            // No answer is not a negative answer, so these fail closed. Routing
            // on them would post an on-prem password to cloud whenever AuthProxy
            // was unreachable — and where the password is reused, that succeeds
            // and lands the user silently in the wrong tenant.
            case EmailLookupOutcomeKind.RateLimited:
                ShowError("Too many sign-in attempts. Try again in a minute.");
                return false;

            case EmailLookupOutcomeKind.Network:
            default:
                ShowError("Can't reach the sign-in service. Check your internet connection and try again.");
                return false;
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

                // Settles what the lookup could only infer, so the mode stops
                // being a guess — otherwise every later password slip bounces
                // the user back to re-run a lookup whose answer is known.
                FellBackToCloud = false;

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

                // Two ways to arrive here, and only one of them has a Shell to return to.
                //
                // Signing out from inside the app pushes this page modally over the
                // AppShell, so popping the modal reveals it again. But login is also the
                // root page — after an account deletion, and on a fresh start — and then
                // Shell.Current is null, because MainPage is a NavigationPage. Reaching
                // for it there throws, and the login screen reports it as a connection
                // error even though the sign-in itself succeeded.
                if (Navigation.ModalStack.Count > 0)
                    await Navigation.PopModalAsync();
                else if (Shell.Current is null)
                    App.TransitionToMainApp();
                else if (App.PendingSharedContact != null)
                    await Shell.Current.GoToAsync(nameof(ImportContactPage));
                else
                    await Shell.Current.GoToAsync("//DashboardPage");
            }
            else
            {
                // A rejection from a server we only guessed makes the guess
                // suspect, so discard it. Scoped to the fall-back: a real cloud
                // user who fumbles a password stays put. The cost is that a
                // fall-back user re-enters their email — cheaper than being
                // stuck, and the server answers wrong-password and no-such-
                // account identically, so the client cannot tell them apart.
                if (FellBackToCloud)
                {
                    ReturnToEmailStep();
                    ShowError("That didn't work. Check the address and try again.");
                }
                else
                {
                    ShowError(result.ErrorMessage ?? "Login failed. Please check your credentials.");
                }
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
