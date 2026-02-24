using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class AddEmailPopup : Popup<AddEmailResult>
{
    private static readonly int[] TagValues = { 0, 1, 2, 99 };

    public AddEmailPopup()
    {
        InitializeComponent();
        TagPicker.SelectedIndex = 0;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim();
        if (string.IsNullOrEmpty(email)) return;

        var tag = TagPicker.SelectedIndex >= 0 && TagPicker.SelectedIndex < TagValues.Length
            ? TagValues[TagPicker.SelectedIndex] : 0;

        await CloseAsync(new AddEmailResult(email, tag, PrimarySwitch.IsToggled, LabelEntry.Text?.Trim()));
    }
}
