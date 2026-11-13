using System.Reflection;
using Famick.HomeManagement.Shared.Net;
using Famick.HomeManagement.UI.Pages.Home;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>Home setup goes on to a page in the app when asked, and to My Home otherwise.</summary>
public class HomeSetupReturnUrlTests
{
    [Theory]
    [InlineData("/recipes/0b6d3c0e-6f4e-4a52-9a43-2f3c1d1f7a10", "/recipes/0b6d3c0e-6f4e-4a52-9a43-2f3c1d1f7a10")]
    [InlineData(null, "/my-home")]
    [InlineData("", "/my-home")]
    [InlineData("//evil.example/x", "/my-home")]
    [InlineData("https://evil.example/x", "/my-home")]
    [InlineData("https://app.famick.com", "/my-home")]
    public void Setup_goes_on_only_to_a_page_in_the_app(string? returnUrl, string expected)
    {
        var page = new HomeSetup { ReturnUrl = returnUrl };
        var allowList = new RedirectUriAllowListOptions { Hosts = ["app.famick.com"] };
        typeof(HomeSetup).GetProperty("RedirectValidator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, new RedirectUrlValidator(Options.Create(allowList)));

        typeof(HomeSetup).GetProperty("NextUrl", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(page).Should().Be(expected);
    }
}
