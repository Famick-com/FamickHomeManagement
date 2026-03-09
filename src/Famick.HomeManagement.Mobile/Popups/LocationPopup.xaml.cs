using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class LocationPopup : Popup<LocationPopupResult>
{
    public LocationPopup()
    {
        InitializeComponent();
    }

    public LocationPopup(LocationDto existing) : this()
    {
        TitleLabel.Text = "Edit Location";
        SaveButton.Text = "Save";

        NameEntry.Text = existing.Name;
        DescriptionEntry.Text = existing.Description;
        SortOrderEntry.Text = existing.SortOrder.ToString();
        ActiveSwitch.IsToggled = existing.IsActive;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        int.TryParse(SortOrderEntry.Text?.Trim(), out var sortOrder);

        await CloseAsync(new LocationPopupResult(
            name,
            DescriptionEntry.Text?.Trim(),
            sortOrder,
            ActiveSwitch.IsToggled));
    }
}
