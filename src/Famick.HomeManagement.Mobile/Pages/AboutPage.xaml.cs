using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Version info
        var version = typeof(AboutPage).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? AppInfo.VersionString;
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0) version = version[..plusIndex];
        VersionLabel.Text = $"Version {version} (Build {AppInfo.BuildString})";

        // Household name
        try
        {
            var tenantStorage = Application.Current?.Handler?.MauiContext?.Services.GetService<TenantStorage>();
            if (tenantStorage != null)
            {
                var tenantName = await tenantStorage.GetTenantNameAsync();
                if (!string.IsNullOrEmpty(tenantName))
                {
                    HouseholdLabel.Text = $"Household: {tenantName}";
                    HouseholdLabel.IsVisible = true;
                }
            }
        }
        catch { }

        PopulateLicenses();
    }

    private void PopulateLicenses()
    {
        var openSource = new[]
        {
            ("AutoMapper", "16.1", "Lucky Penny Software", "RPL-1.5", "https://github.com/AutoMapper/AutoMapper"),
            ("BCrypt.Net-Next", "4.1", "Chris McKee", "MIT", "https://github.com/BcryptNet/bcrypt.net"),
            (".NET MAUI Community Toolkit", "14.0", "Microsoft", "MIT", "https://github.com/CommunityToolkit/Maui"),
            ("MVVM Toolkit", "8.4", "Microsoft", "MIT", "https://github.com/CommunityToolkit/dotnet"),
            ("FluentValidation", "12.1", "Jeremy Skinner", "Apache-2.0", "https://github.com/FluentValidation/FluentValidation"),
            ("Plugin.BLE", "3.2", "Adrian Seceleanu", "Apache-2.0", "https://github.com/dotnet-bluetooth-le/dotnet-bluetooth-le"),
            ("sqlite-net-pcl", "1.9", "Frank A. Krueger", "MIT", "https://github.com/praeclarum/sqlite-net"),
            ("SQLitePCLRaw", "2.1", "Eric Sink", "Apache-2.0", "https://github.com/ericsink/SQLitePCL.raw"),
            ("ZXing.Net.MAUI", "0.7", "Jonathan Dick", "MIT", "https://github.com/Redth/ZXing.Net.Maui"),
        };

        foreach (var (name, ver, author, license, url) in openSource)
        {
            LicensesList.Children.Add(CreateLicenseCard(name, ver, author, license, url));
        }

        var commercial = new[]
        {
            ("Syncfusion Image Editor", "33.x", "Syncfusion Inc.", "Community License", "https://www.syncfusion.com/maui-controls/maui-image-editor"),
        };

        foreach (var (name, ver, author, license, url) in commercial)
        {
            CommercialList.Children.Add(CreateLicenseCard(name, ver, author, license, url));
        }
    }

    private static View CreateLicenseCard(string name, string version, string author, string license, string url)
    {
        var frame = new Border
        {
            Padding = new Thickness(14, 10),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = Colors.Transparent,
            StrokeThickness = 0
        };
        frame.SetAppThemeColor(Border.BackgroundColorProperty,
            Color.FromArgb("#F5F5F5"), Color.FromArgb("#1E1E1E"));

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }
        };

        var nameLabel = new Label
        {
            Text = name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        };
        Grid.SetColumn(nameLabel, 0);
        Grid.SetRow(nameLabel, 0);

        var licenseLabel = new Label
        {
            Text = license,
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        };
        licenseLabel.SetAppThemeColor(Label.TextColorProperty,
            Color.FromArgb("#1976D2"), Color.FromArgb("#90CAF9"));
        Grid.SetColumn(licenseLabel, 1);
        Grid.SetRow(licenseLabel, 0);

        var detailLabel = new Label
        {
            Text = $"{author} - v{version}",
            FontSize = 12
        };
        detailLabel.SetAppThemeColor(Label.TextColorProperty,
            Color.FromArgb("#9E9E9E"), Color.FromArgb("#757575"));
        Grid.SetColumn(detailLabel, 0);
        Grid.SetColumnSpan(detailLabel, 2);
        Grid.SetRow(detailLabel, 1);

        grid.Children.Add(nameLabel);
        grid.Children.Add(licenseLabel);
        grid.Children.Add(detailLabel);

        frame.Content = grid;

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (_, _) =>
        {
            try { await Launcher.OpenAsync(new Uri(url)); } catch { }
        };
        frame.GestureRecognizers.Add(tapGesture);

        return frame;
    }
}
