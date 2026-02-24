using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class AddPhonePopup : Popup<AddPhoneResult>
{
    private static readonly int[] TagValues = { 0, 1, 2, 3, 99 };

    public AddPhonePopup()
    {
        InitializeComponent();
        TagPicker.SelectedIndex = 0;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var phone = PhoneEntry.Text?.Trim();
        if (string.IsNullOrEmpty(phone)) return;

        var tag = TagPicker.SelectedIndex >= 0 && TagPicker.SelectedIndex < TagValues.Length
            ? TagValues[TagPicker.SelectedIndex] : 0;

        await CloseAsync(new AddPhoneResult(phone, tag, PrimarySwitch.IsToggled));
    }
}
