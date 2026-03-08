using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class UtilityPopup : Popup<UtilityPopupResult>
{
    private readonly int _editUtilityType;
    private readonly bool _isEditMode;

    public UtilityPopup()
    {
        InitializeComponent();
        TypePicker.SelectedIndex = 0;
    }

    public UtilityPopup(MobileHomeUtilityDto existing) : this()
    {
        _isEditMode = true;
        _editUtilityType = existing.UtilityType;

        TitleLabel.Text = "Edit Utility";
        SaveButton.Text = "Save";

        // Hide type picker in edit mode
        TypeSection.IsVisible = false;

        CompanyEntry.Text = existing.CompanyName;
        AccountEntry.Text = existing.AccountNumber;
        PhoneEntry.Text = existing.PhoneNumber;
        WebsiteEntry.Text = existing.Website;
        EmailEntry.Text = existing.LoginEmail;
        NotesEditor.Text = existing.Notes;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var utilityType = _isEditMode ? _editUtilityType : TypePicker.SelectedIndex;

        if (!_isEditMode && utilityType < 0)
        {
            return;
        }

        await CloseAsync(new UtilityPopupResult(
            utilityType,
            CompanyEntry.Text?.Trim(),
            AccountEntry.Text?.Trim(),
            PhoneEntry.Text?.Trim(),
            WebsiteEntry.Text?.Trim(),
            EmailEntry.Text?.Trim(),
            NotesEditor.Text?.Trim()));
    }
}
