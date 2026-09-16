using System.Text.Json;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Mobile.Services;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Subscription;

/// <summary>
/// What the plans screen shows, and when it decides a purchase has actually landed.
/// </summary>
public class PlanPresentationTests
{
    private static SubscriptionPlan Plan(
        string productId,
        SubscriptionTier? tier = null,
        BillingPeriod period = BillingPeriod.Monthly,
        string? title = null,
        string price = "$8.99") =>
        new()
        {
            ProductId = productId,
            Title = title ?? productId,
            Price = price,
            Period = period,
            AdvertisedTier = tier
        };

    // ---------- grouping ----------

    /// <summary>
    /// Home above Organize, matching the ranking configured in App Store Connect. Backwards
    /// there is not a display bug — the store reads rank as the difference between an
    /// upgrade and a downgrade.
    /// </summary>
    [Fact]
    public void TheRicherPlanIsOfferedFirst()
    {
        var cards = PlanPresentation.GroupIntoTierCards(
        [
            Plan("famick_organize_monthly", SubscriptionTier.Organize),
            Plan("famick_home_monthly", SubscriptionTier.Home)
        ]);

        cards.Select(c => c.Tier).Should().Equal(SubscriptionTier.Home, SubscriptionTier.Organize);
    }

    [Fact]
    public void MonthlyAndAnnualShareOneCard()
    {
        var cards = PlanPresentation.GroupIntoTierCards(
        [
            Plan("famick_home_monthly", SubscriptionTier.Home, BillingPeriod.Monthly, price: "$8.99"),
            Plan("famick_home_annual", SubscriptionTier.Home, BillingPeriod.Annual, price: "$84.99")
        ]);

        var card = cards.Should().ContainSingle().Subject;
        card.Monthly!.Price.Should().Be("$8.99");
        card.Annual!.Price.Should().Be("$84.99");
    }

    /// <summary>
    /// The four products as configured in FHM-67, grouped the way the screen will show them.
    /// </summary>
    [Fact]
    public void TheFourProductsWeSellBecomeTwoCards()
    {
        var cards = PlanPresentation.GroupIntoTierCards(
        [
            Plan("famick_organize_monthly", SubscriptionTier.Organize, BillingPeriod.Monthly, price: "$3.99"),
            Plan("famick_organize_annual", SubscriptionTier.Organize, BillingPeriod.Annual, price: "$37.99"),
            Plan("famick_home_monthly", SubscriptionTier.Home, BillingPeriod.Monthly, price: "$8.99"),
            Plan("famick_home_annual", SubscriptionTier.Home, BillingPeriod.Annual, price: "$84.99")
        ]);

        cards.Should().HaveCount(2);
        cards.Should().OnlyContain(c => c.Monthly != null && c.Annual != null);
    }

    /// <summary>
    /// A plan the metadata says nothing about is still purchasable, so it is still shown —
    /// just without a tier heading. Hiding something the store will sell is the worse
    /// failure.
    /// </summary>
    [Fact]
    public void APlanWithNoTierIsStillOffered()
    {
        var cards = PlanPresentation.GroupIntoTierCards(
        [
            Plan("famick_home_monthly", SubscriptionTier.Home),
            Plan("famick_mystery_monthly")
        ]);

        cards.Should().HaveCount(2);
        cards.Last().Tier.Should().BeNull();
        cards.Last().Features.Should().BeEmpty("we cannot promise features for a tier we cannot identify");
    }

    [Fact]
    public void NothingOnOfferProducesNoCards()
    {
        PlanPresentation.GroupIntoTierCards([]).Should().BeEmpty();
        PlanPresentation.GroupIntoTierCards(null).Should().BeEmpty();
    }

    [Fact]
    public void TheStoresOwnTitleIsPreferredOverTheProductId()
    {
        var cards = PlanPresentation.GroupIntoTierCards(
            [Plan("famick_home_monthly", SubscriptionTier.Home, title: "Home")]);

        cards.Single().Title.Should().Be("Home");
    }

    // ---------- feature bullets ----------

    [Fact]
    public void TheRicherTierIncludesEverythingTheCheaperOneDoes()
    {
        var organize = PlanPresentation.BuildFeatureBullets(SubscriptionTier.Organize);
        var home = PlanPresentation.BuildFeatureBullets(SubscriptionTier.Home);

        home.Should().Contain(organize, "Home is sold as Organize plus more");
        home.Count.Should().BeGreaterThan(organize.Count);
    }

    // ---------- has the server caught up? ----------

    [Theory]
    [InlineData("Free", "Organize")]
    [InlineData("Organize", "Home")]
    [InlineData("Free", "Home")]
    public void AnyRiseInTierCountsAsTheUpgradeLanding(string before, string after)
    {
        PlanPresentation.HasUpgraded(before, wasExpired: false, after, isExpired: false)
            .Should().BeTrue();
    }

