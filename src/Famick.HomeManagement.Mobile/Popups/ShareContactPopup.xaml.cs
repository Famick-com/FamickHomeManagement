using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class ShareContactPopup : Popup<ShareContactResult>
{
    private HouseholdMemberDto? _selectedUser;

    public ShareContactPopup(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _ = LoadMembersAsync(apiClient);
    }

    private async Task LoadMembersAsync(ShoppingApiClient apiClient)
    {
        var result = await apiClient.GetHouseholdMembersAsync();
        if (result.Success && result.Data != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UsersCollection.ItemsSource = result.Data;
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                UsersCollection.IsVisible = true;
            });
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
            });
        }
    }

    private void OnUserSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is HouseholdMemberDto user)
        {
            _selectedUser = user;
            SelectedUserLabel.Text = user.DisplayName;
            SelectedUserLabel.IsVisible = true;
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (_selectedUser == null) return;

        await CloseAsync(new ShareContactResult(
            _selectedUser.ContactId,
            _selectedUser.DisplayName,
            CanEditSwitch.IsToggled));
    }
}
