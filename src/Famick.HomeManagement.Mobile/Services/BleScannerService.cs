using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;
using Famick.HomeManagement.Mobile.Models;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Manages BLE barcode scanner discovery, connection, and barcode reading.
/// Registered as a Singleton in DI.
/// </summary>
public sealed class BleScannerService : IDisposable
{
    private const string PrefKeyDeviceId = "ble_scanner_device_id";
    private const string PrefKeyDeviceName = "ble_scanner_device_name";
    private const string PrefKeyManufacturer = "ble_scanner_manufacturer";
    private const string PrefKeyServiceUuid = "ble_scanner_service_uuid";
    private const string PrefKeyCharacteristicUuid = "ble_scanner_characteristic_uuid";

    private static readonly Guid HidServiceUuid = Guid.Parse("00001812-0000-1000-8000-00805f9b34fb");

    private readonly IAdapter _adapter;
    private readonly IBluetoothLE _ble;
    private KnownScannerDatabase? _knownScanners;
    private bool _initialized;

    private IDevice? _connectedDevice;
    private ICharacteristic? _barcodeCharacteristic;
    private CancellationTokenSource? _reconnectCts;
    private CancellationTokenSource? _discoveryCts;

    private string? _lastBarcode;
    private DateTime _lastBarcodeTime = DateTime.MinValue;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(1);

    private bool _disposed;

    public BleScannerConnectionState ConnectionState { get; private set; }
        = BleScannerConnectionState.Disconnected;

    public BleScannerConfig? SavedConfig { get; private set; }
    public string? ConnectedDeviceName => _connectedDevice?.Name ?? SavedConfig?.DeviceName;
    public bool HasSavedScanner => SavedConfig != null;
    public bool IsConnected => ConnectionState == BleScannerConnectionState.Connected;

    public BleScannerService()
    {
        _ble = CrossBluetoothLE.Current;
        _adapter = CrossBluetoothLE.Current.Adapter;
        _adapter.DeviceDisconnected += OnDeviceDisconnected;
        _adapter.ScanTimeout = 10000; // 10 seconds

        LoadSavedConfig();
    }

