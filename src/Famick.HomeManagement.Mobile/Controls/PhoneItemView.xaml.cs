using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class PhoneItemView : ContentView
{
    public event EventHandler? DataChanged;

    private readonly ShoppingApiClient _apiClient;

    public PhoneItemView()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private ContactPhoneNumberDto? Phone => BindingContext as ContactPhoneNumberDto;

    private async void OnSetPrimarySwiped(object? sender, EventArgs e)
    {
        var phone = Phone;
        if (phone == null) return;
        var result = await _apiClient.SetPrimaryPhoneAsync(phone.ContactId, phone.Id);
        if (result.Success) DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        var phone = Phone;
        if (phone == null) return;
        var confirm = await Shell.Current.CurrentPage.DisplayAlertAsync("Delete", $"Delete {phone.PhoneNumber}?", "Delete", "Cancel");
        if (!confirm) return;
        var result = await _apiClient.RemoveContactPhoneAsync(phone.Id);
        if (result.Success) DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnCallClicked(object? sender, EventArgs e)
    {
        var phone = Phone;
        if (phone == null) return;
        try { PhoneDialer.Default.Open(phone.PhoneNumber); }
        catch { await Shell.Current.CurrentPage.DisplayAlertAsync("Error", "Cannot open phone dialer", "OK"); }
    }

    private async void OnTextClicked(object? sender, EventArgs e)
    {
        var phone = Phone;
        if (phone == null) return;
        try { await Sms.Default.ComposeAsync(new SmsMessage("", new[] { phone.PhoneNumber })); }
        catch { await Shell.Current.CurrentPage.DisplayAlertAsync("Error", "Cannot open messaging", "OK"); }
    }
}
