using System.Reflection;
using Famick.HomeManagement.Shared.Net;
using Famick.HomeManagement.UI.Pages.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>Signing up from the sign-in page keeps where the visitor was headed, when that's safe.</summary>
public class LoginRegisterLinkTests
{
    [Theory]
    [InlineData("/shares/add/abc", "register?returnUrl=%2Fshares%2Fadd%2Fabc")]
    [InlineData(null, "register")]
    [InlineData("/", "register")]
    [InlineData("//evil.example/x", "register")]
    [InlineData("https://evil.example/x", "register")]
    public void The_register_link_carries_only_a_safe_return_address(string? returnUrl, string expected)
    {
        var login = new Login { ReturnUrl = returnUrl };
        typeof(Login).GetProperty("RedirectValidator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(login, new RedirectUrlValidator(Options.Create(new RedirectUriAllowListOptions())));

        typeof(Login).GetProperty("RegisterHref", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(login).Should().Be(expected);
    }
}
