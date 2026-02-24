using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;
using ZXing.Net.Maui;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class BarcodeScannerPage : ContentPage
{
    private TaskCompletionSource<string?>? _scanCompletionSource;
    private bool _isProcessing;
    private bool _isTorchOn;

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
                    _scanCompletionSource?.TrySetResult(message.Value);
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
                            _scanCompletionSource?.TrySetResult(null);
                            await Navigation.PopAsync();
                        })
                    }
                }
            };
        }
    }

    /// <summary>
    /// Start scanning and return the result when a barcode is detected or cancelled.
    /// </summary>
    public Task<string?> ScanAsync(CancellationToken ct = default)
    {
        _scanCompletionSource = new TaskCompletionSource<string?>();

        // Register cancellation
        ct.Register(() =>
        {
            _scanCompletionSource?.TrySetResult(null);
        });

        return _scanCompletionSource.Task;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        // Prevent multiple detections
        if (_isProcessing) return;
        _isProcessing = true;

        var barcode = e.Results?.FirstOrDefault();
        if (barcode != null && !string.IsNullOrEmpty(barcode.Value))
        {
            // Stop detecting
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
                _scanCompletionSource?.TrySetResult(barcode.Value);
                await Navigation.PopAsync();
            });
        }
        else
        {
            _isProcessing = false;
        }
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
            _scanCompletionSource?.TrySetResult(result.Trim());
            await Navigation.PopAsync();
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        BarcodeReader.IsDetecting = false;
        _scanCompletionSource?.TrySetResult(null);
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        BarcodeReader.IsDetecting = false;
        _scanCompletionSource?.TrySetResult(null);
    }
}
