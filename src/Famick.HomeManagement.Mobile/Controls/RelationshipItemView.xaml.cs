using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Pages.Contacts;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class RelationshipItemView : ContentView
{
    public event EventHandler? RelationshipDeleted;

    private readonly ShoppingApiClient _apiClient;

    public RelationshipItemView()
    {
        InitializeComponent();
        _apiClient = IPlatformApplication.Current!.Services.GetRequiredService<ShoppingApiClient>();
    }

    private async void OnContactTapped(object? sender, EventArgs e)
    {
        if (BindingContext is not ContactRelationshipDto rel) return;
        await Shell.Current.GoToAsync(nameof(ContactDetailPage), new Dictionary<string, object>
        {
            { "ContactId", rel.TargetContactId.ToString() }
        });
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        if (BindingContext is not ContactRelationshipDto rel) return;
        var confirm = await Shell.Current.CurrentPage.DisplayAlert("Delete", "Remove this relationship?", "Delete", "Cancel");
        if (!confirm) return;
        var result = await _apiClient.RemoveContactRelationshipAsync(rel.Id);
        if (result.Success)
            RelationshipDeleted?.Invoke(this, EventArgs.Empty);
        else
            await Shell.Current.CurrentPage.DisplayAlert("Error", result.ErrorMessage ?? "Failed to remove relationship", "OK");
    }
}
