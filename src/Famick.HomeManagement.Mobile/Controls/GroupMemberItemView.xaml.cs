using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Pages.Contacts;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class GroupMemberItemView : ContentView
{
    public static readonly BindableProperty GroupIdProperty =
        BindableProperty.Create(nameof(GroupId), typeof(Guid), typeof(GroupMemberItemView), Guid.Empty);

    public Guid GroupId
    {
        get => (Guid)GetValue(GroupIdProperty);
        set => SetValue(GroupIdProperty, value);
    }

    public event EventHandler? DataChanged;

    private readonly ShoppingApiClient _apiClient;

    public GroupMemberItemView()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private ContactDisplayModel? Member => BindingContext as ContactDisplayModel;

    private async void OnTapped(object? sender, EventArgs e)
    {
        var member = Member;
        if (member == null) return;
        await Shell.Current.GoToAsync(nameof(ContactDetailPage), new Dictionary<string, object>
        {
            { "ContactId", member.Id.ToString() }
        });
    }

    private async void OnMoveSwiped(object? sender, EventArgs e)
    {
        var member = Member;
        if (member == null) return;

        var page = Shell.Current.CurrentPage;
        var popup = new MoveToGroupPopup(_apiClient, GroupId == Guid.Empty ? null : GroupId);
        var popupResult = await page.ShowPopupAsync<MoveToGroupResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;

        var result = await _apiClient.MoveContactToGroupAsync(member.Id, popupResult.Result.GroupId);
        if (result.Success)
            DataChanged?.Invoke(this, EventArgs.Empty);
        else
            await page.DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to move contact", "OK");
    }

    private async void OnRemoveSwiped(object? sender, EventArgs e)
    {
        var member = Member;
        if (member == null) return;

        var page = Shell.Current.CurrentPage;
        var confirm = await page.DisplayAlertAsync("Remove Member",
            $"Remove \"{member.DisplayName}\" from this group?", "Remove", "Cancel");
        if (!confirm) return;

        var result = await _apiClient.DeleteContactAsync(member.Id);
        if (result.Success)
            DataChanged?.Invoke(this, EventArgs.Empty);
        else
            await page.DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to remove member", "OK");
    }
}
