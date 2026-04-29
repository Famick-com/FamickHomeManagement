using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class AddressAutocompleteField : ContentView
{
    public static readonly BindableProperty SelectedAddressIdProperty = BindableProperty.Create(
        nameof(SelectedAddressId),
        typeof(Guid?),
        typeof(AddressAutocompleteField),
        defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty InitialAddressProperty = BindableProperty.Create(
        nameof(InitialAddress),
        typeof(AddressDto),
        typeof(AddressAutocompleteField),
        propertyChanged: OnInitialAddressChanged);

    public Guid? SelectedAddressId
    {
        get => (Guid?)GetValue(SelectedAddressIdProperty);
        set => SetValue(SelectedAddressIdProperty, value);
    }

    public AddressDto? InitialAddress
    {
        get => (AddressDto?)GetValue(InitialAddressProperty);
        set => SetValue(InitialAddressProperty, value);
    }

    public event EventHandler<AddressDto>? AddressSelected;

    private AddressAutocompleteController? _controller;
    private bool _suppressTextEvents;

    public AddressAutocompleteField()
    {
        InitializeComponent();
        ApplyFieldState(FieldState.Idle);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        EnsureController();
    }

    private void EnsureController()
    {
        if (_controller != null) return;

        var services = Handler?.MauiContext?.Services;
        var apiClient = services?.GetService<ShoppingApiClient>();
        if (apiClient == null) return;

        _controller = new AddressAutocompleteController(new ShoppingApiAddressAutocompleteClient(apiClient));
        _controller.SuggestionsChanged += OnSuggestionsChanged;
        _controller.AddressSelected += OnControllerAddressSelected;
        _controller.ManualEntryUnlocked += OnManualEntryUnlocked;
        _controller.ManualEntryPromptChanged += OnManualEntryPromptChanged;
        _controller.SearchStateChanged += OnSearchStateChanged;

        if (InitialAddress != null)
        {
            ApplyPreload(InitialAddress);
        }
    }

    private static void OnInitialAddressChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AddressAutocompleteField self && newValue is AddressDto address)
            self.ApplyPreload(address);
    }

    private void ApplyPreload(AddressDto address)
    {
        if (_controller == null) return;
        _controller.Preload(address);
        SyncFieldsToUi();
        SelectedAddressId = _controller.SelectedAddressId;
        UpdateSelectedIndicator();
        ApplyFieldState(FieldState.Selected);
    }

    private async void OnLine1TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents || _controller == null) return;
        await _controller.OnLine1Changed(e.NewTextValue);
        if (_controller.SelectedAddressId == null)
            SelectedAddressId = null;
    }

    private void OnLine2TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents || _controller == null) return;
        _controller.Fields.Line2 = e.NewTextValue;
    }

    private void OnCityTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents || _controller == null) return;
        _controller.Fields.City = e.NewTextValue;
    }

    private void OnStateTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents || _controller == null) return;
        _controller.Fields.StateProvince = e.NewTextValue;
    }

    private void OnPostalTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents || _controller == null) return;
        _controller.Fields.PostalCode = e.NewTextValue;
    }

    private void OnCountryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents || _controller == null) return;
        _controller.Fields.Country = e.NewTextValue;
    }

    private async void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_controller == null) return;
        if (e.CurrentSelection.FirstOrDefault() is not AddressSuggestionDto picked) return;

        SuggestionsList.SelectedItem = null;
        await _controller.OnSuggestionSelected(picked.SuggestionId);
    }

    private void OnSuggestionsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_controller == null) return;
            SuggestionsList.ItemsSource = _controller.Suggestions;
            SuggestionsList.IsVisible = _controller.Suggestions.Count > 0;
        });
    }

    private void OnControllerAddressSelected(AddressDto address)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncFieldsToUi();
            SuggestionsList.IsVisible = false;
            UpdateSelectedIndicator();
            ApplyFieldState(FieldState.Selected);
            SelectedAddressId = address.Id;
            AddressSelected?.Invoke(this, address);
        });
    }

    private void OnManualEntryUnlocked()
    {
        MainThread.BeginInvokeOnMainThread(() => ApplyFieldState(FieldState.ManualEntry));
    }

    private void OnManualEntryPromptChanged()
    {
        if (_controller == null) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            NoMatchPrompt.IsVisible = _controller.OfferManualEntryPrompt;
        });
    }

    private void OnNoMatchPromptTapped(object? sender, TappedEventArgs e)
    {
        _controller?.EnableManualEntry();
    }

    private void OnSearchStateChanged()
    {
        if (_controller == null) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var searching = _controller.IsSearching;
            SearchSpinner.IsVisible = searching;
            SearchSpinner.IsRunning = searching;
        });
    }

    private void UpdateSelectedIndicator()
    {
        if (_controller?.SelectedAddressId is null)
        {
            SelectedIndicator.IsVisible = false;
            return;
        }

        SelectedIndicator.Text = _controller.LastSelectedSource switch
        {
            "Local" => "Using saved address",
            "Manual" => "Address saved",
            null => "Address selected",
            _ => "Address verified" // Smarty, Geoapify, or any other provider
        };
        SelectedIndicator.IsVisible = true;
    }

    /// <summary>
    /// Three discrete UI states for the field:
    ///  - <c>Idle</c>: nothing selected yet, secondary fields dimmed.
    ///  - <c>Selected</c>: a suggestion has been resolved — Line 1 and the
    ///    standardized fields are read-only; only Apt/Suite stays editable.
    ///    The user taps "Change" to leave this state.
    ///  - <c>ManualEntry</c>: no autocomplete match (or the user tapped
    ///    "Change") — every field is editable.
    /// </summary>
    private void ApplyFieldState(FieldState state)
    {
        switch (state)
        {
            case FieldState.Idle:
                Line1Entry.IsReadOnly = false;
                Line2Entry.IsReadOnly = false;
                CityEntry.IsReadOnly = false;
                StateEntry.IsReadOnly = false;
                PostalEntry.IsReadOnly = false;
                CountryEntry.IsReadOnly = false;
                SecondaryFieldsLayout.IsEnabled = false;
                SecondaryFieldsLayout.Opacity = 0.55;
                break;

            case FieldState.Selected:
                Line1Entry.IsReadOnly = true;
                Line2Entry.IsReadOnly = false;
                CityEntry.IsReadOnly = true;
                StateEntry.IsReadOnly = true;
                PostalEntry.IsReadOnly = true;
                CountryEntry.IsReadOnly = true;
                SecondaryFieldsLayout.IsEnabled = true;
                SecondaryFieldsLayout.Opacity = 1.0;
                break;

            case FieldState.ManualEntry:
                Line1Entry.IsReadOnly = false;
                Line2Entry.IsReadOnly = false;
                CityEntry.IsReadOnly = false;
                StateEntry.IsReadOnly = false;
                PostalEntry.IsReadOnly = false;
                CountryEntry.IsReadOnly = false;
                SecondaryFieldsLayout.IsEnabled = true;
                SecondaryFieldsLayout.Opacity = 1.0;
                break;
        }
    }

    private void SyncFieldsToUi()
    {
        if (_controller == null) return;
        _suppressTextEvents = true;
        try
        {
            Line1Entry.Text = _controller.Fields.Line1;
            Line2Entry.Text = _controller.Fields.Line2;
            CityEntry.Text = _controller.Fields.City;
            StateEntry.Text = _controller.Fields.StateProvince;
            PostalEntry.Text = _controller.Fields.PostalCode;
            CountryEntry.Text = _controller.Fields.Country;
        }
        finally
        {
            _suppressTextEvents = false;
        }
    }

    /// <summary>
    /// Call from the hosting page when the parent form is saved. Resolves the
    /// manual-entry path (if needed) and returns the final persisted address,
    /// or null if nothing usable was entered.
    /// </summary>
    public async Task<AddressDto?> CommitAsync(CancellationToken ct = default)
    {
        if (_controller == null) return null;
        var address = await _controller.CommitAsync(ct);
        if (address != null)
            SelectedAddressId = address.Id;
        return address;
    }

    public void Reset()
    {
        _controller?.Reset();
        SelectedAddressId = null;
        SyncFieldsToUi();
        SuggestionsList.IsVisible = false;
        NoMatchPrompt.IsVisible = false;
        SelectedIndicator.IsVisible = false;
        ApplyFieldState(FieldState.Idle);
    }

    private enum FieldState
    {
        Idle,
        Selected,
        ManualEntry
    }
}
