using Famick.HomeManagement.Shared.PhoneFormatting;

namespace Famick.HomeManagement.Mobile.Controls;

public partial class PhoneNumberDisplay : ContentView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(string),
        typeof(PhoneNumberDisplay),
        default(string),
        propertyChanged: OnValueChanged);

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(PhoneNumberDisplay),
        15.0,
        propertyChanged: OnFontSizeChanged);

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public PhoneNumberDisplay()
    {
        InitializeComponent();
        Render();
    }

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((PhoneNumberDisplay)bindable).Render();
    }

    private static void OnFontSizeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((PhoneNumberDisplay)bindable).DisplayLabel.FontSize = (double)newValue;
    }

    private void Render()
    {
        DisplayLabel.Text = PhoneNumberFormatter.FormatForDisplay(Value);
        DisplayLabel.FontSize = FontSize;
    }
}
