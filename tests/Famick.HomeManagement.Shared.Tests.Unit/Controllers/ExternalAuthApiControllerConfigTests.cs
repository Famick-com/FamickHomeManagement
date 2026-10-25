using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.FeatureFlags;
using Famick.HomeManagement.Shared.Net;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FlagNames = Famick.HomeManagement.FeatureFlags.FeatureFlags;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Phase 4 chunk 4.E — <see cref="ExternalAuthApiController.GetAuthConfiguration"/>
/// now exposes a featureFlags object so mobile clients can decide whether to
/// render the two-step login UI (chunk 4.F) and whether to call /check
/// (chunk 4.C). Server-only flags must NOT appear in the surface.
/// </summary>
public class ExternalAuthApiControllerConfigTests
{
    private static ExternalAuthApiController BuildController(
        IFeatureFlagService featureFlags,
        EmailSettings? emailSettings = null,
        string? configuredBaseUrl = null,
        string requestHost = "home.example",
        bool passwordAuthEnabled = true)
    {
        var externalAuth = new Mock<IExternalAuthService>();
        externalAuth.Setup(s => s.GetEnabledProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalAuthProviderDto>());

        var options = Options.Create(new RedirectUriAllowListOptions { Hosts = new List<string>() });
        var validator = new RedirectUrlValidator(options);

        var controller = new ExternalAuthApiController(
            externalAuthService: externalAuth.Object,
            passkeyService: Mock.Of<IPasskeyService>(),
            redirectValidator: validator,
            featureFlags: featureFlags,
            settings: Options.Create(new ExternalAuthSettings { PasswordAuthEnabled = passwordAuthEnabled }),
            emailSettings: Options.Create(emailSettings ?? new EmailSettings()),
            logger: NullLogger<ExternalAuthApiController>.Instance);

        // PublicBaseUrl reads BaseUrl from the request's services, falling back to the request.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = configuredBaseUrl })
            .Build();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider(),
        };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString(requestHost);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    private static EmailSettings Ses() =>
        new() { Provider = EmailProvider.AwsSes, FromEmail = "noreply@famick.com" };

    private static async Task<bool> PasswordResetAvailable(ExternalAuthApiController sut)
    {
        var result = await sut.GetAuthConfiguration(default);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeOfType<AuthConfigurationDto>().Subject.PasswordResetAvailable;
    }

    [Fact]
    public async Task GetAuthConfiguration_returns_feature_flags_when_enabled()
    {
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabledAsync(FlagNames.TwoStepLoginV2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        flags.Setup(f => f.IsEnabledAsync(FlagNames.CheckEndpointEnabled, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = BuildController(flags.Object);

        var result = await sut.GetAuthConfiguration(default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<AuthConfigurationDto>().Subject;
        dto.FeatureFlags.Should().NotBeNull();
        dto.FeatureFlags.TwoStepLoginV2.Should().BeTrue();
        dto.FeatureFlags.CheckEndpointEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetAuthConfiguration_returns_false_flags_when_disabled()
    {
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = BuildController(flags.Object);

        var result = await sut.GetAuthConfiguration(default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<AuthConfigurationDto>().Subject;
        dto.FeatureFlags.TwoStepLoginV2.Should().BeFalse();
        dto.FeatureFlags.CheckEndpointEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthConfiguration_does_not_expose_server_only_flags()
    {
        // Server-only flags (use_auth_famick_com, proxy_*) must NOT have
        // properties on ClientFeatureFlagsDto. This is a structural test —
        // if someone adds a property in the future, this fires.
        var properties = typeof(ClientFeatureFlagsDto).GetProperties()
            .Select(p => p.Name)
            .ToList();

        properties.Should().NotContain("UseAuthFamickCom");
        properties.Should().NotContain("ProxySignupEnabled");
        properties.Should().NotContain("ProxyAgentEnabled");
        properties.Should().NotContain("ProxyTunnelEnabled");
        properties.Should().NotContain("StepUpEnabled",
            "step-up is enforced server-side and shouldn't leak to anonymous callers");
    }

    [Fact]
    public async Task PasswordResetAvailable_WhenMailIsConfiguredAndTheLinkBaseIsPublic()
    {
        var sut = BuildController(Mock.Of<IFeatureFlagService>(), Ses(), "https://app.famick.com");

        (await PasswordResetAvailable(sut)).Should().BeTrue();
    }

    [Fact]
    public async Task PasswordResetAvailable_UsesTheRequestHostWhenNoBaseUrlIsConfigured()
    {
        // A self-hosted install reached directly at the address its owner gave it.
        var sut = BuildController(Mock.Of<IFeatureFlagService>(), Ses(), requestHost: "192.168.1.20:5000");

        (await PasswordResetAvailable(sut)).Should().BeTrue();
    }

    [Fact]
    public async Task PasswordResetUnavailable_WhenSmtpHasNoHost()
    {
        var smtpWithoutHost = new EmailSettings { Provider = EmailProvider.Smtp, FromEmail = "home@example.com" };
        var sut = BuildController(Mock.Of<IFeatureFlagService>(), smtpWithoutHost, "https://home.example");

        (await PasswordResetAvailable(sut)).Should().BeFalse();
    }

    [Theory]
    [InlineData("localhost:5003")]
    [InlineData("127.0.0.1:5003")]
    public async Task PasswordResetUnavailable_WhenTheLinkWouldPointAtLoopback(string requestHost)
    {
        // What a request relayed through the AuthProxy tunnel looks like on an install with no BaseUrl.
        var sut = BuildController(Mock.Of<IFeatureFlagService>(), Ses(), requestHost: requestHost);

        (await PasswordResetAvailable(sut)).Should().BeFalse();
    }

    [Fact]
    public async Task PasswordResetUnavailable_WhenPasswordSignInIsOff()
    {
        var sut = BuildController(
            Mock.Of<IFeatureFlagService>(), Ses(), "https://app.famick.com", passwordAuthEnabled: false);

        (await PasswordResetAvailable(sut)).Should().BeFalse();
    }
}
