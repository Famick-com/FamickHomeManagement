using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using static Famick.HomeManagement.Mobile.Pages.Profile.ProfileUiHelpers;

namespace Famick.HomeManagement.Mobile.Pages.Profile;

public partial class ProfileSecurityPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private UserProfileMobile? _profile;
    private List<LinkedAccountMobile>? _linkedAccounts;
    private List<ExternalAuthProvider>? _providers;
    private bool _loaded;

    private Entry? _currentPasswordEntry;
    private Entry? _newPasswordEntry;
    private Entry? _confirmPasswordEntry;

    public ProfileSecurityPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded)
        {
            await LoadSecurityDataAsync();
            _loaded = true;
        }
    }

    private async Task LoadSecurityDataAsync()
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        try
        {
            var profileTask = _apiClient.GetProfileAsync();
            var linkedTask = _apiClient.GetLinkedAccountsAsync();
            var providersTask = _apiClient.GetAvailableProvidersAsync();
            await Task.WhenAll(profileTask, linkedTask, providersTask);

            if (profileTask.Result.Success)
                _profile = profileTask.Result.Data;
            if (linkedTask.Result.Success)
                _linkedAccounts = linkedTask.Result.Data;
            if (providersTask.Result.Success)
                _providers = providersTask.Result.Data;

            RenderSecurity();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load security data: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void RenderSecurity()
    {
        SecurityStack.Children.Clear();

        // Change Password section
        if (_profile?.HasPassword == true)
        {
            _currentPasswordEntry = new Entry
            {
                Placeholder = "Current Password",
                IsPassword = true,
                TextColor = GetTextColor(),
                PlaceholderColor = GetSecondaryTextColor()
            };
            _newPasswordEntry = new Entry
            {
                Placeholder = "New Password",
                IsPassword = true,
                TextColor = GetTextColor(),
                PlaceholderColor = GetSecondaryTextColor()
            };
            _confirmPasswordEntry = new Entry
            {
                Placeholder = "Confirm New Password",
                IsPassword = true,
                TextColor = GetTextColor(),
                PlaceholderColor = GetSecondaryTextColor()
            };

            var changePasswordBtn = new Button
            {
                Text = "Change Password",
                BackgroundColor = Color.FromArgb("#1976D2"),
                TextColor = Colors.White,
                CornerRadius = 8,
                Padding = new Thickness(20, 10),
                Margin = new Thickness(0, 5, 0, 0)
            };
            changePasswordBtn.Clicked += OnChangePasswordClicked;

            SecurityStack.Children.Add(CreateCard(new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    CreateLabel("Change Password", true, 16),
                    _currentPasswordEntry,
                    _newPasswordEntry,
                    _confirmPasswordEntry,
                    changePasswordBtn
                }
            }));
        }
        else
        {
            SecurityStack.Children.Add(CreateCard(new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    CreateLabel("Password", true, 16),
                    CreateLabel("Your account uses external sign-in only. No password is set.")
                }
            }));
        }

        // Divider
        SecurityStack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#424242")
                : Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 5)
        });

        // Linked Accounts section
        SecurityStack.Children.Add(CreateLabel("Linked Accounts", true, 18));

        var totalAuthMethods = (_linkedAccounts?.Count ?? 0) + (_profile?.HasPassword == true ? 1 : 0);

        if (_linkedAccounts != null && _linkedAccounts.Count > 0)
        {
            foreach (var account in _linkedAccounts)
            {
                var canUnlink = totalAuthMethods > 1;

                var unlinkBtn = new Button
                {
                    Text = "Unlink",
                    BackgroundColor = canUnlink ? Color.FromArgb("#D32F2F") : Color.FromArgb("#9E9E9E"),
                    TextColor = Colors.White,
                    CornerRadius = 6,
                    FontSize = 12,
                    Padding = new Thickness(10, 5),
                    IsEnabled = canUnlink,
                    CommandParameter = account.Provider
                };
                unlinkBtn.Clicked += OnUnlinkProviderClicked;

                SecurityStack.Children.Add(CreateCard(new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        CreateLabel(account.ProviderDisplayName, true),
                        CreateLabel(account.ProviderEmail ?? "No email", false, 13),
                        CreateLabel($"Linked: {account.LinkedAt:d}", false, 12),
                        unlinkBtn
                    }
                }));
            }
        }

        // Show link buttons for available but not-linked providers
        if (_providers != null)
        {
            var linkedProviderNames = _linkedAccounts?.Select(a => a.Provider).ToHashSet() ?? new HashSet<string>();

            foreach (var provider in _providers.Where(p => p.IsEnabled && !linkedProviderNames.Contains(p.Provider)))
            {
                var linkBtn = new Button
                {
                    Text = $"Link {provider.DisplayName}",
                    BackgroundColor = Color.FromArgb("#1976D2"),
                    TextColor = Colors.White,
                    CornerRadius = 8,
                    Padding = new Thickness(20, 10),
                    CommandParameter = provider.Provider
                };
                linkBtn.Clicked += OnLinkProviderClicked;
                SecurityStack.Children.Add(linkBtn);
            }
        }

        SecurityStack.Children.Add(new BoxView { HeightRequest = 20, BackgroundColor = Colors.Transparent });
    }

    private async void OnChangePasswordClicked(object? sender, EventArgs e)
    {
        if (_currentPasswordEntry == null || _newPasswordEntry == null || _confirmPasswordEntry == null) return;

        var currentPassword = _currentPasswordEntry.Text ?? "";
        var newPassword = _newPasswordEntry.Text ?? "";
        var confirmPassword = _confirmPasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            await DisplayAlert("Error", "Please fill in all password fields", "OK");
            return;
        }

        if (newPassword != confirmPassword)
        {
            await DisplayAlert("Error", "New passwords do not match", "OK");
            return;
        }

        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "Changing...";
        }

        try
        {
            var result = await _apiClient.ChangePasswordAsync(currentPassword, newPassword, confirmPassword);
            if (result.Success)
            {
                _currentPasswordEntry.Text = "";
                _newPasswordEntry.Text = "";
                _confirmPasswordEntry.Text = "";
                await DisplayAlert("Success", "Password changed successfully", "OK");
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to change password", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to change password: {ex.Message}", "OK");
        }
        finally
        {
            if (sender is Button btn2)
            {
                btn2.IsEnabled = true;
                btn2.Text = "Change Password";
            }
        }
    }

    private async void OnUnlinkProviderClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string provider })
        {
            var confirm = await DisplayAlert("Unlink", $"Unlink {provider} from your account?", "Yes", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.UnlinkProviderAsync(provider);
            if (result.Success)
            {
                _loaded = false;
                await LoadSecurityDataAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to unlink provider", "OK");
            }
        }
    }

    private async void OnLinkProviderClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string provider }) return;

        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            if (services == null) return;

            switch (provider.ToUpperInvariant())
            {
                case "APPLE":
                {
                    var appleService = services.GetService<IAppleSignInService>();
                    if (appleService is not { IsAvailable: true })
                    {
                        await DisplayAlert("Unavailable", "Apple Sign-In is not available on this device", "OK");
                        return;
                    }
                    var credential = await appleService.SignInAsync();
                    var result = await _apiClient.LinkAppleNativeAsync(
                        credential.IdentityToken, credential.AuthorizationCode, credential.UserIdentifier);
                    if (result.Success)
                    {
                        _loaded = false;
                        await LoadSecurityDataAsync();
                        await DisplayAlert("Success", "Apple account linked", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", result.ErrorMessage ?? "Failed to link Apple account", "OK");
                    }
                    break;
                }
                case "GOOGLE":
                {
                    var googleService = services.GetService<IGoogleSignInService>();
                    if (googleService is not { IsAvailable: true })
                    {
                        await DisplayAlert("Unavailable", "Google Sign-In is not available on this device", "OK");
                        return;
                    }
                    var credential = await googleService.SignInAsync();
                    var result = await _apiClient.LinkGoogleNativeAsync(credential.IdToken);
                    if (result.Success)
                    {
                        _loaded = false;
                        await LoadSecurityDataAsync();
                        await DisplayAlert("Success", "Google account linked", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", result.ErrorMessage ?? "Failed to link Google account", "OK");
                    }
                    break;
                }
                default:
                    await DisplayAlert("Unavailable", $"Linking {provider} is not supported on mobile", "OK");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // User cancelled sign-in
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to link account: {ex.Message}", "OK");
        }
    }
}
