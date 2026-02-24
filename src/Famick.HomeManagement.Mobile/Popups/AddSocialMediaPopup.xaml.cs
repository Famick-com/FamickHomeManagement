using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class AddSocialMediaPopup : Popup<AddSocialMediaResult>
{
    // Maps picker index to SocialMediaService enum values
    private static readonly int[] ServiceValues = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 99 };

    public AddSocialMediaPopup()
    {
        InitializeComponent();
        ServicePicker.SelectedIndex = 0;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(username)) return;

        var service = ServicePicker.SelectedIndex >= 0 && ServicePicker.SelectedIndex < ServiceValues.Length
            ? ServiceValues[ServicePicker.SelectedIndex] : 99;

        await CloseAsync(new AddSocialMediaResult(service, username, ProfileUrlEntry.Text?.Trim()));
    }
}
