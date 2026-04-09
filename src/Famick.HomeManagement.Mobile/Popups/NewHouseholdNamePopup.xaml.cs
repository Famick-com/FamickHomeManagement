using CommunityToolkit.Maui.Views;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class NewHouseholdNamePopup : Popup<string>
{
    public NewHouseholdNamePopup(string suggestedName)
    {
        InitializeComponent();
        NameEntry.Text = suggestedName;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        await CloseAsync(name);
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);
}
