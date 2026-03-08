using CommunityToolkit.Maui.Views;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class EquipmentUsageLogPopup : Popup<EquipmentUsageLogPopupResult>
{
    public EquipmentUsageLogPopup(string usageUnit)
    {
        InitializeComponent();
        TitleLabel.Text = $"Add {usageUnit} Reading";
        ReadingLabel.Text = $"Reading ({usageUnit}) *";
        DatePicker.Date = DateTime.Now.Date;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReadingEntry.Text) || !decimal.TryParse(ReadingEntry.Text, out var reading))
            return;

        var pickedDate = DatePicker.Date ?? DateTime.Now.Date;
        var date = new DateTime(pickedDate.Year, pickedDate.Month, pickedDate.Day,
            0, 0, 0, DateTimeKind.Local).ToUniversalTime();

        await CloseAsync(new EquipmentUsageLogPopupResult(
            date,
            reading,
            NotesEditor.Text?.Trim()));
    }
}

public record EquipmentUsageLogPopupResult(
    DateTime Date,
    decimal Reading,
    string? Notes);
