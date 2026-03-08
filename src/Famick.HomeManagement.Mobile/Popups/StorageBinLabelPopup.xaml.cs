using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class StorageBinLabelPopup : Popup<StorageBinLabelPopupResult>
{
    private readonly List<Guid>? _binIds;

    public StorageBinLabelPopup(List<Guid>? binIds = null)
    {
        InitializeComponent();
        _binIds = binIds;
        FormatPicker.SelectedIndex = 0;

        if (_binIds != null && _binIds.Count > 0)
        {
            RepeatSection.IsVisible = true;
            InfoLabel.Text = $"Printing labels for {_binIds.Count} bin(s)";
        }
        else
        {
            InfoLabel.Text = "New blank labels will be created";
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnPrintClicked(object? sender, EventArgs e)
    {
        if (!int.TryParse(SheetCountEntry.Text?.Trim(), out var sheetCount) || sheetCount < 1 || sheetCount > 10)
        {
            SheetCountEntry.Text = "1";
            return;
        }

        await CloseAsync(new StorageBinLabelPopupResult(
            sheetCount,
            FormatPicker.SelectedIndex,
            RepeatSwitch.IsToggled,
            _binIds));
    }
}
