using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Shared.Net;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Phase 3 chunk 3.B — open-redirect protection at the
/// <see cref="ExternalAuthApiController.FormPostCallback"/> sink. The action is
/// the server-to-client bounce that OAuth providers POST to with their
/// authorization code; <c>returnUrl</c> arrives as a query parameter from
/// whatever the SPA put on the request when initiating the flow. Until Phase 3
/// the URL was URL-escaped and forwarded verbatim. Now it must pass the host
/// allow-list before propagating.
/// </summary>
public class ExternalAuthApiControllerRedirectTests
{
    private static ExternalAuthApiController BuildController(params string[] allowedHosts)
    {
        var options = Options.Create(new RedirectUriAllowListOptions { Hosts = allowedHosts.ToList() });
        var validator = new RedirectUrlValidator(options);

        var controller = new ExternalAuthApiController(
            externalAuthService: Mock.Of<IExternalAuthService>(),
            passkeyService: Mock.Of<IPasskeyService>(),
            redirectValidator: validator,
            featureFlags: Mock.Of<Famick.HomeManagement.FeatureFlags.IFeatureFlagService>(),
            settings: Options.Create(new ExternalAuthSettings()),
            logger: NullLogger<ExternalAuthApiController>.Instance);

        return controller;
    }

    [Fact]
    public void FormPostCallback_drops_off_allow_list_returnUrl()
    {
        var controller = BuildController("app.famick.com");

        var result = controller.FormPostCallback(
            provider: "google",
            code: "abc",
            state: "xyz",
            error: null,
            errorDescription: null,
            returnUrl: "https://evil.example/x");

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("/auth/external/callback/google");
        redirect.Url.Should().Contain("code=abc");
        redirect.Url.Should().Contain("state=xyz");
        redirect.Url.Should().NotContain("evil.example");
        redirect.Url.Should().NotContain("returnUrl=");
    }

    [Fact]
    public void FormPostCallback_drops_protocol_relative_returnUrl()
    {
        var controller = BuildController("app.famick.com");

        var result = controller.FormPostCallback(
            provider: "google",
            code: "abc",
            state: "xyz",
            error: null,
            errorDescription: null,
            returnUrl: "//attacker.example/path");

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().NotContain("attacker.example");
        redirect.Url.Should().NotContain("returnUrl=");
    }

    [Fact]
    public void FormPostCallback_propagates_allow_listed_returnUrl()
    {
        var controller = BuildController("app.famick.com");

        var result = controller.FormPostCallback(
            provider: "google",
            code: "abc",
            state: "xyz",
            error: null,
            errorDescription: null,
            returnUrl: "https://app.famick.com");

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("returnUrl=");
        // URL-encoded canonical form survives through Uri.EscapeDataString.
        redirect.Url.Should().Contain("https%3A%2F%2Fapp.famick.com");
    }

    [Fact]
    public void FormPostCallback_propagates_relative_returnUrl()
    {
        var controller = BuildController("app.famick.com");

        var result = controller.FormPostCallback(
            provider: "google",
            code: "abc",
            state: "xyz",
            error: null,
            errorDescription: null,
            returnUrl: "/dashboard");

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        // URL-encoded "/dashboard" → "%2Fdashboard"
        redirect.Url.Should().Contain("returnUrl=%2Fdashboard");
    }

    [Fact]
    public void FormPostCallback_handles_missing_returnUrl_gracefully()
    {
        var controller = BuildController("app.famick.com");

        var result = controller.FormPostCallback(
            provider: "google",
            code: "abc",
            state: "xyz",
            error: null,
            errorDescription: null,
            returnUrl: null);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("/auth/external/callback/google");
        redirect.Url.Should().Contain("code=abc");
        redirect.Url.Should().NotContain("returnUrl=");
    }

    [Fact]
    public void FormPostCallback_forwards_error_response()
    {
        // Even on OAuth error, the callback bounces through with error params.
        // Validator shouldn't interfere with the error path.
        var controller = BuildController("app.famick.com");

        var result = controller.FormPostCallback(
            provider: "google",
            code: null,
            state: "xyz",
            error: "access_denied",
            errorDescription: "user denied access",
            returnUrl: null);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("error=access_denied");
        redirect.Url.Should().Contain("error_description=");
    }
}
