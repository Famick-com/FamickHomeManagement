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
    private readonly bool _midSession;

    public LocalServerChangePromptPage(TokenStorage tokenStorage, string oldUrl, string newUrl, bool midSession = false)
    {
        InitializeComponent();
        _tokenStorage = tokenStorage;
        _oldUrl = oldUrl;
        _newUrl = newUrl;
        // Phase 4 follow-up — mid-session prompt (e.g., ProfileSecurityPage
        // re-auth) confirms back to the originating page instead of bouncing
        // to the dashboard. Sign-out from mid-session still routes back to
        // the login screen since tokens are cleared.
        _midSession = midSession;
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

        // Phase 4 chunk 4.G — pushed via Navigation.PushAsync from the
        // login flow (not modal — that races against the login modal's
        // pop on iOS). Pop self off the navigation stack, then continue
        // the deferred transition.
        await Navigation.PopAsync();

        // Mid-session: the user was on a page like ProfileSecurityPage
        // when the change-detector fired. Land them back there; the
        // originating page's own continuation runs after our PopAsync.
        if (_midSession)
            return;

        // Pre-dashboard flow: complete the login transition the prompt
        // intercepted.
        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync();
        else
            await Shell.Current.GoToAsync("//DashboardPage");
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        // Don't trust the new URL — clear tokens and let the user re-login
        // against a known-good server config.
        await _tokenStorage.ClearTokensAsync();
        await Navigation.PopAsync();

        // Mid-session sign-out: bounce out of the Shell back to LoginPage.
        // Pre-dashboard sign-out: the underlying login modal is still
        // presented; popping self lands the user back on LoginPage already.
        if (_midSession)
            App.TransitionToMainApp();
    }
}
