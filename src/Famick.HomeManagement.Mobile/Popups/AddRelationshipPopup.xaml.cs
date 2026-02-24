using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class AddRelationshipPopup : Popup<AddRelationshipResult>
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private ContactSummaryDto? _selectedContact;

    // Maps picker index to RelationshipType enum values
    private static readonly int[] TypeValues =
    {
        1, 2, 3, 4, 5, 6, // Mother,Father,Parent,Daughter,Son,Child
        10, 11, 12,        // Sister,Brother,Sibling
        50, 51,            // Spouse,Partner
        80, 81, 70,        // Friend,Neighbor,Colleague
        99                 // Other
    };

    public AddRelationshipPopup(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        TypePicker.SelectedIndex = 0;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Dispose();
        var query = e.NewTextValue?.Trim();
        if (string.IsNullOrEmpty(query) || query.Length < 2)
        {
            SearchResults.IsVisible = false;
            return;
        }

        _searchDebounceTimer = new Timer(async _ =>
        {
            var result = await _apiClient.SearchContactsAsync(query);
            if (result.Success && result.Data != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SearchResults.ItemsSource = result.Data;
                    SearchResults.IsVisible = result.Data.Count > 0;
                });
            }
        }, null, 400, Timeout.Infinite);
    }

    private void OnContactSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ContactSummaryDto contact) return;
        _selectedContact = contact;
        SelectedContactLabel.Text = contact.DisplayName;
        SelectedContactLabel.IsVisible = true;
        SearchResults.IsVisible = false;
        SearchEntry.Text = string.Empty;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (_selectedContact == null) return;

        var typeIndex = TypePicker.SelectedIndex;
        var relType = typeIndex >= 0 && typeIndex < TypeValues.Length
            ? TypeValues[typeIndex] : 99;

        await CloseAsync(new AddRelationshipResult(
            _selectedContact.Id,
            _selectedContact.DisplayName ?? "",
            relType,
            CustomLabelEntry.Text?.Trim(),
            InverseSwitch.IsToggled));
    }
}
