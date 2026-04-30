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
        Line2Combo.PropertyChanged += OnLine2ComboPropertyChanged;
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
        _controller.SecondaryOptionsChanged += OnSecondaryOptionsChanged;
        _controller.ExpansionStateChanged += OnExpansionStateChanged;

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

    private void OnLine2ComboPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // SfComboBox doesn't expose a TextChanged event in the way Entry does;
        // listen on the bindable Text property instead so the controller stays
        // in sync as the user types a free-form apt/suite value.
        if (_suppressTextEvents || _controller == null) return;
        if (e.PropertyName == nameof(Syncfusion.Maui.Inputs.SfComboBox.Text))
            _controller.Fields.Line2 = Line2Combo.Text;
    }

    private async void OnLine2ComboSelectionChanged(object? sender, Syncfusion.Maui.Inputs.SelectionChangedEventArgs e)
    {
        if (_suppressTextEvents || _controller == null) return;
        if (e.AddedItems is null || e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not AddressSuggestionDto picked) return;

        await _controller.OnSecondaryOptionSelected(picked.SuggestionId);
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

    private void OnSecondaryOptionsChanged()
    {
        if (_controller == null) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Line2Combo.ItemsSource = _controller.SecondaryOptions;
            if (_controller.IsAwaitingSecondary)
            {
                SyncFieldsToUi();
                ApplyFieldState(FieldState.AwaitingSecondary);
                UpdateSelectedIndicator();
            }
        });
    }

    private void OnExpansionStateChanged()
    {
        if (_controller == null) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var expanding = _controller.IsExpandingSecondaries;
            ExpandSpinner.IsVisible = expanding;
            ExpandSpinner.IsRunning = expanding;
        });
    }

    private void UpdateSelectedIndicator()
    {
        if (_controller is null)
        {
            SelectedIndicator.IsVisible = false;
            return;
        }

        if (_controller.IsAwaitingSecondary)
        {
            SelectedIndicator.Text = "Pick an apartment";
            SelectedIndicator.IsVisible = true;
            return;
        }

        if (_controller.SelectedAddressId is null)
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
    /// Four discrete UI states for the field:
    ///  - <c>Idle</c>: nothing selected yet, secondary fields dimmed.
    ///  - <c>Selected</c>: a suggestion has been resolved — Line 1 and the
    ///    standardized fields are read-only; only Apt/Suite stays editable.
    ///  - <c>AwaitingSecondary</c>: the user picked a multi-unit parent
    ///    suggestion. Line 1 / standardized fields are populated and
    ///    read-only; the Apt/Suite combo is enabled with the canonical
    ///    unit list. No Address row has been resolved yet.
    ///  - <c>ManualEntry</c>: no autocomplete match (or the user tapped
    ///    "Change") — every field is editable.
    /// </summary>
    private void ApplyFieldState(FieldState state)
    {
        switch (state)
        {
            case FieldState.Idle:
                Line1Entry.IsReadOnly = false;
                Line2Combo.IsEnabled = true;
                CityEntry.IsReadOnly = false;
                StateEntry.IsReadOnly = false;
                PostalEntry.IsReadOnly = false;
                CountryEntry.IsReadOnly = false;
                SecondaryFieldsLayout.IsEnabled = false;
                SecondaryFieldsLayout.Opacity = 0.55;
                break;

            case FieldState.Selected:
                Line1Entry.IsReadOnly = true;
                Line2Combo.IsEnabled = true;
                CityEntry.IsReadOnly = true;
                StateEntry.IsReadOnly = true;
                PostalEntry.IsReadOnly = true;
                CountryEntry.IsReadOnly = true;
                SecondaryFieldsLayout.IsEnabled = true;
                SecondaryFieldsLayout.Opacity = 1.0;
                break;

            case FieldState.AwaitingSecondary:
                Line1Entry.IsReadOnly = true;
                Line2Combo.IsEnabled = true;
                CityEntry.IsReadOnly = true;
                StateEntry.IsReadOnly = true;
                PostalEntry.IsReadOnly = true;
                CountryEntry.IsReadOnly = true;
                SecondaryFieldsLayout.IsEnabled = true;
                SecondaryFieldsLayout.Opacity = 1.0;
                break;

            case FieldState.ManualEntry:
                Line1Entry.IsReadOnly = false;
                Line2Combo.IsEnabled = true;
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
            Line2Combo.Text = _controller.Fields.Line2;
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
        Line2Combo.ItemsSource = null;
        ExpandSpinner.IsVisible = false;
        ExpandSpinner.IsRunning = false;
        ApplyFieldState(FieldState.Idle);
    }

    private enum FieldState
    {
        Idle,
        Selected,
        AwaitingSecondary,
        ManualEntry
    }
}
