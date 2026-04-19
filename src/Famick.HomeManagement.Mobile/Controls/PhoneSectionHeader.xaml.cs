using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class PhoneSectionHeader : ContentView
{
    public static readonly BindableProperty ContactIdProperty =
        BindableProperty.Create(nameof(ContactId), typeof(Guid), typeof(PhoneSectionHeader), Guid.Empty);

    public Guid ContactId
    {
        get => (Guid)GetValue(ContactIdProperty);
        set => SetValue(ContactIdProperty, value);
    }

    public event EventHandler? PhoneAdded;

    private readonly ShoppingApiClient _apiClient;

    public PhoneSectionHeader()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (ContactId == Guid.Empty) return;
        var page = Shell.Current.CurrentPage;
        var popup = new AddPhonePopup();
        var popupResult = await page.ShowPopupAsync<AddPhoneResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactPhoneAsync(ContactId, new AddPhoneRequest
        {
            PhoneNumber = result.PhoneNumber,
            Tag = result.Tag,
            IsPrimary = result.IsPrimary
        });

        if (apiResult.Success)
            PhoneAdded?.Invoke(this, EventArgs.Empty);
        else
            await page.DisplayAlertAsync("Error", apiResult.ErrorMessage ?? "Failed to add phone", "OK");
    }
}
