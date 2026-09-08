using BarcodeScanning;
using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Mobile.Messages;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class BarcodeScannerPage : ContentPage
{
    /// <summary>
    /// Symbologies that appear on retail packaging. Narrowing the set is the single
    /// biggest scan-latency lever on both platforms, and it stops a promo QR code on a
    /// nearby shelf tag from winning the frame ahead of the product's own barcode.
    /// Type 2 (embedded weight/price) barcodes are ordinary UPC-A/EAN-13 symbols, so
    /// they are covered here and parsed downstream by WeightBarcodeParser.
    /// </summary>
    public const BarcodeFormats ProductFormats =
        BarcodeFormats.Ean13 | BarcodeFormats.Ean8 |
        BarcodeFormats.Upca | BarcodeFormats.Upce |
        BarcodeFormats.Code128 | BarcodeFormats.Code39 |
        BarcodeFormats.Itf | BarcodeFormats.I2OF5 |
        BarcodeFormats.GS1DataBar;

    /// <summary>
    /// Symbologies used by our own printed labels (storage bins).
    /// </summary>
    public const BarcodeFormats LabelFormats = BarcodeFormats.QRCode | BarcodeFormats.DataMatrix;

    // RunContinuationsAsynchronously: without it the caller's continuation runs inline
    // on whichever thread completes the TCS, so it would resume mid-pop and navigate
    // while this page is still on the stack.
    private readonly TaskCompletionSource<string?> _scanCompletionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly BarcodeFormats _symbologies;
    private string? _result;
    private bool _popping;
    private bool _isProcessing;
    private bool _cameraFailed;

    public BarcodeScannerPage(BarcodeFormats symbologies = ProductFormats)
    {
        _symbologies = symbologies;

        try
        {
            InitializeComponent();
            Scanner.BarcodeSymbologies = _symbologies;

            // BLE scanner dual-mode: if a BLE barcode arrives while camera scanner is open,
            // treat it the same as a camera detection
            WeakReferenceMessenger.Default.Register<BleScannerBarcodeMessage>(this, (recipient, message) =>
            {
                if (_isProcessing) return;
                _isProcessing = true;

                Scanner.PauseScanning = true;

                MainThread.BeginInvokeOnMainThread(async () => await CompleteAndPopAsync(message.Value));
            });
        }
        catch (Exception ex)
        {
            ShowCameraError(ex);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_cameraFailed) return;

        // Unlike the ZXing reader this replaced, the native scanner does not request
        // camera permission itself — only ShoppingSessionPage pre-checks, so without
        // this the other call sites would show a black preview on a fresh install.
        try
        {
            if (!await Methods.AskForRequiredPermissionAsync())
            {
                InstructionLabel.Text = "Camera permission is required. Use \"Enter Manually\", or enable camera access in Settings.";
                return;
            }

            // Start detecting only after the page is fully visible and the TCS is ready.
            // This prevents the race condition where the camera detects a barcode during
            // the push animation before the caller has awaited ScanAsync().
            Scanner.CameraEnabled = true;
        }
        catch (Exception ex)
        {
            ShowCameraError(ex);
        }
    }

    /// <summary>
    /// Start scanning and return the result when a barcode is detected or cancelled.
    /// </summary>
    public Task<string?> ScanAsync(CancellationToken ct = default)
    {
        ct.Register(() =>
        {
            _scanCompletionSource.TrySetResult(null);
        });

        return _scanCompletionSource.Task;
    }

    private void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        // The analyzers raise this for every frame, including empty ones. The library
        // already marshals it to the main thread.
        if (_isProcessing || e.BarcodeResults.Count == 0) return;

        var barcode = SelectBest(e.BarcodeResults);
        if (barcode is null) return;

        // RawValue is the payload as encoded. DisplayValue is a human-readable
        // rendering, which for structured QR payloads is reformatted — wrong for the
        // storage-bin URL path — so only fall back to it when RawValue is empty.
        var value = !string.IsNullOrEmpty(barcode.RawValue)
            ? barcode.RawValue
            : barcode.DisplayValue;

        if (string.IsNullOrEmpty(value)) return;

        value = ScannedBarcodeNormalizer.Normalize(value);
        if (string.IsNullOrEmpty(value)) return;

        _isProcessing = true;
        Scanner.PauseScanning = true;

        // Vibrate for feedback
        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            // Vibration may not be available
        }

        // Return result on main thread
        MainThread.BeginInvokeOnMainThread(async () => await CompleteAndPopAsync(value));
    }

    /// <summary>
    /// Pops this page and only then hands the result to the awaiting caller, so the
    /// caller navigates against a settled stack.
    /// </summary>
    /// <remarks>
    /// Completing the TCS before the pop lets the caller push its next page while this
    /// one is still being removed — the in-flight pop then takes the caller's new page
    /// off the stack instead. That is what made a scanned storage bin flash up and
    /// bounce straight back to the list.
    /// </remarks>
    private async Task CompleteAndPopAsync(string? value)
    {
        _result = value;
        _popping = true;

        try
        {
            await Navigation.PopAsync();
        }
        finally
        {
            _scanCompletionSource.TrySetResult(_result);
        }
    }

    /// <summary>
    /// Picks the largest barcode in the frame — the one the user is aiming at, since a
    /// barcode fills more of the frame the closer the camera is to it. BarcodeResults is
    /// an unordered set, so taking the first entry would be non-deterministic whenever
    /// more than one barcode is in view (common on a crowded shelf).
    /// </summary>
    /// <remarks>
    /// Area is compared rather than distance from the preview centre because the bounding
    /// boxes are not in a common coordinate space across platforms: iOS reports normalized
    /// (0-1) image coordinates while Android reports image pixels. Every result within a
    /// single detection event does share one space, so relative areas are comparable even
    /// though absolute values are not.
    /// </remarks>
    private static BarcodeResult? SelectBest(IEnumerable<BarcodeResult> results)
    {
        BarcodeResult? best = null;
        var bestArea = float.MinValue;

        foreach (var result in results)
        {
            var box = result.ImageBoundingBox;
            var area = Math.Abs(box.Width * box.Height);

            if (area > bestArea)
            {
                bestArea = area;
                best = result;
            }
        }

        return best;
    }

    private void OnTorchClicked(object? sender, EventArgs e)
    {
        Scanner.TorchOn = !Scanner.TorchOn;
        TorchButton.BackgroundColor = Scanner.TorchOn
            ? Color.FromArgb("#FFC107")
            : Color.FromArgb("#555555");
    }

    private async void OnManualEntryClicked(object? sender, EventArgs e)
    {
        var result = await DisplayPromptAsync(
            "Enter Barcode",
            "Type the barcode number:",
            "OK",
            "Cancel",
            keyboard: Keyboard.Numeric);

        if (!string.IsNullOrWhiteSpace(result))
        {
            if (_isProcessing) return;
            _isProcessing = true;
            Scanner.PauseScanning = true;
            await CompleteAndPopAsync(result.Trim());
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        Scanner.PauseScanning = true;
        await CompleteAndPopAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(this);

        if (!_cameraFailed)
            Scanner.CameraEnabled = false;

        // When we are popping ourselves, CompleteAndPopAsync completes the TCS once the
        // pop has finished — releasing the caller here instead would put it back in the
        // race this is meant to avoid. This path is the safety net for an external pop
        // (e.g. hardware back), where _result is still null.
        if (!_popping)
            _scanCompletionSource.TrySetResult(_result);
    }

    /// <summary>
    /// Replaces the page content with an explanatory fallback when the camera cannot
    /// be initialized, so the caller still gets a result instead of a blank page.
    /// </summary>
    private void ShowCameraError(Exception ex)
    {
        _cameraFailed = true;
        System.Diagnostics.Debug.WriteLine($"BarcodeScannerPage initialization error: {ex}");

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
                    Command = new Command(async () => await CompleteAndPopAsync(null))
                }
            }
        };
    }
}
