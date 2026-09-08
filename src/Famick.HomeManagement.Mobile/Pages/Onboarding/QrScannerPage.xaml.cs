using Famick.HomeManagement.Mobile.Services;
using BarcodeScanning;

namespace Famick.HomeManagement.Mobile.Pages.Onboarding;

public partial class QrScannerPage : ContentPage
{
    private readonly ApiSettings _apiSettings;
    private readonly ShoppingApiClient _apiClient;
    private bool _isProcessing;
    private bool _cameraFailed;

    public bool ShowOverlay => true;

    public QrScannerPage(ApiSettings apiSettings, ShoppingApiClient apiClient)
    {
        _apiSettings = apiSettings;
        _apiClient = apiClient;

        try
        {
            InitializeComponent();
            BindingContext = this;
            Scanner.BarcodeSymbologies = BarcodeFormats.QRCode;
        }
        catch (Exception ex)
        {
            // Log the error and show a fallback UI
            _cameraFailed = true;
            System.Diagnostics.Debug.WriteLine($"QrScannerPage initialization error: {ex}");
            Content = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                Padding = 20,
                Children =
                {
                    new Label
                    {
                        Text = "Camera Error",
                        FontSize = 24,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = $"Unable to initialize camera scanner:\n{ex.Message}",
                        HorizontalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Button
                    {
                        Text = "Go Back",
                        Command = new Command(async () => await Navigation.PopAsync())
                    }
                }
            };
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_cameraFailed) return;

        // The native scanner does not request camera permission itself, and
        // WelcomePage pushes this page without pre-checking.
        try
        {
            if (!await Methods.AskForRequiredPermissionAsync())
            {
                ShowStatus("Camera permission is required to scan the setup QR code. Enable camera access in Settings.", false);
                return;
            }

            Scanner.CameraEnabled = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"QrScannerPage camera start error: {ex}");
            ShowStatus($"Unable to start the camera: {ex.Message}", false);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (!_cameraFailed)
            Scanner.CameraEnabled = false;
    }

    private void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        // Raised for every analyzed frame, including empty ones. Already on the main thread.
        if (_isProcessing || e.BarcodeResults.Count == 0) return;

        var barcode = e.BarcodeResults.FirstOrDefault();

        // RawValue is the payload as encoded; DisplayValue reformats structured QR
        // payloads, which would corrupt the famick:// setup URL.
        var value = barcode is null
            ? null
            : !string.IsNullOrEmpty(barcode.RawValue) ? barcode.RawValue : barcode.DisplayValue;

        if (string.IsNullOrEmpty(value)) return;

        _isProcessing = true;
        Scanner.PauseScanning = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ProcessQrCodeAsync(value);
        });
    }

    private async Task ProcessQrCodeAsync(string qrValue)
    {
        ShowStatus("Processing QR code...", true);

        try
        {
            // Expected format: famick://setup?url=https://server.com&name=HouseholdName
            // Or: famick://setup?url=https://server.com
            var parsed = ParseQrCode(qrValue);

            if (parsed == null)
            {
                ShowStatus("Invalid QR code format. Please scan a Famick setup QR code.", false);
                await Task.Delay(2000);
                _isProcessing = false;
                Scanner.PauseScanning = false;
                HideStatus();
                return;
            }

            ShowStatus($"Connecting to {parsed.Value.tenantName ?? "server"}...", true);

            // Test connection to the server
            _apiSettings.Mode = ServerMode.SelfHosted;
            _apiSettings.SelfHostedUrl = parsed.Value.url;

            var isHealthy = await _apiClient.CheckHealthAsync();

            if (isHealthy)
            {
                // Configure server from QR code
                _apiSettings.ConfigureFromQrCode(parsed.Value.url, parsed.Value.tenantName);

                ShowStatus("Server configured! Redirecting to login...", false);
                await Task.Delay(1000);

                // Navigate to login page
                await Navigation.PushAsync(
                    Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<LoginPage>());
            }
            else
            {
                ShowStatus("Could not connect to the server. Please try again.", false);
                await Task.Delay(2000);
                _isProcessing = false;
                Scanner.PauseScanning = false;
                HideStatus();
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", false);
            await Task.Delay(2000);
            _isProcessing = false;
            Scanner.PauseScanning = false;
            HideStatus();
        }
    }

    private static (string url, string? tenantName)? ParseQrCode(string qrValue)
    {
        try
        {
            // Handle famick:// scheme
            if (qrValue.StartsWith("famick://setup", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(qrValue);
                var queryParams = ParseQueryString(uri.Query);

                var url = queryParams.GetValueOrDefault("url");
                var name = queryParams.GetValueOrDefault("name");

                if (!string.IsNullOrEmpty(url))
                {
                    return (url, name);
                }
            }

            // Handle HTTP/HTTPS URLs (could be app-setup page with query params or direct server URL)
            if (qrValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                qrValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(qrValue);

                // Check if it's an app-setup URL with query parameters
                if (!string.IsNullOrEmpty(uri.Query))
                {
                    var queryParams = ParseQueryString(uri.Query);
                    var url = queryParams.GetValueOrDefault("url");
                    var name = queryParams.GetValueOrDefault("name");

                    if (!string.IsNullOrEmpty(url))
                    {
                        return (url, name);
                    }
                }

                // Otherwise treat as direct server URL (strip any path/query)
                var baseUrl = $"{uri.Scheme}://{uri.Host}";
                if (!uri.IsDefaultPort)
                    baseUrl += $":{uri.Port}";
                return (baseUrl, null);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(query))
            return result;

        // Remove leading '?' if present
        if (query.StartsWith("?"))
            query = query[1..];

        foreach (var pair in query.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                // Replace + with space before URL decoding (standard form encoding)
                var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
                var value = Uri.UnescapeDataString(parts[1].Replace('+', ' '));
                result[key] = value;
            }
        }

        return result;
    }

    private void ShowStatus(string message, bool showLoading)
    {
        StatusFrame.IsVisible = true;
        StatusLabel.Text = message;
        LoadingIndicator.IsRunning = showLoading;
        LoadingIndicator.IsVisible = showLoading;
    }

    private void HideStatus()
    {
        StatusFrame.IsVisible = false;
    }
}
