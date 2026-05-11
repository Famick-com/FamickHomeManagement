using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

/// <summary>
/// Phase 2.5 — modal page shown in response to a
/// <see cref="Messages.StepUpRequiredMessage"/>. Collects the user's password
/// (or, since Phase 2.5b, a platform passkey assertion), calls the appropriate
/// server endpoint, writes the new access token via <see cref="TokenStorage"/>,
/// and completes the message's TCS so <see cref="AuthenticatingHttpHandler"/>
/// retries the original request.
///
/// The page is constructed by App.xaml.cs's message subscription and the TCS
/// is supplied via the <see cref="Tcs"/> property before <c>PushModalAsync</c>.
/// On dismiss (Cancel button or hardware back), the TCS is completed with
/// <c>null</c> if it hasn't been completed yet, so the handler doesn't hang.
/// </summary>
public partial class StepUpReauthPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly TokenStorage _tokenStorage;
    private readonly IPasskeyAuthenticator? _passkeyAuthenticator;
    private bool _tcsCompleted;

    public TaskCompletionSource<string?>? Tcs { get; set; }

    public StepUpReauthPage(
        ShoppingApiClient apiClient,
        TokenStorage tokenStorage,
        IPasskeyAuthenticator? passkeyAuthenticator = null)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _passkeyAuthenticator = passkeyAuthenticator;

        // Phase 2.5b — show the "Use Passkey" button only on platforms with a
        // native passkey provider (iOS 16+ / Android API 28+). The OS-level
        // passkey sheet handles "no passkey for this account" with its own
        // UX when the user taps it, so no upfront server check is needed.
        PasskeyButton.IsVisible = _passkeyAuthenticator?.IsSupported == true;
    }

    protected override bool OnBackButtonPressed()
    {
        // Hardware back / swipe-back counts as a cancel.
        CompleteTcsWithCancel();
        return base.OnBackButtonPressed();
    }

    private async void OnPasswordEntryCompleted(object? sender, EventArgs e)
    {
        await SubmitAsync().ConfigureAwait(false);
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        await SubmitAsync().ConfigureAwait(false);
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        CompleteTcsWithCancel();
        if (Navigation.ModalStack.Count > 0)
        {
            await Navigation.PopModalAsync().ConfigureAwait(false);
        }
    }

    private async void OnPasskeyClicked(object? sender, EventArgs e)
    {
        if (_passkeyAuthenticator is null || !_passkeyAuthenticator.IsSupported)
        {
            ShowError("Passkey is not available on this device.");
            return;
        }

        SetLoading(true);
        HideError();

        try
        {
            // 1. Server-side options — email scopes allowCredentials to the current
            //    authenticated user so the OS sheet doesn't surface passkeys for
            //    other accounts the user may have synced on this device.
            var email = _tokenStorage.GetEmailFromToken();
            var optionsResult = await _apiClient.PasskeyAuthenticateOptionsAsync(email).ConfigureAwait(true);
            if (!optionsResult.Success || optionsResult.Data is not { } opts
                || string.IsNullOrEmpty(opts.Options)
                || string.IsNullOrEmpty(opts.SessionId))
            {
                ShowError(optionsResult.ErrorMessage ?? "Could not start passkey authentication.");
                return;
            }

            // 2. Native ceremony — opens the OS passkey sheet, awaits user choice
            //    + biometric. Null result means cancelled / no matching passkey /
            //    biometric failed; surface to the user and let them retry or
            //    fall back to password.
            var assertion = await _passkeyAuthenticator.AuthenticateAsync(opts.Options).ConfigureAwait(true);
            if (string.IsNullOrEmpty(assertion))
            {
                ShowError("Passkey authentication was cancelled or failed.");
                return;
            }

            // 3. Server verifies the assertion and returns fresh tokens. NOTE:
            //    this rotates the refresh-token family (verify endpoint returns
            //    a full LoginResponse — Option A in docs/step-up-authentication.md).
            var verifyResult = await _apiClient.PasskeyAuthenticateVerifyAsync(
                opts.SessionId,
                assertion,
                rememberMe: false).ConfigureAwait(true);

            if (verifyResult.Success && verifyResult.Data is { } login
                && !string.IsNullOrEmpty(login.AccessToken)
                && !string.IsNullOrEmpty(login.RefreshToken))
            {
                // Passkey verify rotates the refresh-token family, so use
                // SetTokensAsync (both tokens) rather than SetAccessTokenAsync.
                await _tokenStorage.SetTokensAsync(login.AccessToken, login.RefreshToken).ConfigureAwait(true);

                CompleteTcs(login.AccessToken);

                if (Navigation.ModalStack.Count > 0)
                {
                    await Navigation.PopModalAsync().ConfigureAwait(true);
                }
                return;
            }

            ShowError(verifyResult.ErrorMessage ?? "Passkey authentication failed. Please try again.");
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

    private async Task SubmitAsync()
    {
        var password = PasswordEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter your password");
            return;
        }

        SetLoading(true);
        HideError();

        try
        {
            var result = await _apiClient.ReauthAsync(password).ConfigureAwait(true);

            if (result.Success && result.Data is { } reauth && !string.IsNullOrEmpty(reauth.AccessToken))
            {
                // Swap only the access token — reauth preserves the refresh-token family.
                await _tokenStorage.SetAccessTokenAsync(reauth.AccessToken).ConfigureAwait(true);

                CompleteTcs(reauth.AccessToken);

                if (Navigation.ModalStack.Count > 0)
                {
                    await Navigation.PopModalAsync().ConfigureAwait(true);
                }
                return;
            }

            ShowError(result.ErrorMessage ?? "Re-authentication failed. Please try again.");
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

    private void CompleteTcs(string accessToken)
    {
        if (_tcsCompleted) return;
        _tcsCompleted = true;
        Tcs?.TrySetResult(accessToken);
    }

    private void CompleteTcsWithCancel()
    {
        if (_tcsCompleted) return;
        _tcsCompleted = true;
        Tcs?.TrySetResult(null);
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        ConfirmButton.IsEnabled = !isLoading;
        CancelButton.IsEnabled = !isLoading;
        PasskeyButton.IsEnabled = !isLoading;
        PasswordEntry.IsEnabled = !isLoading;
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
