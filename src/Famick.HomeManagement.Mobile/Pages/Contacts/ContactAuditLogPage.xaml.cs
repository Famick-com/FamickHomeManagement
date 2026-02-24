using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

[QueryProperty(nameof(ContactId), "ContactId")]
public partial class ContactAuditLogPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private string _contactId = string.Empty;

    public string ContactId
    {
        get => _contactId;
        set
        {
            _contactId = value;
            _ = LoadAuditLogAsync();
        }
    }

    public ContactAuditLogPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    private async Task LoadAuditLogAsync()
    {
        if (!Guid.TryParse(_contactId, out var id)) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            RefreshContainer.IsVisible = false;
            EmptyLabel.IsVisible = false;
        });

        try
        {
            var result = await _apiClient.GetContactAuditLogAsync(id);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;

                if (result.Success && result.Data != null && result.Data.Count > 0)
                {
                    AuditCollection.ItemsSource = result.Data;
                    RefreshContainer.IsVisible = true;
                }
                else
                {
                    EmptyLabel.IsVisible = true;
                }
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                EmptyLabel.Text = $"Error: {ex.Message}";
                EmptyLabel.IsVisible = true;
            });
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAuditLogAsync();
        RefreshContainer.IsRefreshing = false;
    }
}
