using Famick.HomeManagement.Core.Platform;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Platform;

public class PlatformResolverTests
{
    [Theory]
    // isMultiTenantEnabled, haIngressEnabled, expected
    [InlineData(true, false, ServerPlatform.Cloud)]
    [InlineData(true, true, ServerPlatform.Cloud)]          // multi-tenant wins over ingress
    [InlineData(false, true, ServerPlatform.HomeAssistant)]
    [InlineData(false, false, ServerPlatform.SelfHosted)]
    public void Resolve_returns_expected_platform(
        bool isMultiTenantEnabled, bool haIngressEnabled, ServerPlatform expected)
    {
        PlatformResolver.Resolve(isMultiTenantEnabled, haIngressEnabled)
            .Should().Be(expected);
    }

    [Fact]
    public void PlatformInfo_convenience_flags_match_platform()
    {
        new PlatformInfo(ServerPlatform.SelfHosted).IsSelfHosted.Should().BeTrue();
        new PlatformInfo(ServerPlatform.HomeAssistant).IsHomeAssistant.Should().BeTrue();
        new PlatformInfo(ServerPlatform.Cloud).IsCloud.Should().BeTrue();

        var cloud = new PlatformInfo(ServerPlatform.Cloud);
        cloud.IsSelfHosted.Should().BeFalse();
        cloud.IsHomeAssistant.Should().BeFalse();
    }
}
