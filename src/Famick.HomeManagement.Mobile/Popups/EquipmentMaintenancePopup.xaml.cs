using CommunityToolkit.Maui.Views;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class EquipmentMaintenancePopup : Popup<EquipmentMaintenancePopupResult>
{
    public EquipmentMaintenancePopup(string? usageUnit)
    {
        InitializeComponent();
        CompletedDatePicker.Date = DateTime.Now.Date;
        ReminderDatePicker.Date = DateTime.Now.Date.AddMonths(3);

        if (!string.IsNullOrEmpty(usageUnit))
        {
            UsageSection.IsVisible = true;
            UsageLabel.Text = $"Usage at completion ({usageUnit})";
        }
    }

    private void OnReminderToggled(object? sender, ToggledEventArgs e)
    {
        ReminderSection.IsVisible = e.Value;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var description = DescriptionEntry.Text?.Trim();
        if (string.IsNullOrEmpty(description)) return;

        var completedPickedDate = CompletedDatePicker.Date ?? DateTime.Now.Date;
        var completedDate = new DateTime(completedPickedDate.Year, completedPickedDate.Month,
            completedPickedDate.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();

        decimal? usageAtCompletion = null;
        if (UsageSection.IsVisible && !string.IsNullOrWhiteSpace(UsageEntry.Text))
        {
            if (decimal.TryParse(UsageEntry.Text, out var usage))
                usageAtCompletion = usage;
        }

        var createReminder = ReminderSwitch.IsToggled;
        string? reminderName = null;
        DateTime? reminderDueDate = null;

        if (createReminder)
        {
            reminderName = ReminderNameEntry.Text?.Trim();
            if (string.IsNullOrEmpty(reminderName))
                reminderName = $"Follow-up: {description}";

            var reminderPickedDate = ReminderDatePicker.Date ?? DateTime.Now.Date.AddMonths(3);
            reminderDueDate = new DateTime(reminderPickedDate.Year, reminderPickedDate.Month,
                reminderPickedDate.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();
        }

        await CloseAsync(new EquipmentMaintenancePopupResult(
            description,
            completedDate,
            usageAtCompletion,
            NotesEditor.Text?.Trim(),
            createReminder,
            reminderName,
            reminderDueDate));
    }
}

public record EquipmentMaintenancePopupResult(
    string Description,
    DateTime CompletedDate,
    decimal? UsageAtCompletion,
    string? Notes,
    bool CreateReminder,
    string? ReminderName,
    DateTime? ReminderDueDate);
