using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class AddressSectionHeader : ContentView
{
    public static readonly BindableProperty ContactIdProperty =
        BindableProperty.Create(nameof(ContactId), typeof(Guid), typeof(AddressSectionHeader), Guid.Empty);

    public Guid ContactId
    {
        get => (Guid)GetValue(ContactIdProperty);
        set => SetValue(ContactIdProperty, value);
    }

    public event EventHandler? AddressAdded;

    private readonly ShoppingApiClient _apiClient;

    public AddressSectionHeader()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (ContactId == Guid.Empty) return;
        var page = Shell.Current.CurrentPage;
        var popup = new AddAddressPopup(_apiClient);
        var popupResult = await page.ShowPopupAsync<AddAddressResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactAddressAsync(ContactId, new AddContactAddressRequest
        {
            AddressId = result.AddressId,
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
            AddressAdded?.Invoke(this, EventArgs.Empty);
        else
            await page.DisplayAlertAsync("Error", apiResult.ErrorMessage ?? "Failed to add address", "OK");
    }
}
