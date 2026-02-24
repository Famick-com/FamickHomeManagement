using Famick.HomeManagement.Core.Interfaces;
using Microsoft.JSInterop;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Web/WASM implementation of barcode scanner using html5-qrcode library.
/// Uses camera access via JavaScript interop.
/// </summary>
public class WebBarcodeScannerService : IBarcodeScannerService
{
    private readonly IJSRuntime _jsRuntime;
    private bool? _isAvailable;

    public WebBarcodeScannerService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Returns true if the browser supports camera access for barcode scanning.
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            // Check synchronously if we've already determined availability
            if (_isAvailable.HasValue)
                return _isAvailable.Value;

            // Default to true for web - actual check happens async
            // The JS will verify camera access is available
            return true;
        }
    }

    /// <summary>
    /// Scans a barcode using the device camera via JavaScript.
    /// </summary>
    public async Task<string?> ScanBarcodeAsync(CancellationToken ct = default)
    {
        try
        {
            // Check if scanning is available
            _isAvailable = await _jsRuntime.InvokeAsync<bool>("barcodeScannerIsAvailable", ct);
            if (!_isAvailable.Value)
            {
                return null;
            }

            // Start the scanner and wait for a result
            var result = await _jsRuntime.InvokeAsync<string?>("barcodeScannerStart", ct);
            return result;
        }
        catch (JSException ex)
        {
            Console.WriteLine($"Barcode scanner JS error: {ex.Message}");
            throw new Exception($"Camera access failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            // User cancelled or timeout - stop the scanner
            try
            {
                await _jsRuntime.InvokeVoidAsync("barcodeScannerStop");
            }
            catch { }
            return null;
        }
    }
}
