using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.FeatureFlags;
using Famick.HomeManagement.Shared.Net;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
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
    private static ExternalAuthApiController BuildController(IFeatureFlagService featureFlags)
    {
        var externalAuth = new Mock<IExternalAuthService>();
        externalAuth.Setup(s => s.GetEnabledProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalAuthProviderDto>());

        var options = Options.Create(new RedirectUriAllowListOptions { Hosts = new List<string>() });
        var validator = new RedirectUrlValidator(options);

        return new ExternalAuthApiController(
            externalAuthService: externalAuth.Object,
            passkeyService: Mock.Of<IPasskeyService>(),
            redirectValidator: validator,
            featureFlags: featureFlags,
            settings: Options.Create(new ExternalAuthSettings { PasswordAuthEnabled = true }),
            logger: NullLogger<ExternalAuthApiController>.Instance);
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
}
