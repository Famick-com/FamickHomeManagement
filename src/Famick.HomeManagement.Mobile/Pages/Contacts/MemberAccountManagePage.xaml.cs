using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

[QueryProperty(nameof(ContactId), "ContactId")]
public partial class MemberAccountManagePage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private HouseholdMemberDto? _member;
    private ContactDetailDto? _contact;
    private int _currentRoleId = 1; // Default: Editor

    public string ContactId { get; set; } = string.Empty;

    public MemberAccountManagePage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMemberAsync();
    }

    private async Task LoadMemberAsync()
    {
        if (!Guid.TryParse(ContactId, out var contactId)) return;

        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;

        try
        {
            // Load contact and household member info in parallel
            var contactTask = _apiClient.GetContactAsync(contactId);
            var membersTask = _apiClient.GetHouseholdMembersAsync();
            await Task.WhenAll(contactTask, membersTask);

            var contactResult = contactTask.Result;
            var membersResult = membersTask.Result;

            if (contactResult.Success && contactResult.Data != null)
            {
                _contact = contactResult.Data;
            }

            if (membersResult.Success && membersResult.Data != null)
            {
                _member = membersResult.Data.FirstOrDefault(m => m.ContactId == contactId);
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ContentScroll.IsVisible = true;
                RenderMemberInfo();
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                await DisplayAlert("Error", $"Failed to load member: {ex.Message}", "OK");
                await Shell.Current.GoToAsync("..");
            });
        }
    }

    private void RenderMemberInfo()
    {
        if (_member == null) return;

        MemberNameLabel.Text = _member.DisplayName;
        MemberEmailLabel.Text = _member.Email ?? "No email on file";

        if (_member.HasUserAccount)
        {
            AccountStatusBadge.BackgroundColor = Color.FromArgb("#4CAF50");
            AccountStatusLabel.Text = "Account Active";
            RoleSection.IsVisible = true;
            InviteButton.IsVisible = false;
            ResendInviteButton.IsVisible = true;
            ResetPasswordButton.IsVisible = true;
            UpdateRoleButtons();
        }
        else
        {
            AccountStatusBadge.BackgroundColor = Color.FromArgb("#FF9800");
            AccountStatusLabel.Text = "No Account";
            RoleSection.IsVisible = false;
            InviteButton.IsVisible = true;
            ResendInviteButton.IsVisible = false;
            ResetPasswordButton.IsVisible = false;
        }
    }

    private void UpdateRoleButtons()
    {
        var activeColor = Color.FromArgb("#1976D2");
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var inactiveColor = isDark ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");
        var activeTextColor = Colors.White;
        var inactiveTextColor = isDark ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#424242");

        // Role ID 2 = Viewer, Role ID 1 = Editor
        ViewerButton.BackgroundColor = _currentRoleId == 2 ? activeColor : inactiveColor;
        ViewerButton.TextColor = _currentRoleId == 2 ? activeTextColor : inactiveTextColor;
        EditorButton.BackgroundColor = _currentRoleId == 1 ? activeColor : inactiveColor;
        EditorButton.TextColor = _currentRoleId == 1 ? activeTextColor : inactiveTextColor;

        RoleDescriptionLabel.Text = _currentRoleId == 2
            ? "Viewer: Can view all data but cannot make changes."
            : "Editor: Can create, edit, and delete data.";
    }

    private async void OnViewerClicked(object? sender, EventArgs e)
    {
        if (_currentRoleId == 2) return; // Already viewer
        await ChangeRoleAsync(2, "Viewer");
    }

    private async void OnEditorClicked(object? sender, EventArgs e)
    {
        if (_currentRoleId == 1) return; // Already editor
        await ChangeRoleAsync(1, "Editor");
    }

    private async Task ChangeRoleAsync(int roleId, string roleName)
    {
        if (_member?.LinkedUserId == null || _contact == null) return;

        try
        {
            var request = new UpdateUserRoleMobileRequest
            {
                Email = _contact.PrimaryEmail ?? string.Empty,
                FirstName = _contact.FirstName ?? string.Empty,
                LastName = _contact.LastName ?? string.Empty,
                Roles = new List<int> { roleId },
                IsActive = true
            };

            var result = await _apiClient.UpdateUserAsync(_member.LinkedUserId!.Value, request);
            if (result.Success)
            {
                _currentRoleId = roleId;
                UpdateRoleButtons();
                await DisplayAlert("Role Updated",
                    $"{_member.DisplayName}'s role has been changed to {roleName}.", "OK");
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update role.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error: {ex.Message}", "OK");
        }
    }

    private async void OnInviteClicked(object? sender, EventArgs e)
    {
        if (_contact == null) return;

        var popup = new InviteMemberPopup();
        popup.Configure(
            _contact.PrimaryEmail,
            _contact.PhoneNumbers.FirstOrDefault()?.PhoneNumber,
            _contact.DisplayName ?? "Member");
        var popupResult = await this.ShowPopupAsync<InviteMemberResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var inviteResult = popupResult.Result;

        try
        {
            var createRequest = new CreateUserMobileRequest
            {
                Email = inviteResult.Email,
                FirstName = _contact.FirstName ?? string.Empty,
                LastName = _contact.LastName ?? string.Empty,
                Roles = new List<int> { 1 }, // Editor by default
                MustChangePassword = true,
                SendWelcomeEmail = false,
                ContactId = _contact.Id
            };

            var createResult = await _apiClient.CreateUserAsync(createRequest);
            if (!createResult.Success || createResult.Data == null)
            {
                await DisplayAlert("Error", createResult.ErrorMessage ?? "Failed to create account.", "OK");
                return;
            }

            var password = createResult.Data.GeneratedPassword ?? "(password set by admin)";

            try
            {
                var smsMessage = new SmsMessage(
                    $"You've been invited to Famick!\n" +
                    $"Download: https://famick.com/download\n" +
                    $"Email: {inviteResult.Email}\n" +
                    $"Temporary password: {password}\n" +
                    $"You'll need to change your password on first login.",
                    new[] { inviteResult.PhoneNumber });
                await Sms.Default.ComposeAsync(smsMessage);
            }
            catch
            {
                await DisplayAlert("Account Created",
                    $"Account created for {_contact.DisplayName}.\n\n" +
                    $"Email: {inviteResult.Email}\n" +
                    $"Temporary password: {password}\n\n" +
                    $"Please share these credentials manually.",
                    "OK");
            }

            // Refresh the page
            await LoadMemberAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error: {ex.Message}", "OK");
        }
    }

    private async void OnResendInviteClicked(object? sender, EventArgs e)
    {
        if (_member?.LinkedUserId == null || _contact == null) return;

        try
        {
            var result = await _apiClient.AdminResetPasswordAsync(
                _member.LinkedUserId!.Value,
                new AdminResetPasswordMobileRequest { NewPassword = null });

            if (!result.Success || result.Data == null)
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to reset password.", "OK");
                return;
            }

            var password = result.Data.GeneratedPassword ?? "(unknown)";
            var email = _member.Email ?? _contact.PrimaryEmail ?? "";
            var primaryPhone = _contact.PhoneNumbers.FirstOrDefault()?.PhoneNumber;

            if (!string.IsNullOrEmpty(primaryPhone))
            {
                try
                {
                    var smsMessage = new SmsMessage(
                        $"You've been invited to Famick!\n" +
                        $"Download: https://famick.com/download\n" +
                        $"Email: {email}\n" +
                        $"Temporary password: {password}\n" +
                        $"You'll need to change your password on first login.",
                        new[] { primaryPhone });
                    await Sms.Default.ComposeAsync(smsMessage);
                    return;
                }
                catch
                {
                    // Fall through to manual alert
                }
            }

            await DisplayAlert("Invite Details",
                $"Share these credentials with {_contact.DisplayName}:\n\n" +
                $"Email: {email}\n" +
                $"Temporary password: {password}\n\n" +
                $"Download: https://famick.com/download",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error: {ex.Message}", "OK");
        }
    }

    private async void OnResetPasswordClicked(object? sender, EventArgs e)
    {
        if (_member?.LinkedUserId == null || _contact == null) return;

        var confirm = await DisplayAlert("Reset Password",
            $"Reset password for {_member.DisplayName}?", "Reset", "Cancel");
        if (!confirm) return;

        try
        {
            var result = await _apiClient.AdminResetPasswordAsync(
                _member.LinkedUserId!.Value,
                new AdminResetPasswordMobileRequest { NewPassword = null });

            if (!result.Success || result.Data == null)
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to reset password.", "OK");
                return;
            }

            var password = result.Data.GeneratedPassword ?? "(unknown)";
            var primaryPhone = _contact.PhoneNumbers.FirstOrDefault()?.PhoneNumber;

            var sendSms = !string.IsNullOrEmpty(primaryPhone) &&
                await DisplayAlert("Password Reset",
                    $"New password: {password}\n\nSend via SMS to {_member.DisplayName}?",
                    "Send SMS", "Just Copy");

            if (sendSms)
            {
                try
                {
                    var smsMessage = new SmsMessage(
                        $"Your Famick password has been reset.\n" +
                        $"New temporary password: {password}\n" +
                        $"You'll need to change your password on next login.",
                        new[] { primaryPhone! });
                    await Sms.Default.ComposeAsync(smsMessage);
                }
                catch
                {
                    await DisplayAlert("Password Reset",
                        $"New password: {password}\n\nSMS unavailable. Please share manually.",
                        "OK");
                }
            }
            else
            {
                await DisplayAlert("Password Reset",
                    $"New password for {_member.DisplayName}:\n\n{password}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error: {ex.Message}", "OK");
        }
    }
}