    [Fact]
    public void NothingChangingIsNotAnUpgrade()
    {
        PlanPresentation.HasUpgraded("Home", wasExpired: false, "Home", isExpired: false)
            .Should().BeFalse();
    }

    /// <summary>
    /// Guards against the obvious shortcut of testing "tier is no longer Free", which would
    /// read a downgrade as success and tell someone their purchase went through.
    /// </summary>
    [Fact]
    public void ADowngradeIsNotAnUpgrade()
    {
        PlanPresentation.HasUpgraded("Home", wasExpired: false, "Organize", isExpired: false)
            .Should().BeFalse();
    }

    /// <summary>
    /// Re-subscribing at the tier you already had moves nothing except whether the
    /// subscription is current — so that has to count, or the poll would time out on a
    /// purchase that worked.
    /// </summary>
    [Fact]
    public void ComingBackFromExpiredCountsEvenAtTheSameTier()
    {
        PlanPresentation.HasUpgraded("Home", wasExpired: true, "Home", isExpired: false)
            .Should().BeTrue();
    }

    // ---------- what a card should offer ----------

    /// <summary>
    /// The plan they are on is not for sale to them again.
    /// </summary>
    [Fact]
    public void TheCurrentPlanIsNotOfferedForPurchase()
    {
        PlanPresentation.ActionFor(SubscriptionTier.Organize, "Organize", isExpired: false)
            .Should().Be(PlanPresentation.PlanAction.Current);
    }

    [Fact]
    public void ARicherPlanIsAnUpgrade()
    {
        PlanPresentation.ActionFor(SubscriptionTier.Home, "Organize", isExpired: false)
            .Should().Be(PlanPresentation.PlanAction.Upgrade);
    }

    [Fact]
    public void ACheaperPlanIsASwitchRatherThanASecondSubscription()
    {
        PlanPresentation.ActionFor(SubscriptionTier.Organize, "Home", isExpired: false)
            .Should().Be(PlanPresentation.PlanAction.Downgrade);
    }

    /// <summary>
    /// A trial holds nothing, so everything is a straight purchase.
    /// </summary>
    [Fact]
    public void EverythingIsPurchasableDuringATrial()
    {
        PlanPresentation.ActionFor(SubscriptionTier.Home, "Free", isExpired: false)
            .Should().Be(PlanPresentation.PlanAction.Purchase);
    }

    /// <summary>
    /// Once it has lapsed, the plan they used to hold says nothing about what they can buy
    /// — and re-subscribing to the same one is the likeliest thing they want.
    /// </summary>
    [Fact]
    public void AnExpiredHouseholdCanBuyItsOldPlanAgain()
    {
        PlanPresentation.ActionFor(SubscriptionTier.Home, "Home", isExpired: true)
            .Should().Be(PlanPresentation.PlanAction.Purchase);
    }

    [Fact]
    public void APlanWithNoKnownTierIsStillPurchasable()
    {
        PlanPresentation.ActionFor(null, "Home", isExpired: false)
            .Should().Be(PlanPresentation.PlanAction.Purchase);
    }

    // ---------- has the household already got what a restore would give it? ----------

    /// <summary>
    /// A restore on a household already holding the entitlement has nothing to wait for.
    /// </summary>
    [Theory]
    [InlineData("Organize")]
    [InlineData("Home")]
    [InlineData("Pro")]
    public void APaidUnexpiredPlanNeedsNoRestore(string tier)
    {
        PlanPresentation.IsOnAPaidPlan(tier, isExpired: false).Should().BeTrue();
    }

    /// <summary>
    /// A trial is not a paid plan. The tier reads Free during one, and restoring then is
    /// expected to move the household onto something — a change worth waiting for.
    /// </summary>
    [Fact]
    public void ATrialIsNotAPaidPlan()
    {
        PlanPresentation.IsOnAPaidPlan("Free", isExpired: false).Should().BeFalse();
    }

    [Fact]
    public void AnExpiredPaidPlanStillNeedsRestoring()
    {
        PlanPresentation.IsOnAPaidPlan("Home", isExpired: true).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nonsense")]
    public void ATierWeCannotReadIsNotTreatedAsPaid(string? tier)
    {
        PlanPresentation.IsOnAPaidPlan(tier, isExpired: false).Should().BeFalse();
    }

    /// <summary>
    /// Guards the snapshot fallback used when the pre-purchase read fails.
    /// </summary>
    /// <remarks>
    /// That fallback once used <c>SubscriptionStateService.CurrentTier</c>, which answers
    /// Pro for an unknown tier — turning every comparison into "is this above Pro" and
    /// guaranteeing the timeout the fallback existed to prevent.
    /// </remarks>
    [Fact]
    public void NothingIsAnUpgradeOnPro()
    {
        PlanPresentation.HasUpgraded("Pro", wasExpired: false, "Home", isExpired: false)
            .Should().BeFalse();
        PlanPresentation.HasUpgraded("Pro", wasExpired: false, "Pro", isExpired: false)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "Home")]
    [InlineData("Home", null)]
    [InlineData("", "")]
    [InlineData("Nonsense", "Home")]
    public void ATierWeCannotReadIsNeverTreatedAsSuccess(string? before, string? after)
    {
        PlanPresentation.HasUpgraded(before, wasExpired: false, after, isExpired: false)
            .Should().BeFalse();
    }
}

