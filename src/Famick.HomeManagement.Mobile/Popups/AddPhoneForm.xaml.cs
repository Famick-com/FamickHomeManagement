using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class AddPhoneForm : ContentView
{
    private static readonly PhoneTagOption[] Tags =
    {
        new("Mobile", 0),
        new("Home", 1),
        new("Work", 2),
        new("Fax", 3),
        new("Other", 99)
    };

    public event EventHandler<AddPhoneResult>? Submitted;
    public event EventHandler? Cancelled;

    public AddPhoneForm()
    {
        InitializeComponent();
        TypeCombo.ItemsSource = Tags;
        TypeCombo.SelectedIndex = 0;
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        var phone = PhoneEditor.Value?.Trim();
        if (string.IsNullOrEmpty(phone)) return;

        var tagIndex = TypeCombo.SelectedIndex;
        var tag = tagIndex >= 0 && tagIndex < Tags.Length ? Tags[tagIndex].Value : 0;

        Submitted?.Invoke(this, new AddPhoneResult(phone, tag, PrimarySwitch.IsToggled));
    }

    private sealed record PhoneTagOption(string Label, int Value);
}
