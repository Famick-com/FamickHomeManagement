using MudBlazor;

namespace Famick.HomeManagement.UI.Theme;

/// <summary>
/// Centralized theme configuration for Famick Home Management.
/// Based on the Famick green/gray house-themed logo.
/// </summary>
public static class FamickTheme
{
    // Brand Colors (extracted from logo)
    public static class Colors
    {
        // Greens
        public const string SageGreen = "#7BA17C";      // Primary light variant
        public const string ForestGreen = "#518751";     // Primary color
        public const string DarkGreen = "#3D6B3D";       // Primary dark variant
        public const string DarkModeGreen = "#2D4A2D";   // App bar in dark mode

        // Grays
        public const string WarmGray = "#9E9E9E";        // Secondary color (house)
        public const string LightGray = "#BDBDBD";       // Secondary light
        public const string SmokeGray = "#A8A8A8";       // Chimney smoke
        public const string Charcoal = "#424242";        // Dark mode surfaces

        // Backgrounds
        public const string DarkSurface = "#1E1E1E";
        public const string DarkBackground = "#121212";
    }

    /// <summary>
    /// The main MudBlazor theme with Famick branding.
    /// </summary>
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = Colors.ForestGreen,
            PrimaryDarken = Colors.DarkGreen,
            PrimaryLighten = Colors.SageGreen,
            Secondary = Colors.WarmGray,
            SecondaryDarken = Colors.Charcoal,
            SecondaryLighten = Colors.LightGray,
            Tertiary = Colors.SageGreen,
            AppbarBackground = Colors.ForestGreen,
            AppbarText = MudBlazor.Colors.Shades.White,
            DrawerBackground = MudBlazor.Colors.Shades.White,
            DrawerText = MudBlazor.Colors.Gray.Darken3,
            DrawerIcon = Colors.ForestGreen,
        },
        PaletteDark = new PaletteDark()
        {
            Primary = Colors.SageGreen,
            PrimaryDarken = Colors.ForestGreen,
            PrimaryLighten = "#A5C4A5",
            Secondary = Colors.LightGray,
            SecondaryDarken = Colors.WarmGray,
            SecondaryLighten = "#E0E0E0",
            Tertiary = Colors.ForestGreen,
            AppbarBackground = Colors.DarkModeGreen,
            AppbarText = MudBlazor.Colors.Shades.White,
            DrawerBackground = Colors.DarkSurface,
            DrawerText = MudBlazor.Colors.Gray.Lighten3,
            DrawerIcon = Colors.SageGreen,
            Surface = Colors.DarkSurface,
            Background = Colors.DarkBackground,
        },
        Typography = new Typography()
        {
            Default = new Default()
            {
                FontFamily = new[] { "Roboto", "Helvetica", "Arial", "sans-serif" }
            }
        }
    };
}
