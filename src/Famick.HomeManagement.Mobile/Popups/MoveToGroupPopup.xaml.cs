using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class MoveToGroupPopup : Popup<MoveToGroupResult>
{
    private List<ContactGroupSummaryDto> _groups = new();

    public MoveToGroupPopup(ShoppingApiClient apiClient, Guid? excludeGroupId = null)
    {
        InitializeComponent();
        _ = LoadGroupsAsync(apiClient, excludeGroupId);
    }

    private async Task LoadGroupsAsync(ShoppingApiClient apiClient, Guid? excludeGroupId)
    {
        var result = await apiClient.GetContactGroupsAsync(pageSize: 100);
        if (result.Success && result.Data != null)
        {
            _groups = excludeGroupId.HasValue
                ? result.Data.Items.Where(g => g.Id != excludeGroupId.Value).ToList()
                : result.Data.Items;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                GroupsCollection.ItemsSource = _groups;
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                GroupsCollection.IsVisible = true;
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

    private async void OnGroupSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ContactGroupSummaryDto group)
        {
            await CloseAsync(new MoveToGroupResult(group.Id, group.GroupName));
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);
}