    /// <summary>
    /// Loads the known scanner database from embedded resources.
    /// Safe to call multiple times; only loads once.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("known_ble_scanners.json");
            _knownScanners = await JsonSerializer.DeserializeAsync<KnownScannerDatabase>(stream);
            _initialized = true;
            Console.WriteLine($"[BLE] Loaded known scanner database v{_knownScanners?.Version} with {_knownScanners?.Scanners.Count} entries");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BLE] Failed to load known scanner database: {ex.Message}");
            _knownScanners = new KnownScannerDatabase();
            _initialized = true;
        }
    }

    /// <summary>
    /// Discovers nearby BLE barcode scanners.
    /// In normal mode, only shows devices matching known scanner UUIDs/names.
    /// In advanced mode, shows all devices with heuristic ranking.
    /// </summary>
    public async Task<List<DiscoveredBleScanner>> DiscoverScannersAsync(
        bool advancedMode = false,
        CancellationToken ct = default)
    {
        await InitializeAsync();

        _discoveryCts?.Cancel();
        _discoveryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var localCt = _discoveryCts.Token;

        SetState(BleScannerConnectionState.Scanning);

        var discovered = new List<DiscoveredBleScanner>();

        _adapter.DeviceDiscovered += OnDeviceFound;

        try
        {
            await _adapter.StartScanningForDevicesAsync(
                cancellationToken: localCt);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancel/timeout
        }
        finally
        {
            _adapter.DeviceDiscovered -= OnDeviceFound;
            SetState(BleScannerConnectionState.Disconnected);
        }

        if (!advancedMode)
        {
            discovered = discovered.Where(d => d.IsKnownScanner).ToList();
        }

        return discovered
            .OrderByDescending(d => d.IsKnownScanner)
            .ThenByDescending(d => d.HeuristicScore)
            .ThenByDescending(d => d.Rssi)
            .ToList();

        void OnDeviceFound(object? sender, DeviceEventArgs e)
        {
            var device = e.Device;
            if (string.IsNullOrEmpty(device.Name)) return;

            // Skip duplicates
            if (discovered.Any(d => d.DeviceId == device.Id)) return;

            var serviceUuids = device.AdvertisementRecords?
                .Where(r => r.Type == AdvertisementRecordType.UuidsComplete128Bit
                         || r.Type == AdvertisementRecordType.UuidsIncomplete128Bit)
                .SelectMany(r => ParseServiceUuids(r.Data))
                .ToList() ?? [];

            var (isKnown, manufacturer, model) = MatchKnownScanner(device.Name, serviceUuids);
            var score = ComputeHeuristicScore(device.Name, serviceUuids);

            // Add RSSI bonus for strong signals
            if (device.Rssi > -60)
                score += 10;

            discovered.Add(new DiscoveredBleScanner
            {
                DeviceId = device.Id,
                Name = device.Name,
                Rssi = device.Rssi,
                IsKnownScanner = isKnown,
                Manufacturer = manufacturer,
                Model = model,
                HeuristicScore = score,
                ServiceUuids = serviceUuids
            });
        }
    }

    /// <summary>
    /// Connects to a discovered scanner and subscribes to barcode notifications.
    /// Saves the config to Preferences on success.
    /// </summary>
    public async Task<bool> ConnectAsync(DiscoveredBleScanner scanner)
    {
        await InitializeAsync();

        SetState(BleScannerConnectionState.Connecting);

        try
        {
            // Find the device from the adapter's discovered list
            var device = _adapter.DiscoveredDevices.FirstOrDefault(d => d.Id == scanner.DeviceId);
            if (device == null)
            {
                Console.WriteLine("[BLE] Device not found in discovered list");
                SetState(BleScannerConnectionState.Disconnected);
                return false;
            }

            await _adapter.ConnectToDeviceAsync(device);
            _connectedDevice = device;

            // Find the barcode notification characteristic
            var characteristic = await FindBarcodeCharacteristicAsync(device, scanner);
            if (characteristic == null)
            {
                Console.WriteLine("[BLE] No barcode characteristic found");
                await _adapter.DisconnectDeviceAsync(device);
                _connectedDevice = null;
                SetState(BleScannerConnectionState.Disconnected);
                return false;
            }

            _barcodeCharacteristic = characteristic;
            _barcodeCharacteristic.ValueUpdated += OnBarcodeCharacteristicUpdated;
            await _barcodeCharacteristic.StartUpdatesAsync();

            // Save config
            var config = new BleScannerConfig
            {
                DeviceId = scanner.DeviceId,
                DeviceName = scanner.Name,
                Manufacturer = scanner.Manufacturer,
                ServiceUuid = _barcodeCharacteristic.Service.Id,
                CharacteristicUuid = _barcodeCharacteristic.Id
            };
            SaveConfig(config);

            SetState(BleScannerConnectionState.Connected);
            Console.WriteLine($"[BLE] Connected to {scanner.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BLE] Connect failed: {ex.Message}");
            _connectedDevice = null;
            SetState(BleScannerConnectionState.Disconnected);
            return false;
        }
    }

    /// <summary>
    /// Attempts to reconnect to the saved scanner.
    /// Used for auto-connect on app startup.
    /// </summary>
    public async Task AutoConnectAsync()
    {
        if (SavedConfig == null || IsConnected) return;

        await InitializeAsync();

        SetState(BleScannerConnectionState.Connecting);

        try
        {
            var device = await _adapter.ConnectToKnownDeviceAsync(
                SavedConfig.DeviceId,
                cancellationToken: new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);

            _connectedDevice = device;

            // Try saved characteristic first, then discover
            var characteristic = await FindCharacteristicByIdAsync(device, SavedConfig.ServiceUuid, SavedConfig.CharacteristicUuid)
                ?? await FindBarcodeCharacteristicAsync(device, null);

            if (characteristic == null)
            {
                Console.WriteLine("[BLE] Auto-connect: no barcode characteristic found");
                await _adapter.DisconnectDeviceAsync(device);
                _connectedDevice = null;
                SetState(BleScannerConnectionState.Disconnected);
                return;
            }

            _barcodeCharacteristic = characteristic;
            _barcodeCharacteristic.ValueUpdated += OnBarcodeCharacteristicUpdated;
            await _barcodeCharacteristic.StartUpdatesAsync();

            SetState(BleScannerConnectionState.Connected);
            Console.WriteLine($"[BLE] Auto-connected to {SavedConfig.DeviceName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BLE] Auto-connect failed: {ex.Message}");
            _connectedDevice = null;
            SetState(BleScannerConnectionState.Disconnected);
        }
    }

    /// <summary>
    /// Disconnects and removes the saved scanner configuration.
    /// </summary>
    public async Task RemoveScannerAsync()
    {
        StopReconnecting();

        if (_barcodeCharacteristic != null)
        {
            _barcodeCharacteristic.ValueUpdated -= OnBarcodeCharacteristicUpdated;
            try { await _barcodeCharacteristic.StopUpdatesAsync(); }
            catch { /* Ignore */ }
            _barcodeCharacteristic = null;
        }

        if (_connectedDevice != null)
        {
            try { await _adapter.DisconnectDeviceAsync(_connectedDevice); }
            catch { /* Ignore */ }
            _connectedDevice = null;
        }

        ClearSavedConfig();
        SetState(BleScannerConnectionState.Disconnected);
        Console.WriteLine("[BLE] Scanner removed");
    }

    /// <summary>
    /// Stops any ongoing discovery scan.
    /// </summary>
    public void CancelDiscovery()
    {
        _discoveryCts?.Cancel();
    }

    /// <summary>
    /// Stops reconnection attempts (call when app goes to background).
    /// </summary>
    public void StopReconnecting()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
    }

    /// <summary>
    /// Resumes reconnection if disconnected (call when app returns to foreground).
    /// </summary>
    public async Task ResumeConnectionAsync()
    {
        if (SavedConfig == null || IsConnected) return;
        await AutoConnectAsync();
    }

    /// <summary>
    /// Computes a heuristic score for an unknown BLE device.
    /// Higher score = more likely to be a barcode scanner.
    /// </summary>
    internal int ComputeHeuristicScore(string? deviceName, IReadOnlyList<Guid> serviceUuids)
    {
        if (_knownScanners == null) return 0;

        var score = 0;
        var nameLower = deviceName?.ToLowerInvariant() ?? "";

        // Scanner-related keywords in device name (+30 each)
        foreach (var keyword in _knownScanners.HeuristicKeywords.ScannerNames)
        {
            if (nameLower.Contains(keyword, StringComparison.Ordinal))
                score += 30;
        }

        // Manufacturer keywords in device name (+20 each)
        foreach (var keyword in _knownScanners.HeuristicKeywords.ManufacturerNames)
        {
            if (nameLower.Contains(keyword, StringComparison.Ordinal))
                score += 20;
        }

        // Known scanner service UUIDs (+40)
        var knownUuids = _knownScanners.KnownScannerServiceUuids
            .Select(s => Guid.Parse(s)).ToHashSet();
        foreach (var uuid in serviceUuids)
        {
            if (knownUuids.Contains(uuid))
                score += 40;
        }

        // HID service UUID (+25)
        if (serviceUuids.Any(u => u == HidServiceUuid))
            score += 25;

        return score;
    }

    /// <summary>
    /// Parses a barcode string from raw BLE characteristic data.
    /// Handles common formats: UTF-8 with optional CR/LF terminators.
    /// </summary>
    internal static string? ParseBarcodeFromBytes(byte[] data)
    {
        if (data == null || data.Length == 0) return null;

        var text = Encoding.UTF8.GetString(data).Trim('\r', '\n', '\0');
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private void OnBarcodeCharacteristicUpdated(object? sender, CharacteristicUpdatedEventArgs e)
    {
        var barcode = ParseBarcodeFromBytes(e.Characteristic.Value);
        if (barcode == null) return;

        // Duplicate suppression
        var now = DateTime.UtcNow;
        if (barcode == _lastBarcode && now - _lastBarcodeTime < DuplicateWindow)
            return;

        _lastBarcode = barcode;
        _lastBarcodeTime = now;

        // Haptic feedback
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(100)); }
            catch { /* Vibration may not be available */ }
        });

        // Broadcast to all subscribers
        WeakReferenceMessenger.Default.Send(new BleScannerBarcodeMessage(barcode));
    }

    private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        if (SavedConfig == null || e.Device.Id != SavedConfig.DeviceId) return;

        if (_barcodeCharacteristic != null)
        {
            _barcodeCharacteristic.ValueUpdated -= OnBarcodeCharacteristicUpdated;
            _barcodeCharacteristic = null;
        }
        _connectedDevice = null;
        SetState(BleScannerConnectionState.Disconnected);

        // Start reconnection
        _ = ReconnectWithBackoffAsync();
    }

    private async Task ReconnectWithBackoffAsync()
    {
        StopReconnecting();
        _reconnectCts = new CancellationTokenSource();
        var ct = _reconnectCts.Token;

        SetState(BleScannerConnectionState.Reconnecting);

        var delays = new[] { 1000, 2000, 4000, 8000, 30000 };
        for (var attempt = 0; attempt < delays.Length && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(delays[attempt], ct);
                if (ct.IsCancellationRequested) break;

                Console.WriteLine($"[BLE] Reconnect attempt {attempt + 1}/{delays.Length}");
                await AutoConnectAsync();
                if (IsConnected) return;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[BLE] Reconnect error: {ex.Message}");
            }
        }

        if (!IsConnected)
            SetState(BleScannerConnectionState.Disconnected);
    }

    private async Task<ICharacteristic?> FindBarcodeCharacteristicAsync(IDevice device, DiscoveredBleScanner? scanner)
    {
        var services = await device.GetServicesAsync();

        // First try known scanner service/characteristic UUIDs
        if (_knownScanners != null)
        {
            foreach (var knownScanner in _knownScanners.Scanners)
            {
                // Check if this device matches the known scanner by name
                var nameMatches = scanner != null && knownScanner.NamePatterns
                    .Any(p => scanner.Name.Contains(p, StringComparison.OrdinalIgnoreCase));

                foreach (var serviceUuidStr in knownScanner.ServiceUuids)
                {
                    var serviceUuid = Guid.Parse(serviceUuidStr);
                    var service = services.FirstOrDefault(s => s.Id == serviceUuid);
                    if (service == null) continue;

                    if (knownScanner.CharacteristicUuid != null)
                    {
                        var charUuid = Guid.Parse(knownScanner.CharacteristicUuid);
                        var characteristics = await service.GetCharacteristicsAsync();
                        var characteristic = characteristics.FirstOrDefault(c => c.Id == charUuid);
                        if (characteristic != null && characteristic.CanUpdate)
                            return characteristic;
                    }
                }
            }
        }

        // Fallback: search all services for a notify characteristic
        foreach (var service in services)
        {
            try
            {
                var characteristics = await service.GetCharacteristicsAsync();
                foreach (var characteristic in characteristics)
                {
                    if (characteristic.CanUpdate)
                        return characteristic;
                }
            }
            catch
            {
                // Some services may not be readable
            }
        }

        return null;
    }

    private static async Task<ICharacteristic?> FindCharacteristicByIdAsync(
        IDevice device, Guid? serviceUuid, Guid? characteristicUuid)
    {
        if (serviceUuid == null || characteristicUuid == null) return null;

        try
        {
            var service = await device.GetServiceAsync(serviceUuid.Value);
            if (service == null) return null;

            var characteristic = await service.GetCharacteristicAsync(characteristicUuid.Value);
            return characteristic?.CanUpdate == true ? characteristic : null;
        }
        catch
        {
            return null;
        }
    }

    private (bool isKnown, string? manufacturer, string? model) MatchKnownScanner(
        string deviceName, List<Guid> serviceUuids)
    {
        if (_knownScanners == null) return (false, null, null);

        foreach (var scanner in _knownScanners.Scanners)
        {
            // Check name patterns
            var nameMatch = scanner.NamePatterns
                .Any(p => deviceName.Contains(p, StringComparison.OrdinalIgnoreCase));

            // Check service UUIDs
            var uuidMatch = scanner.ServiceUuids
                .Any(u => serviceUuids.Contains(Guid.Parse(u)));

            if (nameMatch || uuidMatch)
            {
                return (true, scanner.Manufacturer, scanner.Models.FirstOrDefault());
            }
        }

        return (false, null, null);
    }

    private static List<Guid> ParseServiceUuids(byte[] data)
    {
        var uuids = new List<Guid>();
        if (data == null) return uuids;

        // 128-bit UUIDs are 16 bytes each
        for (var i = 0; i + 16 <= data.Length; i += 16)
        {
            try
            {
                var bytes = new byte[16];
                Array.Copy(data, i, bytes, 0, 16);
                uuids.Add(new Guid(bytes));
            }
            catch
            {
                // Skip malformed UUID
            }
        }

        return uuids;
    }

    private void SetState(BleScannerConnectionState state)
    {
        ConnectionState = state;
        WeakReferenceMessenger.Default.Send(new BleScannerStateMessage(state));
    }

    private void LoadSavedConfig()
    {
        var deviceIdStr = Preferences.Default.Get<string?>(PrefKeyDeviceId, null);
        var deviceName = Preferences.Default.Get<string?>(PrefKeyDeviceName, null);

        if (deviceIdStr == null || deviceName == null || !Guid.TryParse(deviceIdStr, out var deviceId))
        {
            SavedConfig = null;
            return;
        }

        var serviceUuidStr = Preferences.Default.Get<string?>(PrefKeyServiceUuid, null);
        var charUuidStr = Preferences.Default.Get<string?>(PrefKeyCharacteristicUuid, null);

        SavedConfig = new BleScannerConfig
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            Manufacturer = Preferences.Default.Get<string?>(PrefKeyManufacturer, null),
            ServiceUuid = serviceUuidStr != null && Guid.TryParse(serviceUuidStr, out var su) ? su : null,
            CharacteristicUuid = charUuidStr != null && Guid.TryParse(charUuidStr, out var cu) ? cu : null
        };
    }

    private void SaveConfig(BleScannerConfig config)
    {
        SavedConfig = config;
        Preferences.Default.Set(PrefKeyDeviceId, config.DeviceId.ToString());
        Preferences.Default.Set(PrefKeyDeviceName, config.DeviceName);
        Preferences.Default.Set(PrefKeyManufacturer, config.Manufacturer ?? "");
        Preferences.Default.Set(PrefKeyServiceUuid, config.ServiceUuid?.ToString() ?? "");
        Preferences.Default.Set(PrefKeyCharacteristicUuid, config.CharacteristicUuid?.ToString() ?? "");
    }

    private void ClearSavedConfig()
    {
        SavedConfig = null;
        Preferences.Default.Remove(PrefKeyDeviceId);
        Preferences.Default.Remove(PrefKeyDeviceName);
        Preferences.Default.Remove(PrefKeyManufacturer);
        Preferences.Default.Remove(PrefKeyServiceUuid);
        Preferences.Default.Remove(PrefKeyCharacteristicUuid);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _discoveryCts?.Cancel();
        _discoveryCts?.Dispose();
        _adapter.DeviceDisconnected -= OnDeviceDisconnected;

        if (_barcodeCharacteristic != null)
            _barcodeCharacteristic.ValueUpdated -= OnBarcodeCharacteristicUpdated;

        _connectedDevice?.Dispose();
    }
}
