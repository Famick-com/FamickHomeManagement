using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class SearchContactPopup : Popup<ContactSummaryDto>
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;

    public SearchContactPopup(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new Timer(_ =>
        {
            var query = e.NewTextValue?.Trim() ?? string.Empty;
            if (query.Length < 2)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ResultsCollection.IsVisible = false;
                    EmptyLabel.Text = "Type to search contacts";
                    EmptyLabel.IsVisible = true;
                });
                return;
            }
            _ = SearchAsync(query);
        }, null, 400, Timeout.Infinite);
    }

    private async Task SearchAsync(string query)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            EmptyLabel.IsVisible = false;
        });

        var result = await _apiClient.SearchContactsAsync(query, 20);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;

            if (result.Success && result.Data?.Count > 0)
            {
                // Filter out groups, show only individual contacts
                var contacts = result.Data.Where(c => !c.IsGroup).ToList();
                if (contacts.Count > 0)
                {
                    ResultsCollection.ItemsSource = contacts;
                    ResultsCollection.IsVisible = true;
                    EmptyLabel.IsVisible = false;
                }
                else
                {
                    ResultsCollection.IsVisible = false;
                    EmptyLabel.Text = "No contacts found";
                    EmptyLabel.IsVisible = true;
                }
            }
            else
            {
                ResultsCollection.IsVisible = false;
                EmptyLabel.Text = "No contacts found";
                EmptyLabel.IsVisible = true;
            }
        });
    }

    private async void OnContactSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ContactSummaryDto contact)
        {
            await CloseAsync(contact);
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);
}
