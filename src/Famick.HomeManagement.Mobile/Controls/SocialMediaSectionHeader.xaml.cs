using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class SocialMediaSectionHeader : ContentView
{
    public static readonly BindableProperty ContactIdProperty =
        BindableProperty.Create(nameof(ContactId), typeof(Guid), typeof(SocialMediaSectionHeader), Guid.Empty);

    public Guid ContactId
    {
        get => (Guid)GetValue(ContactIdProperty);
        set => SetValue(ContactIdProperty, value);
    }

    public event EventHandler? SocialAdded;

    private readonly ShoppingApiClient _apiClient;

    public SocialMediaSectionHeader()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (ContactId == Guid.Empty) return;
        var page = Shell.Current.CurrentPage;
        var popup = new AddSocialMediaPopup();
        var popupResult = await page.ShowPopupAsync<AddSocialMediaResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactSocialMediaAsync(ContactId, new AddSocialMediaRequest
        {
            Service = result.Service,
            Username = result.Username,
            ProfileUrl = result.ProfileUrl
        });

        if (apiResult.Success)
            SocialAdded?.Invoke(this, EventArgs.Empty);
        else
            await page.DisplayAlertAsync("Error", apiResult.ErrorMessage ?? "Failed to add social media", "OK");
    }
}
