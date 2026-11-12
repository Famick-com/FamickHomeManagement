using Famick.HomeManagement.UI.Icons;
using Microsoft.JSInterop;

namespace Famick.HomeManagement.UI.Services;

/// <summary>The family of share icon a visitor's system uses.</summary>
public enum ShareStyle
{
    Other,
    Apple,
    Windows
}

/// <summary>
/// The share icon the visitor's system uses: a box with an arrow up on Apple's, a box with an arrow
/// leaving its corner on Windows, and three joined dots elsewhere. Asked of the browser once and
/// reused, as <see cref="PlatformState"/> reuses the server's answer.
/// </summary>
public sealed class ShareIcons
{
    /// <summary>The icon shown until the browser has answered, and wherever it can't.</summary>
    public const string Default = MudBlazor.Icons.Material.Filled.Share;

    private readonly IJSRuntime _js;
    private Task<ShareStyle>? _style;

    public ShareIcons(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>The icon for this visitor's system.</summary>
    public async Task<string> GetAsync() => IconFor(await (_style ??= ReadStyleAsync()));

    public static string IconFor(ShareStyle style) => style switch
    {
        ShareStyle.Apple => MudBlazor.Icons.Material.Filled.IosShare,
        ShareStyle.Windows => CustomIcons.WindowsShare,
        _ => Default
    };

    public static ShareStyle Parse(string? style) => style switch
    {
        "apple" => ShareStyle.Apple,
        "windows" => ShareStyle.Windows,
        _ => ShareStyle.Other
    };

    private async Task<ShareStyle> ReadStyleAsync()
    {
        try
        {
            return Parse(await _js.InvokeAsync<string?>("famickMobileDetection.getShareStyle"));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // The script isn't on this page, or there's no browser to ask.
            return ShareStyle.Other;
        }
    }
}
