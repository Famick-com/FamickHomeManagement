using Famick.HomeManagement.Core.Subscription;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Subscription;

public class SubscriptionFeatureMapTests
{
    [Theory]
    [InlineData(SubscriptionFeatureMap.Contacts, SubscriptionTier.Organize)]
    [InlineData(SubscriptionFeatureMap.Chores, SubscriptionTier.Organize)]
    [InlineData(SubscriptionFeatureMap.Equipment, SubscriptionTier.Organize)]
    [InlineData(SubscriptionFeatureMap.Todos, SubscriptionTier.Organize)]
    [InlineData(SubscriptionFeatureMap.Shopping, SubscriptionTier.Home)]
    [InlineData(SubscriptionFeatureMap.Inventory, SubscriptionTier.Home)]
    [InlineData(SubscriptionFeatureMap.Products, SubscriptionTier.Home)]
    [InlineData(SubscriptionFeatureMap.Recipes, SubscriptionTier.Home)]
    [InlineData(SubscriptionFeatureMap.StorageBins, SubscriptionTier.Home)]
    [InlineData(SubscriptionFeatureMap.MealPlanner, SubscriptionTier.Home)]
    public void GetRequiredTier_ReturnsCorrectTier(string featureArea, SubscriptionTier expectedTier)
    {
        SubscriptionFeatureMap.GetRequiredTier(featureArea).Should().Be(expectedTier);
    }

    [Fact]
    public void GetRequiredTier_UnknownFeature_ReturnsFree()
    {
        SubscriptionFeatureMap.GetRequiredTier("nonexistent").Should().Be(SubscriptionTier.Free);
    }

    [Theory]
    [InlineData(SubscriptionFeatureMap.Contacts, SubscriptionTier.Organize, true)]
    [InlineData(SubscriptionFeatureMap.Contacts, SubscriptionTier.Home, true)]
    [InlineData(SubscriptionFeatureMap.Contacts, SubscriptionTier.Free, false)]
    [InlineData(SubscriptionFeatureMap.Shopping, SubscriptionTier.Home, true)]
    [InlineData(SubscriptionFeatureMap.Shopping, SubscriptionTier.Organize, false)]
    [InlineData(SubscriptionFeatureMap.Shopping, SubscriptionTier.Free, false)]
    [InlineData(SubscriptionFeatureMap.Recipes, SubscriptionTier.Organize, false)]
    public void IsFeatureAvailable_RespectsHierarchy(string featureArea, SubscriptionTier currentTier, bool expected)
    {
        SubscriptionFeatureMap.IsFeatureAvailable(featureArea, currentTier).Should().Be(expected);
    }

    [Fact]
    public void GetFeatureDescription_ReturnsNonEmpty_ForKnownFeatures()
    {
        foreach (var feature in SubscriptionFeatureMap.GetAllFeatures().Keys)
        {
            SubscriptionFeatureMap.GetFeatureDescription(feature).Should().NotBeNullOrEmpty(
                $"feature '{feature}' should have a description");
        }
    }

    [Fact]
    public void GetFeatureDescription_UnknownFeature_ReturnsEmpty()
    {
        SubscriptionFeatureMap.GetFeatureDescription("nonexistent").Should().BeEmpty();
    }

    [Fact]
    public void GetAllFeatures_ReturnsAllTiers()
    {
        var features = SubscriptionFeatureMap.GetAllFeatures();
        features.Should().NotBeEmpty();
        features.Values.Should().Contain(SubscriptionTier.Organize);
        features.Values.Should().Contain(SubscriptionTier.Home);
    }

    [Theory]
    [InlineData(SubscriptionFeatureMap.Contacts, true)]
    [InlineData(SubscriptionFeatureMap.Shopping, false)]
    public void IsFeatureAvailable_CaseInsensitive(string featureArea, bool expectedForOrganize)
    {
        SubscriptionFeatureMap.IsFeatureAvailable(featureArea.ToUpperInvariant(), SubscriptionTier.Organize)
            .Should().Be(expectedForOrganize);
    }

    /// <summary>
    /// Home is the top plan, so it has to unlock everything in the map.
    /// </summary>
    /// <remarks>
    /// A feature mapped above every plan on sale is one nobody can buy, and both clients would
    /// still list it wherever they read the map.
    /// </remarks>
    [Fact]
    public void Home_HasAccessToAllFeatures()
    {
        foreach (var feature in SubscriptionFeatureMap.GetAllFeatures().Keys)
        {
            SubscriptionFeatureMap.IsFeatureAvailable(feature, SubscriptionTier.Home)
                .Should().BeTrue($"Home is the top plan and should have access to '{feature}'");
        }
    }

    /// <summary>
    /// Downloading your own data must not be behind a paywall.
    /// </summary>
    /// <remarks>
    /// CCPA and MHMDA both grant a right of access to personal data, and this product is US-only,
    /// so both apply. If somebody ever adds DataExport back to the tier map, this is the test that
    /// should stop them and make them read the reasoning first.
    /// </remarks>
    [Fact]
    public void DataExportIsAvailableOnEveryTier()
    {
        SubscriptionFeatureMap.GetRequiredTier(SubscriptionFeatureMap.DataExport)
            .Should().Be(SubscriptionTier.Free);

        foreach (var tier in Enum.GetValues<SubscriptionTier>())
        {
            SubscriptionFeatureMap.IsFeatureAvailable(SubscriptionFeatureMap.DataExport, tier)
                .Should().BeTrue("a data-access right cannot depend on {0}", tier);
        }
    }

