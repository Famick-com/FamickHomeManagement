using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class AddAddressPopup : Popup<AddAddressResult>
{
    private static readonly int[] TagValues = { 0, 1, 2, 3, 4, 99 };

    public AddAddressPopup()
    {
        InitializeComponent();
        TagPicker.SelectedIndex = 0;
    }

    public AddAddressPopup(ShoppingApiClient apiClient) : this()
    {
        // The autocomplete field resolves its own ShoppingApiClient from the
        // DI scope; no explicit wiring needed here.
    }

    public AddAddressPopup(ContactAddressDto existing) : this()
    {
        TitleLabel.Text = "Edit Address";
        SaveButton.Text = "Save";
        PrimarySwitch.IsToggled = existing.IsPrimary;

        var tagIndex = Array.IndexOf(TagValues, existing.Tag);
        if (tagIndex >= 0) TagPicker.SelectedIndex = tagIndex;

        AddressField.InitialAddress = existing.Address;
    }

    public AddAddressPopup(ContactAddressDto existing, ShoppingApiClient apiClient) : this(existing)
    {
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        SaveButton.IsEnabled = false;
        SavingIndicator.IsVisible = true;
        try
        {
            var address = await AddressField.CommitAsync();
            if (address == null) return;

            var tag = TagPicker.SelectedIndex >= 0 && TagPicker.SelectedIndex < TagValues.Length
                ? TagValues[TagPicker.SelectedIndex] : 0;

            await CloseAsync(new AddAddressResult(
                address.AddressLine1,
                address.AddressLine2,
                address.City,
                address.StateProvince,
                address.PostalCode,
                address.Country,
                tag,
                PrimarySwitch.IsToggled,
                address.Id));
        }
        finally
        {
            SavingIndicator.IsVisible = false;
            SaveButton.IsEnabled = true;
        }
    }
}
