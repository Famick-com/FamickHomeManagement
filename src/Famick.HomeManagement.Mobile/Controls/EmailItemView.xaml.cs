using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class EmailItemView : ContentView
{
    public event EventHandler? DataChanged;

    private readonly ShoppingApiClient _apiClient;

    public EmailItemView()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private ContactEmailAddressDto? Email => BindingContext as ContactEmailAddressDto;

    private async void OnSetPrimarySwiped(object? sender, EventArgs e)
    {
        var email = Email;
        if (email == null) return;
        var result = await _apiClient.SetPrimaryEmailAsync(email.ContactId, email.Id);
        if (result.Success) DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        var email = Email;
        if (email == null) return;
        var confirm = await Shell.Current.CurrentPage.DisplayAlertAsync("Delete", $"Delete {email.Email}?", "Delete", "Cancel");
        if (!confirm) return;
        var result = await _apiClient.RemoveContactEmailAsync(email.Id);
        if (result.Success) DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var email = Email;
        if (email == null) return;
        try
        {
            var message = new EmailMessage { To = new List<string> { email.Email } };
            await Microsoft.Maui.ApplicationModel.Communication.Email.Default.ComposeAsync(message);
        }
        catch { await Shell.Current.CurrentPage.DisplayAlertAsync("Error", "Cannot open email client", "OK"); }
    }
}