/// <summary>
/// Reading the plan copy the store hands back. It is edited by hand in a web console, so
/// the cases that matter are the malformed ones.
/// </summary>
public class PlanCopyTests
{
    private static IReadOnlyDictionary<string, JsonElement> Metadata(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public void CopyIsReadPerProduct()
    {
        var copy = PlanCopy.From(Metadata("""
        {
          "famick_home_annual": {
            "tier": "Home",
            "title": "Home",
            "description": "Everything in Organize, plus shopping and inventory."
          }
        }
        """));

        var entry = copy.For("famick_home_annual");
        entry.Should().NotBeNull();
        entry!.Tier.Should().Be(SubscriptionTier.Home);
        entry.Title.Should().Be("Home");
        entry.Description.Should().StartWith("Everything in Organize");
    }

    [Fact]
    public void AProductTheMetadataDoesNotMentionSimplyHasNoCopy()
    {
        PlanCopy.From(Metadata("""{ "famick_home_annual": { "tier": "Home" } }"""))
            .For("famick_organize_monthly")
            .Should().BeNull();
    }

    /// <summary>
    /// A tier nobody can parse leaves the plan ungrouped rather than filed under a guess.
    /// </summary>
    [Fact]
    public void AnUnknownTierIsIgnoredRatherThanGuessedAt()
    {
        var entry = PlanCopy.From(Metadata("""
        { "famick_home_annual": { "tier": "Platinum", "title": "Home" } }
        """)).For("famick_home_annual");

        entry!.Tier.Should().BeNull();
        entry.Title.Should().Be("Home", "the rest of the entry is still usable");
    }

    /// <summary>
    /// One bad entry must not cost the others. Somebody paying should not meet a broken
    /// screen because of a typo in a console.
    /// </summary>
    [Fact]
    public void OneMalformedEntryDoesNotTakeDownTheRest()
    {
        var copy = PlanCopy.From(Metadata("""
        {
          "famick_home_annual": "this should have been an object",
          "famick_organize_monthly": { "tier": "Organize", "title": "Organize" }
        }
        """));

        copy.For("famick_home_annual").Should().BeNull();
        copy.For("famick_organize_monthly")!.Tier.Should().Be(SubscriptionTier.Organize);
    }

    [Fact]
    public void ValuesOfTheWrongTypeAreSkippedNotCoerced()
    {
        var entry = PlanCopy.From(Metadata("""
        { "famick_home_annual": { "tier": 2, "title": ["Home"], "description": null } }
        """)).For("famick_home_annual");

        entry.Should().NotBeNull();
        entry!.Tier.Should().BeNull();
        entry.Title.Should().BeNull();
        entry.Description.Should().BeNull();
    }

    /// <summary>
    /// A tier outside the enum is not a tier.
    /// </summary>
    /// <remarks>
    /// <c>Enum.TryParse</c> also accepts numeric text, so "99" parses happily to
    /// <c>(SubscriptionTier)99</c>. Feature lists are built as "everything at or below this
    /// tier", so that would advertise the whole Pro set under a tier nobody configured —
    /// on the screen where people decide what to pay for.
    /// </remarks>
    [Theory]
    [InlineData("99")]
    [InlineData("-1")]
    [InlineData("4")]
    public void ATierOutsideTheEnumIsNotAccepted(string raw)
    {
        var entry = PlanCopy.From(Metadata($$"""
        { "famick_home_annual": { "tier": "{{raw}}", "title": "Home" } }
        """)).For("famick_home_annual");

        entry!.Tier.Should().BeNull();
        entry.Title.Should().Be("Home", "the rest of the entry is still usable");
    }

    /// <summary>
    /// A number that does name a real tier is tolerated — it resolves to what whoever
    /// typed it meant. Only values outside the enum are the hazard.
    /// </summary>
    [Fact]
    public void ANumberNamingARealTierStillResolves()
    {
        PlanCopy.From(Metadata("""
        { "famick_home_annual": { "tier": "2" } }
        """)).For("famick_home_annual")!.Tier.Should().Be(SubscriptionTier.Home);
    }

    [Fact]
    public void NoMetadataAtAllIsAnEmptyLookupRatherThanAFailure()
    {
        PlanCopy.From(null).For("anything").Should().BeNull();
        PlanCopy.Empty.For("anything").Should().BeNull();
    }

    [Fact]
    public void ProductIdsAreMatchedCaseInsensitively()
    {
        PlanCopy.From(Metadata("""{ "Famick_Home_Annual": { "tier": "Home" } }"""))
            .For("famick_home_annual")
            .Should().NotBeNull();
    }
}
