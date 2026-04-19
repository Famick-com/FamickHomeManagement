using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class AddressItemView : ContentView
{
    public event EventHandler? DataChanged;

    private readonly ShoppingApiClient _apiClient;

    public AddressItemView()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private ContactAddressDto? Address => BindingContext as ContactAddressDto;

    private async void OnEditSwiped(object? sender, EventArgs e)
    {
        var addr = Address;
        if (addr == null) return;
        var page = Shell.Current.CurrentPage;
        var popup = new AddAddressPopup(addr, _apiClient);
        var popupResult = await page.ShowPopupAsync<AddAddressResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.UpdateContactAddressAsync(addr.ContactId, addr.Id, new AddContactAddressRequest
        {
            AddressLine1 = result.AddressLine1,
            AddressLine2 = result.AddressLine2,
            City = result.City,
            StateProvince = result.StateProvince,
            PostalCode = result.PostalCode,
            Country = result.Country,
            Tag = result.Tag,
            IsPrimary = result.IsPrimary
        });

        if (apiResult.Success)
            DataChanged?.Invoke(this, EventArgs.Empty);
        else
            await page.DisplayAlertAsync("Error", apiResult.ErrorMessage ?? "Failed to update address", "OK");
    }

    private async void OnSetPrimarySwiped(object? sender, EventArgs e)
    {
        var addr = Address;
        if (addr == null || addr.IsPrimary) return;
        var result = await _apiClient.SetPrimaryAddressAsync(addr.ContactId, addr.Id);
        if (result.Success) DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        var addr = Address;
        if (addr == null) return;
        var confirm = await Shell.Current.CurrentPage.DisplayAlertAsync("Delete", "Delete this address?", "Delete", "Cancel");
        if (!confirm) return;
        var result = await _apiClient.RemoveContactAddressAsync(addr.ContactId, addr.Id);
        if (result.Success)
            DataChanged?.Invoke(this, EventArgs.Empty);
        else
            await Shell.Current.CurrentPage.DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to delete address", "OK");
    }

    private async void OnTapped(object? sender, EventArgs e)
    {
        var addr = Address;
        if (addr == null) return;
        if (addr.Address.Latitude != null && addr.Address.Longitude != null)
        {
            await Map.Default.OpenAsync(addr.Address.Latitude.Value, addr.Address.Longitude.Value,
                new MapLaunchOptions { Name = addr.Address.DisplayAddress });
        }
        else if (!string.IsNullOrEmpty(addr.Address.DisplayAddress))
        {
            await Map.Default.OpenAsync(new Placemark
            {
                Thoroughfare = addr.Address.AddressLine1,
                Locality = addr.Address.City,
                AdminArea = addr.Address.StateProvince,
                PostalCode = addr.Address.PostalCode,
                CountryName = addr.Address.Country
            }, new MapLaunchOptions());
        }
    }
}
