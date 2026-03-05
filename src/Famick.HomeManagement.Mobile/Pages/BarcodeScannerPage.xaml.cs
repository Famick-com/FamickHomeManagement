using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;
using ZXing.Net.Maui;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class BarcodeScannerPage : ContentPage
{
    private const int RequiredConsecutiveReads = 3;

    private readonly TaskCompletionSource<string?> _scanCompletionSource = new();
    private bool _isProcessing;
    private bool _isTorchOn;
    private string? _lastDetectedValue;
    private int _consecutiveReadCount;

    public bool IsTorchOn
    {
        get => _isTorchOn;
        set
        {
            if (_isTorchOn == value) return;
            _isTorchOn = value;
            OnPropertyChanged();
        }
    }

    public BarcodeReaderOptions BarcodeOptions { get; } = new()
    {
        Formats = BarcodeFormats.OneDimensional,
        AutoRotate = true,
        Multiple = false,
        TryHarder = true
    };

    public BarcodeScannerPage()
    {
        try
        {
            InitializeComponent();
            BindingContext = this;

            // BLE scanner dual-mode: if a BLE barcode arrives while camera scanner is open,
            // treat it the same as a camera detection
            WeakReferenceMessenger.Default.Register<BleScannerBarcodeMessage>(this, (recipient, message) =>
            {
                if (_isProcessing) return;
                _isProcessing = true;

                BarcodeReader.IsDetecting = false;

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    _scanCompletionSource.TrySetResult(message.Value);
                    await Navigation.PopAsync();
                });
            });
        }
        catch (Exception ex)
        {
            // Log the error and show a fallback UI
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
                        Command = new Command(async () =>
                        {
                            _scanCompletionSource.TrySetResult(null);
                            await Navigation.PopAsync();
                        })
                    }
                }
            };
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Start detecting only after the page is fully visible and the TCS is ready.
        // This prevents the race condition where the camera detects a barcode during
        // the push animation before the caller has awaited ScanAsync().
        BarcodeReader.IsDetecting = true;
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

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing) return;

        var barcode = e.Results?.FirstOrDefault();
        if (barcode == null || string.IsNullOrEmpty(barcode.Value))
            return;

        // Require multiple consecutive identical reads to guard against partial
        // barcodes that ZXing can produce while the camera is still focusing.
        var value = barcode.Value;
        if (value == _lastDetectedValue)
        {
            _consecutiveReadCount++;
        }
        else
        {
            _lastDetectedValue = value;
            _consecutiveReadCount = 1;
        }

        if (_consecutiveReadCount < RequiredConsecutiveReads)
            return;

        _isProcessing = true;
        BarcodeReader.IsDetecting = false;

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
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _scanCompletionSource.TrySetResult(value);
            await Navigation.PopAsync();
        });
    }

    private void OnTorchClicked(object? sender, EventArgs e)
    {
        IsTorchOn = !IsTorchOn;
        TorchButton.BackgroundColor = IsTorchOn
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
            BarcodeReader.IsDetecting = false;
            _scanCompletionSource.TrySetResult(result.Trim());
            await Navigation.PopAsync();
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        BarcodeReader.IsDetecting = false;
        _scanCompletionSource.TrySetResult(null);
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        BarcodeReader.IsDetecting = false;
        // Complete with null so the caller's await unblocks if the page is
        // popped externally (e.g. back button). TrySetResult is a no-op if
        // the TCS was already completed by a barcode detection or cancel.
        _scanCompletionSource.TrySetResult(null);
    }
}
