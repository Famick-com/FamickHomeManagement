using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Shared.PhoneFormatting;
using Syncfusion.Maui.Inputs;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class PhoneNumberEditor : ContentView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(string),
        typeof(PhoneNumberEditor),
        default(string),
        BindingMode.TwoWay,
        propertyChanged: OnValueChanged);

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private CountryPhoneFormat _country = CountryPhoneFormats.Default;
    private bool _syncing;
    private string? _lastEmitted;

    public PhoneNumberEditor()
    {
        InitializeComponent();
        UpdateCountryLabel(CountryPhoneFormats.Default);
        SyncFromValue(Value);
    }

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PhoneNumberEditor editor)
        {
            editor.SyncFromValue(newValue as string);
        }
    }

    private void SyncFromValue(string? value)
    {
        if (string.Equals(value, _lastEmitted, StringComparison.Ordinal)) return;

        _syncing = true;
        try
        {
            var parsed = PhoneNumberFormatter.Parse(value);
            _country = parsed.Country;
            UpdateCountryLabel(parsed.Country);
            ApplyMask(parsed.Country);
            if (!string.Equals(NumberEntry.Value?.ToString(), parsed.LocalNumber, StringComparison.Ordinal))
            {
                NumberEntry.Value = parsed.LocalNumber;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateCountryLabel(CountryPhoneFormat country)
    {
        CountryLabel.Text = $"{country.Flag} {country.DialingCode}";
    }

    private void ApplyMask(CountryPhoneFormat country)
    {
        var target = country.HasFixedMask ? country.Mask! : string.Empty;
        if (!string.Equals(NumberEntry.Mask, target, StringComparison.Ordinal))
        {
            NumberEntry.Mask = target;
        }
    }

    private async void OnCountryTapped(object? sender, TappedEventArgs e)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null) return;

        var chosen = await CountryPickerPopup.ShowAsync(page);
        if (chosen is null || ReferenceEquals(chosen, _country)) return;

        _country = chosen;
        UpdateCountryLabel(chosen);
        ApplyMask(chosen);
        Emit();
    }

    private void OnNumberChanged(object? sender, MaskedEntryValueChangedEventArgs e)
    {
        if (_syncing) return;
        Emit();
    }

    private void Emit()
    {
        var local = NumberEntry.Value?.ToString() ?? string.Empty;
        var stored = PhoneNumberFormatter.FormatForStorage(_country, local);
        _lastEmitted = stored;
        Value = stored;
    }
}
