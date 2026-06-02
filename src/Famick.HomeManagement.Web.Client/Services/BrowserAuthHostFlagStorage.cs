using Famick.HomeManagement.UI.Services;
using Microsoft.JSInterop;

namespace Famick.HomeManagement.Web.Client.Services;

/// <summary>
/// Phase 5 chunk 5.K — localStorage-backed <see cref="IAuthHostFlagStorage"/>
/// for the Blazor WASM SPA. The flag persists across reloads so the
/// <see cref="AuthHostRoutingHandler"/> routes auth traffic correctly even
/// before the next config fetch completes.
/// </summary>
public class BrowserAuthHostFlagStorage : IAuthHostFlagStorage
{
    private const string Key = "famick.use_auth_famick_com";

    private readonly IJSRuntime _jsRuntime;

    public BrowserAuthHostFlagStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> GetUseAuthFamickComAsync()
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", Key);
            return bool.TryParse(value, out var parsed) && parsed;
        }
        catch (Exception)
        {
            // Defensive: any JS interop failure (pre-render, disposed circuit)
            // falls back to the safe default — keep auth on the same origin.
            return false;
        }
    }

    public async Task SetUseAuthFamickComAsync(bool value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", Key, value ? "true" : "false");
        }
        catch (Exception)
        {
            // Swallow — if we can't persist, the next config fetch will retry.
        }
    }
}
