using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.Core.Subscription;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Subscription;

/// <summary>
/// What the web client unlocks for the household the server reported.
/// </summary>
public class SubscriptionStateProviderTests
{
    private static SubscriptionStateProvider Create() => new(
        Mock.Of<IApiClient>(),
        Mock.Of<ITokenStorage>(),
        new MessageBus(NullLogger<MessageBus>.Instance, []));

    [Fact]
    public void An_unrestricted_server_unlocks_every_feature()
    {
        var sut = Create();

        sut.UpdateFromTenant(string.Empty, isTrialActive: false, isExpired: false, isUnrestricted: true);

        sut.IsUnrestricted.Should().BeTrue();
        foreach (var feature in SubscriptionFeatureMap.GetAllFeatures().Keys)
        {
            sut.IsFeatureAvailable(feature).Should().BeTrue($"'{feature}' should be open on an unrestricted server");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Platinum")]
    [InlineData("Pro")]
    [InlineData("3")]
    [InlineData("99")]
    public void A_tier_that_is_missing_or_unreadable_resolves_to_Organize(string? tier)
    {
        var sut = Create();

        sut.UpdateFromTenant(tier, isTrialActive: false, isExpired: false, isUnrestricted: false);

        sut.IsUnrestricted.Should().BeFalse();
        sut.CurrentTier.Should().Be(SubscriptionTier.Organize);
        sut.IsFeatureAvailable(SubscriptionFeatureMap.Contacts).Should().BeTrue();
        sut.IsFeatureAvailable(SubscriptionFeatureMap.Inventory).Should().BeFalse();
    }

    [Fact]
    public void Before_anything_is_reported_the_household_is_treated_as_Organize()
    {
        var sut = Create();

        sut.IsUnrestricted.Should().BeFalse();
        sut.CurrentTier.Should().Be(SubscriptionTier.Organize);
        sut.IsFeatureAvailable(SubscriptionFeatureMap.Shopping).Should().BeFalse();
    }

    [Fact]
    public void A_trial_still_lifts_an_Organize_household_to_Home()
    {
        var sut = Create();

        sut.UpdateFromTenant("Organize", isTrialActive: true, isExpired: false, isUnrestricted: false);

        sut.IsFeatureAvailable(SubscriptionFeatureMap.Inventory).Should().BeTrue();
    }

    [Fact]
    public void A_restricted_report_after_an_unrestricted_one_locks_again()
    {
        var sut = Create();
        sut.UpdateFromTenant(string.Empty, isTrialActive: false, isExpired: false, isUnrestricted: true);

        sut.UpdateFromTenant("Organize", isTrialActive: false, isExpired: false, isUnrestricted: false);

        sut.IsUnrestricted.Should().BeFalse();
        sut.IsFeatureAvailable(SubscriptionFeatureMap.Recipes).Should().BeFalse();
    }
}
