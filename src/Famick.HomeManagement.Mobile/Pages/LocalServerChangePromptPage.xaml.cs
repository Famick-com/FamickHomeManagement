using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

/// <summary>
/// Phase 4 chunk 4.G — Full-screen prompt shown when the server-delivered
/// <c>LoginResponse.LocalServer</c> differs from the value last stored on
/// this device. Per the design doc (§"Local Server URL Change Detection"),
/// the page must be dismissable only by explicit user confirmation — no
/// swipe-back, no system back-button.
/// </summary>
public partial class LocalServerChangePromptPage : ContentPage
{
    private const string LastLocalServerKey = "last_local_server";

    private readonly TokenStorage _tokenStorage;
    private readonly string _oldUrl;
    private readonly string _newUrl;

    public LocalServerChangePromptPage(TokenStorage tokenStorage, string oldUrl, string newUrl)
    {
        InitializeComponent();
        _tokenStorage = tokenStorage;
        _oldUrl = oldUrl;
        _newUrl = newUrl;
        OldUrlLabel.Text = string.IsNullOrEmpty(oldUrl) ? "(none)" : oldUrl;
        NewUrlLabel.Text = newUrl;
    }

    /// <summary>
    /// Override the hardware/gesture back button so the user can't bypass
    /// the prompt by swiping. Sign-out remains the only escape hatch.
    /// </summary>
    protected override bool OnBackButtonPressed() => true;

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        Preferences.Default.Set(LastLocalServerKey, _newUrl);
        await Navigation.PopModalAsync();
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        // Don't trust the new URL — clear tokens and let the user re-login
        // against a known-good server config.
        await _tokenStorage.ClearTokensAsync();
        await Navigation.PopModalAsync();
        await Shell.Current.GoToAsync("//DashboardPage");
    }
}
