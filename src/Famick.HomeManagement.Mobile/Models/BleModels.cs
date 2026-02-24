using System.Text.Json.Serialization;

namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// Connection state of the BLE scanner.
/// </summary>
public enum BleScannerConnectionState
{
    Disconnected,
    Scanning,
    Connecting,
    Connected,
    Reconnecting
}

/// <summary>
/// A BLE scanner discovered during scanning.
/// </summary>
public sealed class DiscoveredBleScanner
{
    public required Guid DeviceId { get; init; }
    public required string Name { get; init; }
    public int Rssi { get; init; }
    public bool IsKnownScanner { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public int HeuristicScore { get; init; }
    public List<Guid> ServiceUuids { get; init; } = [];
}

/// <summary>
/// Saved scanner configuration persisted in MAUI Preferences.
/// </summary>
public sealed class BleScannerConfig
{
    public required Guid DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public string? Manufacturer { get; init; }
    public Guid? ServiceUuid { get; init; }
    public Guid? CharacteristicUuid { get; init; }
}

/// <summary>
/// Root object for known_ble_scanners.json deserialization.
/// </summary>
public sealed class KnownScannerDatabase
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("scanners")]
    public List<KnownScannerEntry> Scanners { get; set; } = [];

    [JsonPropertyName("knownScannerServiceUuids")]
    public List<string> KnownScannerServiceUuids { get; set; } = [];

    [JsonPropertyName("heuristicKeywords")]
    public HeuristicKeywords HeuristicKeywords { get; set; } = new();
}

public sealed class KnownScannerEntry
{
    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = "";

    [JsonPropertyName("models")]
    public List<string> Models { get; set; } = [];

    [JsonPropertyName("serviceUuids")]
    public List<string> ServiceUuids { get; set; } = [];

    [JsonPropertyName("characteristicUuid")]
    public string? CharacteristicUuid { get; set; }

    [JsonPropertyName("namePatterns")]
    public List<string> NamePatterns { get; set; } = [];
}

public sealed class HeuristicKeywords
{
    [JsonPropertyName("scannerNames")]
    public List<string> ScannerNames { get; set; } = [];

    [JsonPropertyName("manufacturerNames")]
    public List<string> ManufacturerNames { get; set; } = [];
}
