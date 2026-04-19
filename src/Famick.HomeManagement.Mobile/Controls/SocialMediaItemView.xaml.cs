using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class SocialMediaItemView : ContentView
{
    public event EventHandler? DataChanged;

    private readonly ShoppingApiClient _apiClient;

    public SocialMediaItemView()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private ContactSocialMediaDto? Social => BindingContext as ContactSocialMediaDto;

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        var social = Social;
        if (social == null) return;
        var confirm = await Shell.Current.CurrentPage.DisplayAlertAsync("Delete", $"Remove {social.ServiceLabel} profile?", "Delete", "Cancel");
        if (!confirm) return;
        var result = await _apiClient.RemoveContactSocialMediaAsync(social.Id);
        if (result.Success) DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        var social = Social;
        if (social == null || string.IsNullOrEmpty(social.ProfileUrl)) return;
        try { await Launcher.OpenAsync(new Uri(social.ProfileUrl)); }
        catch { /* ignore */ }
    }
}
