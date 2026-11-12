using Famick.HomeManagement.UI.Icons;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.JSInterop;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>The share icon follows the visitor's system, and the browser is asked once.</summary>
public class ShareIconsTests
{
    [Theory]
    [InlineData("apple", MudBlazor.Icons.Material.Filled.IosShare)]
    [InlineData("windows", CustomIcons.WindowsShare)]
    [InlineData("other", MudBlazor.Icons.Material.Filled.Share)]
    [InlineData(null, MudBlazor.Icons.Material.Filled.Share)]
    [InlineData("haiku", MudBlazor.Icons.Material.Filled.Share)]
    public async Task Each_system_gets_its_own_icon(string? style, string expected)
    {
        var js = new StyleJsRuntime(() => style);

        (await new ShareIcons(js).GetAsync()).Should().Be(expected);
    }

    [Fact]
    public async Task The_browser_is_asked_once()
    {
        var js = new StyleJsRuntime(() => "apple");
        var icons = new ShareIcons(js);

        await icons.GetAsync();
        await icons.GetAsync();

        js.Calls.Should().Be(1);
        js.Identifier.Should().Be("famickMobileDetection.getShareStyle");
    }

    [Fact]
    public async Task A_page_without_the_script_gets_the_usual_icon()
    {
        var js = new StyleJsRuntime(() => throw new JSException("famickMobileDetection is not defined"));

        (await new ShareIcons(js).GetAsync()).Should().Be(ShareIcons.Default);
    }

    private sealed class StyleJsRuntime(Func<string?> answer) : IJSRuntime
    {
        public int Calls { get; private set; }
        public string? Identifier { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls++;
            Identifier = identifier;
            return ValueTask.FromResult((TValue)(object?)answer()!);
        }
    }
}
