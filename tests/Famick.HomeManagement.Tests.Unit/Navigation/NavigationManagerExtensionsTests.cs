using Famick.HomeManagement.UI;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Navigation;

public class NavigationManagerExtensionsTests
{
    [Theory]
    // App-absolute internal paths lose the single leading slash so they resolve
    // against <base href> (works at root AND under HA Ingress sub-path).
    [InlineData("/login", "login")]
    [InlineData("/settings/locations", "settings/locations")]
    [InlineData("/force-change-password", "force-change-password")]
    // Home: "/" -> "" (base href itself).
    [InlineData("/", "")]
    // Only the FIRST leading slash is stripped — an embedded returnUrl value
    // keeps its leading slash for the downstream validator.
    [InlineData("/login?returnUrl=/products/7", "login?returnUrl=/products/7")]
    // Already-relative paths pass through unchanged.
    [InlineData("products/123", "products/123")]
    [InlineData("", "")]
    // Absolute and protocol-relative URLs intentionally leave the app: untouched.
    [InlineData("https://accounts.google.com/o/oauth2/auth", "https://accounts.google.com/o/oauth2/auth")]
    [InlineData("http://example.test/x", "http://example.test/x")]
    [InlineData("//evil.example/x", "//evil.example/x")]
    public void ToNavTarget_MapsAppAbsoluteToBaseRelative_AndLeavesExternalAlone(string input, string expected)
    {
        NavigationManagerExtensions.ToNavTarget(input).Should().Be(expected);
    }
}
