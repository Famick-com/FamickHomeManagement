using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Profile;

/// <summary>
/// Account and household deletion, as required by App Store Review Guideline 5.1.1(v):
/// an app offering account creation must offer deletion from inside it.
/// </summary>
/// <remarks>
/// The screen's job is to make sure nobody deletes a household by accident. The server
/// decides the scope from the caller's role, so this page asks for it before saying
/// anything — an admin's request destroys other people's data, and the warning has to
/// say so before the button is reachable.
/// </remarks>
public partial class DeleteAccountPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly TokenStorage _tokenStorage;

    private AccountDeletionStatusMobile? _status;

    /// <summary>
    /// What has to be typed before a household deletion is allowed. Null when the request
    /// only removes this user, where no phrase is required.
    /// </summary>
    private string? _confirmationPhrase;

    public DeleteAccountPage(ShoppingApiClient apiClient, TokenStorage tokenStorage)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStatusAsync();
    }

    private async Task LoadStatusAsync()
    {
        SetLoading(true);
        ErrorLabel.IsVisible = false;

        var result = await _apiClient.GetAccountDeletionStatusAsync();

        SetLoading(false);

        if (!result.Success || result.Data == null)
        {
            ShowError(result.ErrorMessage ?? "Couldn't load your account details.");
            return;
        }

        _status = result.Data;
        Render();
    }

    private void Render()
    {
        if (_status == null) return;

        if (_status.IsPending)
        {
            RenderPending(_status);
            return;
        }

        PendingPanel.IsVisible = false;
        RequestPanel.IsVisible = true;

        if (_status.Scope == AccountDeletionScope.Household)
            RenderHouseholdWarning(_status);
        else
            RenderMemberWarning();
    }

    private void RenderPending(AccountDeletionStatusMobile status)
    {
        RequestPanel.IsVisible = false;
        PendingPanel.IsVisible = true;

        var when = status.PurgeAfter?.ToLocalTime().ToString("D") ?? "shortly";

        PendingTitleLabel.Text = status.Scope == AccountDeletionScope.Household
            ? "This household is scheduled for deletion"
            : "Your account is scheduled for deletion";

        PendingDetailLabel.Text = status.Scope == AccountDeletionScope.Household
            ? $"Everything in {Describe(status.HouseholdName)} will be permanently deleted on {when}. "
              + "Until then you can still change your mind."
            : $"Your account will be permanently deleted on {when}. Until then you can still change your mind.";
    }

    private void RenderHouseholdWarning(AccountDeletionStatusMobile status)
    {
        var household = Describe(status.HouseholdName);

        ScopeTitleLabel.Text = "This deletes the whole household";

        ScopeDetailLabel.Text = status.OtherMemberCount > 0
            ? $"You are an admin of {household}, so deleting your account deletes the household itself — "
              + $"including {Members(status.OtherMemberCount)} who will lose access and everything they have added."
            : $"You are an admin of {household}, so deleting your account deletes the household itself.";

        WhatHappensLabel.Text =
            "Everything in the household goes: inventory, shopping lists, recipes, contacts, calendar, "
            + "equipment, vehicles and documents.\n\n"
            + "Nothing is deleted straight away. You have 30 days to change your mind, and simply signing "
            + "back in during that time cancels the deletion.";

        // A household deletion takes data from people who did not ask for it, so the
        // button waits behind a typed phrase rather than a single tap.
        //
        // The phrase falls back to DELETE when the household has no name. A household can
        // be unnamed — the setup wizard does not force one — and gating on a name that
        // does not exist leaves the button permanently disabled with nothing the user
        // could type to satisfy it.
        _confirmationPhrase = string.IsNullOrWhiteSpace(status.HouseholdName)
            ? "DELETE"
            : status.HouseholdName.Trim();

        ConfirmNamePanel.IsVisible = true;
        ConfirmNamePromptLabel.Text = $"Type {_confirmationPhrase} to confirm.";
        ConfirmNameEntry.Placeholder = _confirmationPhrase;
        ConfirmNameEntry.Text = string.Empty;

        DeleteButton.Text = "Delete Household";
        SetDeleteEnabled(false);
    }

    private void RenderMemberWarning()
    {
        ScopeTitleLabel.Text = "This deletes your account";

        ScopeDetailLabel.Text =
            "Your sign-in, your linked accounts and your passkeys are removed. The household carries on "
            + "without you, and what you have added to it stays for the others.";

        WhatHappensLabel.Text =
            "You will be signed out everywhere and will no longer be able to sign in.\n\n"
            + "Nothing is deleted straight away. You have 30 days to change your mind, and simply signing "
            + "back in during that time cancels the deletion.";

        ConfirmNamePanel.IsVisible = false;
        _confirmationPhrase = null;
        DeleteButton.Text = "Delete My Account";
        SetDeleteEnabled(true);
    }

    private void OnConfirmNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (_confirmationPhrase == null) return;

        SetDeleteEnabled(string.Equals(
            e.NewTextValue?.Trim(), _confirmationPhrase, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// Enables the delete button and makes that state visible.
    /// </summary>
    /// <remarks>
    /// The button keeps its filled red background when disabled, so without the opacity
    /// change it looks exactly as tappable as an enabled one — and tapping it appears to
    /// do nothing rather than showing why.
    /// </remarks>
    private void SetDeleteEnabled(bool enabled)
    {
        DeleteButton.IsEnabled = enabled;
        DeleteButton.Opacity = enabled ? 1 : 0.4;
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_status == null) return;

        var isHousehold = _status.Scope == AccountDeletionScope.Household;

        var confirmed = await DisplayAlert(
            isHousehold ? "Delete this household?" : "Delete your account?",
            isHousehold
                ? $"{Describe(_status.HouseholdName)} and everything in it will be deleted in 30 days. "
                  + "Signing back in before then cancels it."
                : "Your account will be deleted in 30 days. Signing back in before then cancels it.",
            isHousehold ? "Delete Household" : "Delete Account",
            "Cancel");

        if (!confirmed) return;

        SetLoading(true);
        var result = await _apiClient.RequestAccountDeletionAsync();
        SetLoading(false);

        if (!result.Success || result.Data == null)
        {
            ShowError(result.ErrorMessage ?? "Couldn't schedule the deletion.");
            return;
        }

        var when = result.Data.PurgeAfter.ToLocalTime().ToString("D");

        await DisplayAlert(
            "Scheduled",
            $"Deletion is scheduled for {when}. Sign in again before then to cancel it.",
            "OK");

        await SignOutAfterRequestAsync();
    }

    /// <summary>
    /// Returns the app to the sign-in screen after a deletion is scheduled.
    /// </summary>
    /// <remarks>
    /// The request already revoked the session server-side, so every stored credential is
    /// dead — leaving them in place would just produce 401s. The push token is
    /// unregistered too: notifications for a household being deleted should stop now, not
    /// keep arriving for the next thirty days. Server config is deliberately kept so the
    /// user can sign straight back in, which is what cancels the deletion.
    /// </remarks>
    private async Task SignOutAfterRequestAsync()
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;

        try
        {
            var pushService = services?.GetService<PushNotificationRegistrationService>();
            if (pushService != null) await pushService.UnregisterAsync();
        }
        catch (Exception ex)
        {
            // Best-effort — a stale push registration must not block the sign-out.
            Console.WriteLine($"[DeleteAccount] Push unregister error: {ex.Message}");
        }

        await _tokenStorage.ClearTokensAsync();
        services?.GetService<TenantStorage>()?.Clear();

        App.TransitionToLogin();
    }

    private async void OnKeepAccountClicked(object? sender, EventArgs e)
    {
        SetLoading(true);
        var result = await _apiClient.CancelAccountDeletionAsync();
        SetLoading(false);

        if (!result.Success)
        {
            ShowError(result.ErrorMessage ?? "Couldn't cancel the deletion.");
            return;
        }

        await DisplayAlert("Cancelled", "Your account is no longer scheduled for deletion.", "OK");
        await LoadStatusAsync();
    }

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private static string Describe(string? householdName)
        => string.IsNullOrWhiteSpace(householdName) ? "your household" : householdName;

    private static string Members(int count)
        => count == 1 ? "1 other member" : $"{count} other members";
}
