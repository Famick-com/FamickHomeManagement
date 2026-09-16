using Famick.HomeManagement.Mobile.Services;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Mobile.Tests.Unit;

/// <summary>
/// Covers the decision that controls whether the app offers to sell a subscription.
/// </summary>
/// <remarks>
/// Self-hosted Famick is free under ELv2, so getting this wrong in the permissive
/// direction means showing a purchase screen to a household that already owns what it
/// would be buying — and, because tier gating keys off the same answer, restricting a
/// household that is entitled to everything.
/// </remarks>
public class ServerModeClassificationTests
{
    [Fact]
    public void Cloud_is_the_only_mode_with_a_subscription_to_sell()
    {
        ServerMode.Cloud.IsCloud().Should().BeTrue();
        ServerMode.Cloud.IsSelfHosted().Should().BeFalse();
    }

    [Fact]
    public void SelfHosted_is_not_a_cloud_tenant()
    {
        ServerMode.SelfHosted.IsCloud().Should().BeFalse();
        ServerMode.SelfHosted.IsSelfHosted().Should().BeTrue();
    }

    /// <summary>
    /// The regression this class exists for.
    /// </summary>
    /// <remarks>
    /// A proxied household runs its own server and is merely reached through
    /// auth.famick.com. The classification used to infer cloud-ness from the base URL
    /// host, and a proxied base URL genuinely is on a famick.com host — so it answered
    /// "cloud" and the app tried to sell a subscription to a household that had nothing
    /// to buy. Any reintroduction of URL sniffing fails here.
    /// </remarks>
    [Fact]
    public void Proxied_is_self_hosted_despite_being_reached_through_a_famick_host()
    {
        ServerMode.Proxied.IsCloud().Should().BeFalse();
        ServerMode.Proxied.IsSelfHosted().Should().BeTrue();
    }

    [Theory]
    [InlineData(ServerMode.Cloud)]
    [InlineData(ServerMode.SelfHosted)]
    [InlineData(ServerMode.Proxied)]
    public void The_two_answers_are_always_complementary(ServerMode mode)
    {
        mode.IsSelfHosted().Should().Be(!mode.IsCloud());
    }

    /// <summary>
    /// Documents the fallback rather than endorsing it.
    /// </summary>
    /// <remarks>
    /// An undefined value cannot arrive here through <c>ApiSettings.Mode</c>, which rejects
    /// one — but the arm exists, so what it does should be written down. Treating an unknown
    /// mode as cloud shows a purchase screen that may be pointless; treating it as
    /// self-hosted would hand out every paid feature instead. The former is the safer
    /// failure, which is why the default falls this way.
    /// </remarks>
    [Fact]
    public void An_undefined_mode_falls_back_to_cloud()
    {
        ((ServerMode)99).IsCloud().Should().BeTrue();
    }
}