    // ---- effective tier: one rule for the server and both clients ------------------------

    [Theory]
    [InlineData(SubscriptionTier.Free, true, SubscriptionTier.Home)]
    [InlineData(SubscriptionTier.Free, false, SubscriptionTier.Free)]
    [InlineData(SubscriptionTier.Organize, true, SubscriptionTier.Home)]
    [InlineData(SubscriptionTier.Organize, false, SubscriptionTier.Organize)]
    [InlineData(SubscriptionTier.Home, true, SubscriptionTier.Home)]
    [InlineData(SubscriptionTier.Home, false, SubscriptionTier.Home)]
    public void GetEffectiveTier_IsTheBetterOfBoughtAndTrial(SubscriptionTier tier, bool trial, SubscriptionTier expected)
    {
        SubscriptionFeatureMap.GetEffectiveTier(tier, trial).Should().Be(expected);
    }

    [Theory]
    [InlineData(SubscriptionTier.Organize)]
    [InlineData(SubscriptionTier.Home)]
    public void BuyingAnyTierDuringATrialNeverReducesAccess(SubscriptionTier bought)
    {
        var beforeBuying = SubscriptionFeatureMap.GetEffectiveTier(SubscriptionTier.Free, isTrialActive: true);
        var afterBuying = SubscriptionFeatureMap.GetEffectiveTier(bought, isTrialActive: true);

        ((int)afterBuying).Should().BeGreaterThanOrEqualTo((int)beforeBuying);
    }

    [Fact]
    public void AHouseholdThatBoughtOrganizeMidTrialStillHasHomeFeatures()
    {
        // The case that motivated the shared rule: paid, trial still running, Home-only feature.
        var effective = SubscriptionFeatureMap.GetEffectiveTier(SubscriptionTier.Organize, isTrialActive: true);

        SubscriptionFeatureMap.IsFeatureAvailable(SubscriptionFeatureMap.Recipes, effective).Should().BeTrue();
        SubscriptionFeatureMap.IsFeatureAvailable(SubscriptionFeatureMap.Inventory, effective).Should().BeTrue();
    }

    [Fact]
    public void OnceTheTrialEndsAnOrganizeHouseholdSettlesOntoOrganize()
    {
        var effective = SubscriptionFeatureMap.GetEffectiveTier(SubscriptionTier.Organize, isTrialActive: false);

        SubscriptionFeatureMap.IsFeatureAvailable(SubscriptionFeatureMap.Contacts, effective).Should().BeTrue();
        SubscriptionFeatureMap.IsFeatureAvailable(SubscriptionFeatureMap.Recipes, effective).Should().BeFalse();
    }

    [Theory]
    [InlineData(SubscriptionTier.Free, true, SubscriptionTier.Home)]
    [InlineData(SubscriptionTier.Free, false, SubscriptionTier.Home)]
    [InlineData(SubscriptionTier.Organize, true, SubscriptionTier.Home)]
    [InlineData(SubscriptionTier.Organize, false, SubscriptionTier.Organize)]
    [InlineData(SubscriptionTier.Home, false, SubscriptionTier.Home)]
    public void GetReadableTier_KeepsWhatTheHouseholdHad(SubscriptionTier tier, bool trial, SubscriptionTier expected)
    {
        SubscriptionFeatureMap.GetReadableTier(tier, trial).Should().Be(expected);
    }

    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    // Mirrors the cloud SubscriptionMiddleware: these are the cases it lets write.
    [Theory]
    [InlineData(SubscriptionTier.Free, true, null, true)]                  // trial running
    [InlineData(SubscriptionTier.Free, false, null, false)]                // trial over, never paid
    [InlineData(SubscriptionTier.Organize, false, null, true)]             // paid, no end date
    [InlineData(SubscriptionTier.Home, false, 1, true)]                    // paid, ends tomorrow
    [InlineData(SubscriptionTier.Home, false, -1, false)]                  // paid, ended yesterday
    [InlineData(SubscriptionTier.Organize, true, -1, true)]                // ended, but trial still running
    public void CanWrite_FollowsTheTrialAndTheSubscriptionEnd(
        SubscriptionTier tier, bool trial, int? expiresInDays, bool expected)
    {
        DateTime? expiresAt = expiresInDays.HasValue ? Now.AddDays(expiresInDays.Value) : null;

        SubscriptionFeatureMap.CanWrite(tier, trial, expiresAt, Now).Should().Be(expected);
    }

    [Fact]
    public void CanWrite_StopsAtTheMomentTheSubscriptionEnds()
    {
        SubscriptionFeatureMap.CanWrite(SubscriptionTier.Home, false, Now, Now).Should().BeFalse();
    }
}
