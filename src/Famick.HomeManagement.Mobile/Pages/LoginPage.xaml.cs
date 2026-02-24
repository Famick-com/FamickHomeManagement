using Famick.HomeManagement.Mobile.Models;
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
                    imageButton.SetAppThemeColor(
                        ImageButton.BackgroundColorProperty,
                        Colors.Black,
                        Colors.White);
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

                // Navigate to main app
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

                // Navigate to main app
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
