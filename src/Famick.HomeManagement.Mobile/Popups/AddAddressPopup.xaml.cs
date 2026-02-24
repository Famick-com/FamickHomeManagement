using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class AddAddressPopup : Popup<AddAddressResult>
{
    private static readonly int[] TagValues = { 0, 1, 2, 3, 4, 99 };

    private readonly ShoppingApiClient? _apiClient;
    private CancellationTokenSource? _searchCts;
    private Guid? _selectedAddressId;
    private bool _suppressSearch;
    private NormalizedAddressResult? _verifiedAddress;

    public AddAddressPopup()
    {
        InitializeComponent();
        TagPicker.SelectedIndex = 0;
    }

    public AddAddressPopup(ShoppingApiClient apiClient) : this()
    {
        _apiClient = apiClient;
    }

    public AddAddressPopup(ContactAddressDto existing) : this()
    {
        _suppressSearch = true;
        TitleLabel.Text = "Edit Address";
        SaveButton.Text = "Save";
        Line1Entry.Text = existing.Address.AddressLine1;
        Line2Entry.Text = existing.Address.AddressLine2;
        CityEntry.Text = existing.Address.City;
        StateEntry.Text = existing.Address.StateProvince;
        PostalEntry.Text = existing.Address.PostalCode;
        CountryEntry.Text = existing.Address.Country;
        PrimarySwitch.IsToggled = existing.IsPrimary;

        var tagIndex = Array.IndexOf(TagValues, existing.Tag);
        if (tagIndex >= 0) TagPicker.SelectedIndex = tagIndex;
    }

    public AddAddressPopup(ContactAddressDto existing, ShoppingApiClient apiClient) : this(existing)
    {
        _apiClient = apiClient;
    }

    private async void OnLine1TextChanged(object? sender, TextChangedEventArgs e)
    {
        // Don't search in edit mode or when programmatically populating fields
        if (_suppressSearch || _apiClient == null) return;

        // If user is typing after selecting an address, clear the selection
        if (_selectedAddressId.HasValue)
        {
            _selectedAddressId = null;
            SelectedAddressLabel.IsVisible = false;
            SetFieldsReadOnly(false);
            ClearFieldsExceptLine1();
        }

        _searchCts?.Cancel();

        var query = e.NewTextValue?.Trim();
        if (string.IsNullOrEmpty(query) || query.Length < 2)
        {
            SearchResultsView.IsVisible = false;
            SearchResultsView.ItemsSource = null;
            return;
        }

        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;

            var result = await _apiClient.SearchAddressesAsync(query, 10);
            if (token.IsCancellationRequested) return;

            if (result.Success && result.Data != null && result.Data.Count > 0)
            {
                SearchResultsView.ItemsSource = result.Data;
                SearchResultsView.IsVisible = true;
            }
            else
            {
                SearchResultsView.IsVisible = false;
            }
        }
        catch (TaskCanceledException)
        {
            // Debounce cancelled, expected
        }
    }

    private void OnSearchResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AddressDto address) return;

        _suppressSearch = true;
        _selectedAddressId = address.Id;

        Line1Entry.Text = address.AddressLine1;
        Line2Entry.Text = address.AddressLine2;
        CityEntry.Text = address.City;
        StateEntry.Text = address.StateProvince;
        PostalEntry.Text = address.PostalCode;
        CountryEntry.Text = address.Country;

        _suppressSearch = false;

        SelectedAddressLabel.Text = "Using existing address";
        SelectedAddressLabel.IsVisible = true;
        SearchResultsView.IsVisible = false;

        SetFieldsReadOnly(true);
        Line1Entry.IsReadOnly = true;
    }

    private void ClearFieldsExceptLine1()
    {
        _suppressSearch = true;
        Line2Entry.Text = string.Empty;
        CityEntry.Text = string.Empty;
        StateEntry.Text = string.Empty;
        PostalEntry.Text = string.Empty;
        CountryEntry.Text = string.Empty;
        _suppressSearch = false;
    }

    private void SetFieldsReadOnly(bool readOnly)
    {
        Line2Entry.IsReadOnly = readOnly;
        CityEntry.IsReadOnly = readOnly;
        StateEntry.IsReadOnly = readOnly;
        PostalEntry.IsReadOnly = readOnly;
        CountryEntry.IsReadOnly = readOnly;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Line1Entry.Text) && string.IsNullOrWhiteSpace(CityEntry.Text))
            return;

        // If an existing address was selected, skip verification
        if (_selectedAddressId.HasValue)
        {
            await SaveAndClose();
            return;
        }

        // For new addresses, verify via Geoapify if API client is available
        if (_apiClient != null)
        {
            await VerifyAddress();
            return;
        }

        // No API client (shouldn't happen in normal flow), save directly
        await SaveAndClose();
    }

    private async Task VerifyAddress()
    {
        VerifyingIndicator.IsVisible = true;
        SaveButton.IsEnabled = false;

        try
        {
            var request = new NormalizeAddressRequest
            {
                AddressLine1 = Line1Entry.Text?.Trim(),
                AddressLine2 = Line2Entry.Text?.Trim(),
                City = CityEntry.Text?.Trim(),
                StateProvince = StateEntry.Text?.Trim(),
                PostalCode = PostalEntry.Text?.Trim(),
                Country = CountryEntry.Text?.Trim()
            };

            var result = await _apiClient!.NormalizeSuggestionsAsync(request, 1);

            if (result.Success && result.Data != null && result.Data.Count > 0)
            {
                _verifiedAddress = result.Data[0];
                ShowVerificationView();
            }
            else
            {
                // No suggestions found, save with original address
                await SaveAndClose();
            }
        }
        catch
        {
            // Verification failed, save with original address
            await SaveAndClose();
        }
        finally
        {
            VerifyingIndicator.IsVisible = false;
            SaveButton.IsEnabled = true;
        }
    }

    private void ShowVerificationView()
    {
        if (_verifiedAddress == null) return;

        // Build display text for verified address
        var verifiedParts = new List<string>();
        if (!string.IsNullOrEmpty(_verifiedAddress.AddressLine1)) verifiedParts.Add(_verifiedAddress.AddressLine1);
        if (!string.IsNullOrEmpty(_verifiedAddress.AddressLine2)) verifiedParts.Add(_verifiedAddress.AddressLine2);
        var verifiedCityLine = string.Join(", ",
            new[] { _verifiedAddress.City, _verifiedAddress.StateProvince, _verifiedAddress.PostalCode }
                .Where(s => !string.IsNullOrEmpty(s)));
        if (!string.IsNullOrEmpty(verifiedCityLine)) verifiedParts.Add(verifiedCityLine);
        if (!string.IsNullOrEmpty(_verifiedAddress.Country)) verifiedParts.Add(_verifiedAddress.Country);
        VerifiedAddressText.Text = string.Join("\n", verifiedParts);

        // Build display text for original address
        var originalParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Line1Entry.Text)) originalParts.Add(Line1Entry.Text.Trim());
        if (!string.IsNullOrWhiteSpace(Line2Entry.Text)) originalParts.Add(Line2Entry.Text.Trim());
        var originalCityLine = string.Join(", ",
            new[] { CityEntry.Text?.Trim(), StateEntry.Text?.Trim(), PostalEntry.Text?.Trim() }
                .Where(s => !string.IsNullOrEmpty(s)));
        if (!string.IsNullOrEmpty(originalCityLine)) originalParts.Add(originalCityLine);
        if (!string.IsNullOrWhiteSpace(CountryEntry.Text)) originalParts.Add(CountryEntry.Text.Trim());
        OriginalAddressText.Text = string.Join("\n", originalParts);

        FormSection.IsVisible = false;
        VerificationSection.IsVisible = true;
    }

    private void OnVerificationBackClicked(object? sender, EventArgs e)
    {
        _verifiedAddress = null;
        VerificationSection.IsVisible = false;
        FormSection.IsVisible = true;
    }

    private async void OnKeepOriginalClicked(object? sender, EventArgs e)
    {
        _verifiedAddress = null;
        await SaveAndClose();
    }

    private async void OnUseVerifiedClicked(object? sender, EventArgs e)
    {
        if (_verifiedAddress == null) return;

        // Update the form fields with verified data
        _suppressSearch = true;
        Line1Entry.Text = _verifiedAddress.AddressLine1;
        Line2Entry.Text = _verifiedAddress.AddressLine2;
        CityEntry.Text = _verifiedAddress.City;
        StateEntry.Text = _verifiedAddress.StateProvince;
        PostalEntry.Text = _verifiedAddress.PostalCode;
        CountryEntry.Text = _verifiedAddress.Country;
        _suppressSearch = false;

        await SaveAndClose();
    }

    private async Task SaveAndClose()
    {
        var tag = TagPicker.SelectedIndex >= 0 && TagPicker.SelectedIndex < TagValues.Length
            ? TagValues[TagPicker.SelectedIndex] : 0;

        await CloseAsync(new AddAddressResult(
            Line1Entry.Text?.Trim(),
            Line2Entry.Text?.Trim(),
            CityEntry.Text?.Trim(),
            StateEntry.Text?.Trim(),
            PostalEntry.Text?.Trim(),
            CountryEntry.Text?.Trim(),
            tag,
            PrimarySwitch.IsToggled,
            _selectedAddressId));
    }
}
