using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

/// <summary>
/// Phase 2.5 — modal page shown in response to a
/// <see cref="Messages.StepUpRequiredMessage"/>. Collects the user's password,
/// calls <c>POST /api/auth/reauth</c>, writes the new access token via
/// <see cref="TokenStorage.SetAccessTokenAsync"/>, and completes the message's
/// TCS so <see cref="AuthenticatingHttpHandler"/> retries the original request.
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
    private bool _tcsCompleted;

    public TaskCompletionSource<string?>? Tcs { get; set; }

    public StepUpReauthPage(ShoppingApiClient apiClient, TokenStorage tokenStorage)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
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
