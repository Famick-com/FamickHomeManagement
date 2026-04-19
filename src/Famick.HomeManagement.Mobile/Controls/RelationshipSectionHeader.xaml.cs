using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class RelationshipSectionHeader : ContentView
{
    public static readonly BindableProperty ContactIdProperty =
        BindableProperty.Create(nameof(ContactId), typeof(Guid), typeof(RelationshipSectionHeader), Guid.Empty);

    public Guid ContactId
    {
        get => (Guid)GetValue(ContactIdProperty);
        set => SetValue(ContactIdProperty, value);
    }

    public event EventHandler? RelationshipAdded;

    private readonly ShoppingApiClient _apiClient;

    public RelationshipSectionHeader()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (ContactId == Guid.Empty) return;
        var page = Shell.Current.CurrentPage;
        var popup = new AddRelationshipPopup(_apiClient);
        var popupResult = await page.ShowPopupAsync<AddRelationshipResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactRelationshipAsync(ContactId, new AddRelationshipRequest
        {
            TargetContactId = result.TargetContactId,
            RelationshipType = result.RelationshipType,
            CustomLabel = result.CustomLabel,
            CreateInverse = result.CreateInverse
        });

        if (apiResult.Success)
            RelationshipAdded?.Invoke(this, EventArgs.Empty);
        else
            await page.DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add relationship", "OK");
    }
}
