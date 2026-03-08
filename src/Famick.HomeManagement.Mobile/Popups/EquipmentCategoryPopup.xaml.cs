using CommunityToolkit.Maui.Views;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class EquipmentCategoryPopup : Popup<EquipmentCategoryPopupResult>
{
    public EquipmentCategoryPopup()
    {
        InitializeComponent();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        await CloseAsync(new EquipmentCategoryPopupResult(
            name,
            DescriptionEditor.Text?.Trim()));
    }
}

public record EquipmentCategoryPopupResult(
    string Name,
    string? Description);
