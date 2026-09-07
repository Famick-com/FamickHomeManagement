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
    [InlineData(SubscriptionFeatureMap.Vehicles, SubscriptionTier.Home)]
    [InlineData(SubscriptionFeatureMap.StorageBins, SubscriptionTier.Home)]
    [InlineData(SubscriptionFeatureMap.MealPlanner, SubscriptionTier.Home)]
    [InlineData(SubscriptionFeatureMap.Analytics, SubscriptionTier.Pro)]
    [InlineData(SubscriptionFeatureMap.ScheduledExport, SubscriptionTier.Pro)]
    [InlineData(SubscriptionFeatureMap.ApiAccess, SubscriptionTier.Pro)]
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
    [InlineData(SubscriptionFeatureMap.Contacts, SubscriptionTier.Pro, true)]
    [InlineData(SubscriptionFeatureMap.Contacts, SubscriptionTier.Free, false)]
    [InlineData(SubscriptionFeatureMap.Shopping, SubscriptionTier.Home, true)]
    [InlineData(SubscriptionFeatureMap.Shopping, SubscriptionTier.Organize, false)]
    [InlineData(SubscriptionFeatureMap.Shopping, SubscriptionTier.Free, false)]
    [InlineData(SubscriptionFeatureMap.Analytics, SubscriptionTier.Pro, true)]
    [InlineData(SubscriptionFeatureMap.Analytics, SubscriptionTier.Home, false)]
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
        features.Values.Should().Contain(SubscriptionTier.Pro);
    }

    [Theory]
    [InlineData(SubscriptionFeatureMap.Contacts, true)]
    [InlineData(SubscriptionFeatureMap.Shopping, true)]
    [InlineData(SubscriptionFeatureMap.Analytics, false)]
    public void IsFeatureAvailable_CaseInsensitive(string featureArea, bool expectedForHome)
    {
        SubscriptionFeatureMap.IsFeatureAvailable(featureArea.ToUpperInvariant(), SubscriptionTier.Home)
            .Should().Be(expectedForHome);
    }

    [Fact]
    public void Pro_HasAccessToAllFeatures()
    {
        foreach (var feature in SubscriptionFeatureMap.GetAllFeatures().Keys)
        {
            SubscriptionFeatureMap.IsFeatureAvailable(feature, SubscriptionTier.Pro)
                .Should().BeTrue($"Pro tier should have access to '{feature}'");
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

    [Fact]
    public void ScheduledExportIsStillAPaidFeature()
    {
        // The compliance floor is free; the convenience on top of it is not.
        SubscriptionFeatureMap.IsFeatureAvailable(SubscriptionFeatureMap.ScheduledExport, SubscriptionTier.Free)
            .Should().BeFalse();
        SubscriptionFeatureMap.IsFeatureAvailable(SubscriptionFeatureMap.ScheduledExport, SubscriptionTier.Pro)
            .Should().BeTrue();
    }
}
