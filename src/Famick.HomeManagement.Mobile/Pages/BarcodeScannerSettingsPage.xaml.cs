using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class BarcodeScannerSettingsPage : ContentPage
{
    private CancellationTokenSource? _testCts;

    public BarcodeScannerSettingsPage()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<BleScannerStateMessage>(this, (recipient, message) =>
        {
            MainThread.BeginInvokeOnMainThread(() => UpdateScannerStatusUI());
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateScannerUI();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(this);

        var bleService = GetBleService();
        bleService?.CancelDiscovery();
    }

    private BleScannerService? GetBleService()
        => Application.Current?.Handler?.MauiContext?.Services.GetService<BleScannerService>();

    private void UpdateScannerUI()
    {
        var bleService = GetBleService();
        if (bleService == null) return;

        if (bleService.HasSavedScanner)
        {
            NoScannerSection.IsVisible = false;
            DiscoverySection.IsVisible = false;
            ConnectedScannerSection.IsVisible = true;
            ScannerNameLabel.Text = bleService.ConnectedDeviceName ?? "Unknown Scanner";
            UpdateScannerStatusUI();
        }
        else
        {
            NoScannerSection.IsVisible = true;
            ConnectedScannerSection.IsVisible = false;
            DiscoverySection.IsVisible = false;
        }
    }

    private void UpdateScannerStatusUI()
    {
        var bleService = GetBleService();
        if (bleService == null) return;

        switch (bleService.ConnectionState)
        {
            case BleScannerConnectionState.Connected:
                ScannerStatusIndicator.TextColor = Colors.Green;
                ScannerStatusLabel.Text = "Connected";
                break;
            case BleScannerConnectionState.Connecting:
                ScannerStatusIndicator.TextColor = Colors.Orange;
                ScannerStatusLabel.Text = "Connecting...";
                break;
            case BleScannerConnectionState.Reconnecting:
                ScannerStatusIndicator.TextColor = Colors.Orange;
                ScannerStatusLabel.Text = "Reconnecting...";
                break;
            default:
                ScannerStatusIndicator.TextColor = Colors.Gray;
                ScannerStatusLabel.Text = "Disconnected";
                break;
        }
    }

    private async void OnFindScannerClicked(object? sender, EventArgs e)
    {
        var bleService = GetBleService();
        if (bleService == null) return;

        NoScannerSection.IsVisible = false;
        ConnectedScannerSection.IsVisible = false;
        DiscoverySection.IsVisible = true;
        DiscoverySpinner.IsRunning = true;
        DiscoveryStatusLabel.Text = "Searching for scanners...";
        DiscoveredScannersCollection.ItemsSource = null;
        AdvancedModeCheckbox.IsChecked = false;

        await RunDiscoveryAsync(advancedMode: false);
    }

    private async Task RunDiscoveryAsync(bool advancedMode)
    {
        var bleService = GetBleService();
        if (bleService == null) return;

        DiscoverySpinner.IsRunning = true;
        DiscoveryStatusLabel.Text = advancedMode
            ? "Searching all BLE devices..."
            : "Searching for scanners...";

        try
        {
            var scanners = await bleService.DiscoverScannersAsync(advancedMode);

            var displayItems = scanners.Select(s => new ScannerDisplayItem
            {
                DeviceId = s.DeviceId,
                Name = s.Name,
                Rssi = s.Rssi,
                IsKnownScanner = s.IsKnownScanner,
                Manufacturer = s.Manufacturer,
                DisplayIcon = s.IsKnownScanner ? "\u2705" : "\U0001F4F6",
                DisplayLabel = s.IsKnownScanner
                    ? $"{s.Manufacturer} - Barcode Scanner"
                    : s.HeuristicScore > 0
                        ? "Possible scanner"
                        : "BLE Device",
                Scanner = s
            }).ToList();

            DiscoveredScannersCollection.ItemsSource = displayItems;
            DiscoveryStatusLabel.Text = $"Found {displayItems.Count} device(s)";
        }
        catch (Exception ex)
        {
            DiscoveryStatusLabel.Text = $"Search failed: {ex.Message}";
        }
        finally
        {
            DiscoverySpinner.IsRunning = false;
        }
    }

    private async void OnScannerSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ScannerDisplayItem item) return;

        DiscoveredScannersCollection.SelectedItem = null;

        var confirm = await DisplayAlertAsync(
            "Connect Scanner",
            $"Connect to {item.Name}?",
            "Connect",
            "Cancel");
        if (!confirm) return;

        var bleService = GetBleService();
        if (bleService == null) return;

        DiscoverySpinner.IsRunning = true;
        DiscoveryStatusLabel.Text = $"Connecting to {item.Name}...";

        var success = await bleService.ConnectAsync(item.Scanner);

        if (success)
        {
            DiscoverySection.IsVisible = false;
            UpdateScannerUI();
            await DisplayAlertAsync("Connected", $"Successfully connected to {item.Name}", "OK");
        }
        else
        {
            DiscoverySpinner.IsRunning = false;
            DiscoveryStatusLabel.Text = "Connection failed. Select a scanner to try again.";
        }
    }

    private async void OnTestScannerClicked(object? sender, EventArgs e)
    {
        var bleService = GetBleService();
        if (bleService == null || !bleService.IsConnected)
        {
            await DisplayAlertAsync("Not Connected", "Scanner is not connected.", "OK");
            return;
        }

        TestResultBorder.IsVisible = true;
        TestResultLabel.Text = "Waiting for scan...";
        TestResultBorder.BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#1A237E") : Color.FromArgb("#E3F2FD");

        _testCts?.Cancel();
        _testCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var tcs = new TaskCompletionSource<string?>();
        _testCts.Token.Register(() => tcs.TrySetResult(null));

        void OnBarcode(object recipient, BleScannerBarcodeMessage message)
        {
            tcs.TrySetResult(message.Value);
        }

        WeakReferenceMessenger.Default.Register<BleScannerBarcodeMessage>(tcs, OnBarcode);

        var barcode = await tcs.Task;

        WeakReferenceMessenger.Default.Unregister<BleScannerBarcodeMessage>(tcs);
        _testCts = null;

        if (!string.IsNullOrEmpty(barcode))
        {
            TestResultBorder.BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1B5E20") : Color.FromArgb("#F1F8E9");
            TestResultLabel.Text = barcode;
        }
        else
        {
            TestResultBorder.IsVisible = false;
        }
    }

    private async void OnRemoveScannerClicked(object? sender, EventArgs e)
    {
        var bleService = GetBleService();
        if (bleService == null) return;

        var confirm = await DisplayAlertAsync(
            "Remove Scanner",
            $"Disconnect and remove {bleService.ConnectedDeviceName}?",
            "Remove",
            "Cancel");
        if (!confirm) return;

        await bleService.RemoveScannerAsync();
        UpdateScannerUI();
    }

    private async void OnAdvancedModeChanged(object? sender, CheckedChangedEventArgs e)
    {
        var bleService = GetBleService();
        if (bleService == null) return;

        bleService.CancelDiscovery();
        await RunDiscoveryAsync(advancedMode: e.Value);
    }

    private void OnCancelDiscoveryClicked(object? sender, EventArgs e)
    {
        var bleService = GetBleService();
        bleService?.CancelDiscovery();

        DiscoverySection.IsVisible = false;
        UpdateScannerUI();
    }
}

/// <summary>
/// Display wrapper for discovered BLE scanners in the CollectionView.
/// </summary>
public class ScannerDisplayItem
{
    public Guid DeviceId { get; init; }
    public required string Name { get; init; }
    public int Rssi { get; init; }
    public bool IsKnownScanner { get; init; }
    public string? Manufacturer { get; init; }
    public required string DisplayIcon { get; init; }
    public required string DisplayLabel { get; init; }
    public required DiscoveredBleScanner Scanner { get; init; }
}
