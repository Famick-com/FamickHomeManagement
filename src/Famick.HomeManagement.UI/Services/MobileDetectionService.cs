using Microsoft.JSInterop;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Web implementation of mobile detection service using JavaScript interop
/// </summary>
public class MobileDetectionService : IMobileDetectionService
{
    private readonly IJSRuntime _jsRuntime;
    private bool? _isMobile;
    private string? _platform;

    public MobileDetectionService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> IsMobileDeviceAsync()
    {
        if (_isMobile.HasValue)
            return _isMobile.Value;

        try
        {
            _isMobile = await _jsRuntime.InvokeAsync<bool>("famickMobileDetection.isMobile");
            return _isMobile.Value;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetMobilePlatformAsync()
    {
        if (_platform != null)
            return _platform;

        try
        {
            _platform = await _jsRuntime.InvokeAsync<string?>("famickMobileDetection.getPlatform");
            return _platform;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> TryOpenAppAsync(string deepLinkUrl, int timeoutMs = 2000)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("famickMobileDetection.tryOpenApp", deepLinkUrl, timeoutMs);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ShouldShowAppBannerAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("famickMobileDetection.shouldShowAppBanner");
        }
        catch
        {
            return false;
        }
    }

    public async Task DismissAppBannerAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("famickMobileDetection.dismissAppBanner");
        }
        catch
        {
            // Ignore errors
        }
    }
}
