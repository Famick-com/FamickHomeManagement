using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Wizard;

public partial class WizardMembersPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private List<HouseholdMemberDto> _members = new();

    public WizardMembersPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        SetLoading(true);
        try
        {
            var result = await _apiClient.GetHouseholdMembersAsync();
            if (result.Success && result.Data != null)
            {
                _members = result.Data;
                var currentUser = _members.FirstOrDefault(m => m.IsCurrentUser);
                var others = _members.Where(m => !m.IsCurrentUser).ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (currentUser != null)
                    {
                        MyFirstNameEntry.Text = currentUser.FirstName;
                        MyLastNameEntry.Text = currentUser.LastName;
                    }
                    RenderMembersList(others);
                });
            }
        }
        catch
        {
            // Continue without pre-fill
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnSaveMyInfoClicked(object? sender, EventArgs e)
    {
        var firstName = MyFirstNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(firstName))
        {
            ShowError("Please enter your first name.");
            return;
        }

        SetLoading(true);
        HideError();
        try
        {
            var request = new SaveCurrentUserContactRequest
            {
                FirstName = firstName,
                LastName = MyLastNameEntry.Text?.Trim()
            };
            var result = await _apiClient.SaveCurrentUserContactAsync(request);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage ?? "Failed to save.");
                return;
            }
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnShowAddMemberClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new WizardAddMemberPage(_apiClient));
    }

    private void RenderMembersList(List<HouseholdMemberDto> others)
    {
        MembersListLayout.Children.Clear();
        EmptyMembersLabel.IsVisible = others.Count == 0;

        foreach (var member in others)
        {
            var border = new Border
            {
                Padding = new Thickness(10),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Margin = new Thickness(0, 4),
                BackgroundColor = Colors.Transparent
            };
            border.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#E0E0E0"), Color.FromArgb("#444444"));

            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var nameLabel = new Label { Text = member.DisplayName, FontAttributes = FontAttributes.Bold };
            nameLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            var relLabel = new Label { Text = member.RelationshipType, FontSize = 12 };
            relLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#999999"));

            // Account status indicator
            var statusLabel = new Label
            {
                Text = member.HasUserAccount ? "Has account" : "No account",
                FontSize = 11
            };
            if (member.HasUserAccount)
            {
                statusLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#4CAF50"), Color.FromArgb("#81C784"));
            }
            else
            {
                statusLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#9E9E9E"), Color.FromArgb("#757575"));
            }

            var stack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
            stack.Children.Add(nameLabel);
            stack.Children.Add(relLabel);
            stack.Children.Add(statusLabel);
            Grid.SetColumn(stack, 0);

            var deleteBtn = new Button
            {
                Text = "Remove",
                FontSize = 12,
                HeightRequest = 32,
                Padding = new Thickness(8, 0),
                BackgroundColor = Colors.Transparent,
                VerticalOptions = LayoutOptions.Center
            };
            deleteBtn.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#E53935"), Color.FromArgb("#EF5350"));
            var capturedMember = member;
            deleteBtn.Clicked += async (s, ev) => await DeleteMemberAsync(capturedMember);
            Grid.SetColumn(deleteBtn, 1);

            grid.Children.Add(stack);
            grid.Children.Add(deleteBtn);
            border.Content = grid;

            // Add tap gesture for member management
            var tapGesture = new TapGestureRecognizer();
            var tappedMember = member;
            tapGesture.Tapped += async (s, ev) => await OnMemberTappedAsync(tappedMember);
            border.GestureRecognizers.Add(tapGesture);

            MembersListLayout.Children.Add(border);
        }
    }

    private async Task OnMemberTappedAsync(HouseholdMemberDto member)
    {
        if (member.IsCurrentUser) return;

        if (member.HasUserAccount)
        {
            var action = await DisplayActionSheet(
                member.DisplayName, "Cancel", null,
                "Reset Password", "Change Role");

            switch (action)
            {
                case "Reset Password":
                    await ResetPasswordAsync(member);
                    break;
                case "Change Role":
                    await ChangeRoleAsync(member);
                    break;
            }
        }
        else
        {
            var action = await DisplayActionSheet(
                member.DisplayName, "Cancel", null,
                "Create Account & Send Invite");

            if (action == "Create Account & Send Invite")
            {
                await InviteMemberAsync(member);
            }
        }
    }

    private async Task InviteMemberAsync(HouseholdMemberDto member)
    {
        var popup = new InviteMemberPopup();
        popup.Configure(member.Email, member.PhoneNumber, member.DisplayName);
        var popupResult = await this.ShowPopupAsync<InviteMemberResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var inviteResult = popupResult.Result;

        SetLoading(true);
        try
        {
            var createRequest = new CreateUserMobileRequest
            {
                Email = inviteResult.Email,
                FirstName = member.FirstName,
                LastName = member.LastName ?? string.Empty,
                Roles = new List<int> { 1 }, // Editor
                MustChangePassword = true,
                SendWelcomeEmail = false,
                ContactId = member.ContactId
            };

            var createResult = await _apiClient.CreateUserAsync(createRequest);
            if (!createResult.Success || createResult.Data == null)
            {
                ShowError(createResult.ErrorMessage ?? "Failed to create account.");
                return;
            }

            var password = createResult.Data.GeneratedPassword ?? "(password set by admin)";

            // Try to open SMS composer
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
                // SMS not available (e.g. simulator) — show credentials in dialog
                await DisplayAlert("Account Created",
                    $"Account created for {member.DisplayName}.\n\n" +
                    $"Email: {inviteResult.Email}\n" +
                    $"Temporary password: {password}\n\n" +
                    $"Please share these credentials with them manually.",
                    "OK");
            }

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task ResetPasswordAsync(HouseholdMemberDto member)
    {
        if (!member.LinkedUserId.HasValue) return;

        var confirm = await DisplayAlert("Reset Password",
            $"Reset password for {member.DisplayName}?", "Reset", "Cancel");
        if (!confirm) return;

        SetLoading(true);
        try
        {
            var result = await _apiClient.AdminResetPasswordAsync(
                member.LinkedUserId.Value,
                new AdminResetPasswordMobileRequest { NewPassword = null });

            if (!result.Success || result.Data == null)
            {
                ShowError(result.ErrorMessage ?? "Failed to reset password.");
                return;
            }

            var password = result.Data.GeneratedPassword ?? "(unknown)";

            // Offer to send via SMS
            var sendSms = !string.IsNullOrEmpty(member.PhoneNumber) &&
                await DisplayAlert("Password Reset",
                    $"New password: {password}\n\nSend via SMS to {member.DisplayName}?",
                    "Send SMS", "Just Copy");

            if (sendSms)
            {
                try
                {
                    var smsMessage = new SmsMessage(
                        $"Your Famick password has been reset.\n" +
                        $"New temporary password: {password}\n" +
                        $"You'll need to change your password on next login.",
                        new[] { member.PhoneNumber! });
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
                    $"New password for {member.DisplayName}:\n\n{password}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task ChangeRoleAsync(HouseholdMemberDto member)
    {
        if (!member.LinkedUserId.HasValue) return;

        var action = await DisplayActionSheet(
            $"Change role for {member.DisplayName}",
            "Cancel", null,
            "Viewer (read-only)", "Editor (can edit)");

        List<int>? roles = action switch
        {
            "Viewer (read-only)" => new List<int> { 2 }, // Viewer
            "Editor (can edit)" => new List<int> { 1 },   // Editor
            _ => null
        };

        if (roles == null) return;

        SetLoading(true);
        try
        {
            var request = new UpdateUserRoleMobileRequest
            {
                Email = member.Email ?? string.Empty,
                FirstName = member.FirstName,
                LastName = member.LastName ?? string.Empty,
                Roles = roles,
                IsActive = true
            };

            var result = await _apiClient.UpdateUserAsync(member.LinkedUserId.Value, request);
            if (result.Success)
            {
                await DisplayAlert("Role Updated",
                    $"{member.DisplayName}'s role has been updated.", "OK");
                await LoadDataAsync();
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Failed to update role.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task DeleteMemberAsync(HouseholdMemberDto member)
    {
        var confirm = await DisplayAlertAsync("Remove Member",
            $"Remove {member.DisplayName} from household?", "Remove", "Cancel");
        if (!confirm) return;

        var result = await _apiClient.DeleteHouseholdMemberAsync(member.ContactId);
        if (result.Success)
        {
            await LoadDataAsync();
        }
        else
        {
            ShowError(result.ErrorMessage ?? "Failed to remove member.");
        }
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(
            Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<WizardHomeStatsPage>());
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(
            Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<WizardHomeStatsPage>());
    }

    private void SetLoading(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = isLoading;
            LoadingIndicator.IsRunning = isLoading;
        });
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
        });
    }

    private void HideError()
    {
        MainThread.BeginInvokeOnMainThread(() => ErrorLabel.IsVisible = false);
    }
}
