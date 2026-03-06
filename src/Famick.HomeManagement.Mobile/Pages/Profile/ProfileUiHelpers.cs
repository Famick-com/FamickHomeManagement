namespace Famick.HomeManagement.Mobile.Pages.Profile;

internal static class ProfileUiHelpers
{
    public static Border CreateCard(View content)
    {
        return new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Stroke = Colors.Transparent,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#2A2A2A")
                : Colors.White,
            Padding = new Thickness(16),
            Content = content
        };
    }

    public static Label CreateLabel(string text, bool isBold = false, double fontSize = 14)
    {
        return new Label
        {
            Text = text,
            FontSize = fontSize,
            FontAttributes = isBold ? FontAttributes.Bold : FontAttributes.None,
            TextColor = isBold ? GetTextColor() : GetSecondaryTextColor()
        };
    }

    public static Color GetTextColor()
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? Colors.White
            : Colors.Black;
    }

    public static Color GetSecondaryTextColor()
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#B0B0B0")
            : Color.FromArgb("#666666");
    }
}
