using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// The light/dark preference.
/// <para>
/// The behaviour worth protecting is the precedence: an explicit choice outranks the operating
/// system, and "never chose" is not the same as "chose light". Getting that wrong means either
/// a toggle that appears not to work, or an OS preference that silently overrides the user.
/// </para>
/// </summary>
public class ThemePreferenceTests
{
    /// <summary>
    /// The parameterless InvokeAsync&lt;T&gt;(identifier) and InvokeVoidAsync(identifier, args)
    /// are extension methods; both funnel into the two-parameter interface overload
    /// InvokeAsync&lt;T&gt;(string, object?[]?), which is what these mocks have to target. The
    /// CancellationToken overload is never reached from this service.
    /// </summary>
    private static Mock<IJSRuntime> Js(bool resolves)
    {
        var mock = new Mock<IJSRuntime>();
        mock.Setup(j => j.InvokeAsync<bool>("famickTheme.resolve", It.IsAny<object?[]?>()))
            .ReturnsAsync(resolves);
        return mock;
    }

    [Fact]
    public async Task BeforeInitialising_ItReportsLight()
    {
        // Light is the safer default while the answer is unknown: a dark screen nobody asked
        // for reads as broken.
        var preference = new ThemePreference(Js(true).Object);

        preference.IsDarkMode.Should().BeFalse();
    }

    [Fact]
    public async Task Initialise_TakesTheResolvedPreference()
    {
        var preference = new ThemePreference(Js(true).Object);

        await preference.InitializeAsync();

        preference.IsDarkMode.Should().BeTrue();
    }

    [Fact]
    public async Task Initialise_OnlyReadsOnce()
    {
        // Every themed screen calls this on first render, and there are eight of them.
        var js = Js(true);
        var preference = new ThemePreference(js.Object);

        await preference.InitializeAsync();
        await preference.InitializeAsync();
        await preference.InitializeAsync();

        js.Verify(j => j.InvokeAsync<bool>("famickTheme.resolve", It.IsAny<object?[]?>()),
            Times.Once);
    }

    [Fact]
    public async Task Initialise_WhenJavaScriptIsUnavailable_FallsBackToLight()
    {
        // Prerendering has no JS. Throwing here would take down every page that renders a theme.
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<bool>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .ThrowsAsync(new InvalidOperationException("no JS during prerender"));

        var preference = new ThemePreference(js.Object);

        await preference.InitializeAsync();

        preference.IsDarkMode.Should().BeFalse();
    }

    [Fact]
    public async Task Set_PersistsTheChoice()
    {
        // Without this the toggle flips a field and forgets, which is what made it look broken.
        var js = Js(false);
        var preference = new ThemePreference(js.Object);
        await preference.InitializeAsync();

        await preference.SetAsync(true);

        preference.IsDarkMode.Should().BeTrue();
        js.Verify(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "famickTheme.store", It.Is<object?[]?>(a => (bool)a![0]!)),
            Times.Once);
    }

    [Fact]
    public async Task Set_NotifiesOpenScreens()
    {
        // Eight components render a theme; they re-render from this rather than polling.
        var preference = new ThemePreference(Js(false).Object);
        await preference.InitializeAsync();

        var notifications = 0;
        preference.Changed += () => notifications++;

        await preference.SetAsync(true);

        notifications.Should().Be(1);
    }

    [Fact]
    public async Task Set_ToTheSameValue_ChangesNothing()
    {
        var preference = new ThemePreference(Js(true).Object);
        await preference.InitializeAsync();

        var notifications = 0;
        preference.Changed += () => notifications++;

        await preference.SetAsync(true);

        notifications.Should().Be(0);
    }

    [Fact]
    public async Task AnExplicitChoice_SurvivesALaterInitialise()
    {
        // A screen mounting after the user has chosen must not re-read and overwrite it.
        var js = Js(false);
        var preference = new ThemePreference(js.Object);

        await preference.SetAsync(true);
        await preference.InitializeAsync();

        preference.IsDarkMode.Should().BeTrue();
        js.Verify(j => j.InvokeAsync<bool>("famickTheme.resolve", It.IsAny<object?[]?>()),
            Times.Never);
    }

    [Fact]
    public async Task Toggle_FlipsAndPersists()
    {
        var preference = new ThemePreference(Js(false).Object);
        await preference.InitializeAsync();

        await preference.ToggleAsync();
        preference.IsDarkMode.Should().BeTrue();

        await preference.ToggleAsync();
        preference.IsDarkMode.Should().BeFalse();
    }
}
