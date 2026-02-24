namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for scanning barcodes using the device camera.
/// Platform-specific implementations (MAUI for mobile, stub for web).
/// </summary>
public interface IBarcodeScannerService
{
    /// <summary>
    /// Whether barcode scanning is available on this platform.
    /// Returns false on web, true on mobile devices with camera access.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Scan a barcode using the device camera.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The scanned barcode string, or null if cancelled/failed</returns>
    Task<string?> ScanBarcodeAsync(CancellationToken ct = default);
}
