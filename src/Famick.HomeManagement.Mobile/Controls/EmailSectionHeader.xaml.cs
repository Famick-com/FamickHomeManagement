using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class EmailSectionHeader : ContentView
{
    public static readonly BindableProperty ContactIdProperty =
        BindableProperty.Create(nameof(ContactId), typeof(Guid), typeof(EmailSectionHeader), Guid.Empty);

    public Guid ContactId
    {
        get => (Guid)GetValue(ContactIdProperty);
        set => SetValue(ContactIdProperty, value);
    }

    public event EventHandler? EmailAdded;

    private readonly ShoppingApiClient _apiClient;

    public EmailSectionHeader()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (ContactId == Guid.Empty) return;
        var page = Shell.Current.CurrentPage;
        var popup = new AddEmailPopup();
        var popupResult = await page.ShowPopupAsync<AddEmailResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactEmailAsync(ContactId, new AddEmailRequest
        {
            Email = result.Email,
            Tag = result.Tag,
            IsPrimary = result.IsPrimary,
            Label = result.Label
        });

        if (apiResult.Success)
            EmailAdded?.Invoke(this, EventArgs.Empty);
        else
            await page.DisplayAlertAsync("Error", apiResult.ErrorMessage ?? "Failed to add email", "OK");
    }
}
