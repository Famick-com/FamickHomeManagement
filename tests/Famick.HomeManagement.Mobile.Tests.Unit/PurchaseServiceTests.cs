using Famick.HomeManagement.Mobile.Services;
using FluentAssertions;
using Maui.RevenueCat.InAppBilling.Enums;
using Maui.RevenueCat.InAppBilling.Models;
using Maui.RevenueCat.InAppBilling.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Mobile.Tests.Unit;

/// <summary>
/// Which household a purchase belongs to, and when buying is allowed at all.
/// </summary>
/// <remarks>
/// The store keeps one current app user, and the cloud matches a purchase to a household by
/// parsing that value as a tenant id. Every failure here has the same shape and none of it
/// is visible: the money is taken, no tier arrives, and neither side logs a problem.
/// </remarks>
public class PurchaseServiceTests
{
    private static readonly Guid Household = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherHousehold = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IRevenueCatBilling> _billing = new(MockBehavior.Loose);

    private static IStoreConfiguration Config(bool cloud = true, string key = "test_key") =>
        Mock.Of<IStoreConfiguration>(c => c.IsCloudHousehold == cloud && c.ApiKey == key);

    private PurchaseService Service(IStoreConfiguration? configuration = null) =>
        new(_billing.Object, configuration ?? Config(), NullLogger<PurchaseService>.Instance);

    /// <summary>Puts the SDK in the state it reaches after a successful first bind.</summary>
    private void GivenInitializedFor(Guid tenantId)
    {
        _billing.Setup(b => b.IsInitialized()).Returns(true);
        _billing.Setup(b => b.GetAppUserId()).Returns(tenantId.ToString());
    }

    // ---------- binding ----------

    /// <summary>
    /// The store is told the tenant id, because that is the only value the cloud can match
    /// a purchase back to a household with.
    /// </summary>
    [Fact]
    public async Task InitializingTellsTheStoreWhichHouseholdIsBuying()
    {
        _billing.Setup(b => b.IsInitialized()).Returns(false);

        await Service().InitializeAsync(Household);

        _billing.Verify(b => b.Initialize("test_key", Household.ToString()), Times.Once);
    }

    [Fact]
    public async Task InitializingAgainForTheSameHouseholdDoesNothing()
    {
        GivenInitializedFor(Household);

        await Service().InitializeAsync(Household);

        _billing.Verify(b => b.Initialize(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _billing.Verify(b => b.Login(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Switching households re-points the store. Without this a purchase would be credited
    /// to whoever signed in first.
    /// </summary>
    [Fact]
    public async Task SwitchingHouseholdsRepointsTheStore()
    {
        GivenInitializedFor(OtherHousehold);
        _billing.Setup(b => b.Login(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerInfoResultDto());

        var service = Service();
        await service.InitializeAsync(Household);

        _billing.Verify(b => b.Login(Household.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        service.IsAvailable.Should().BeTrue();
    }

    /// <summary>
    /// A re-point that fails leaves the store initialised and still pointed at the previous
    /// household, so buying has to stop until it succeeds.
    /// </summary>
    [Fact]
    public async Task AFailedRepointDisablesBuying()
    {
        GivenInitializedFor(OtherHousehold);
        _billing.Setup(b => b.Login(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerInfoResultDto { Error = PurchaseErrorStatus.NetworkError });

        var service = Service();
        await service.InitializeAsync(Household);

        service.IsAvailable.Should().BeFalse("the store is still pointed at the other household");
    }

    // ---------- when the store may be used at all ----------

    [Fact]
    public async Task AHouseholdOnItsOwnServerIsNeverSoldAnything()
    {
        var service = Service(Config(cloud: false));

        await service.InitializeAsync(Household);

        _billing.Verify(b => b.Initialize(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        service.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ABuildWithNoStoreKeyOffersNothing()
    {
        var service = Service(Config(key: string.Empty));

        await service.InitializeAsync(Household);

        _billing.Verify(b => b.Initialize(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        service.IsAvailable.Should().BeFalse();
    }

    /// <summary>
    /// Initialised is not the same as bound to somebody.
    /// </summary>
    [Fact]
    public void AnInitializedStoreThatHasBoundToNobodyIsNotAvailable()
    {
        _billing.Setup(b => b.IsInitialized()).Returns(true);

        Service().IsAvailable.Should().BeFalse();
    }

    /// <summary>
    /// Signing out unbinds the store.
    /// </summary>
    /// <remarks>
    /// Logout returns the store to an anonymous user but leaves it initialised. A binding
    /// left behind would keep buying available against a household nobody is signed in to,
    /// and the purchase would carry an anonymous id the cloud cannot match to a tenant.
    /// </remarks>
    [Fact]
    public async Task SigningOutStopsTheStoreBeingUsed()
    {
        _billing.Setup(b => b.IsInitialized()).Returns(false);
        _billing.Setup(b => b.Logout(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerInfoResultDto());

        var service = Service();
        await service.InitializeAsync(Household);

        // The store reports itself initialised from here on, as it would after a real bind.
        _billing.Setup(b => b.IsInitialized()).Returns(true);
        service.IsAvailable.Should().BeTrue();

        await service.LogoutAsync();

        service.IsAvailable.Should().BeFalse();
    }

    /// <summary>A store that throws on sign-out must still not remain bound.</summary>
    [Fact]
    public async Task SigningOutUnbindsEvenIfTheStoreThrows()
    {
        _billing.Setup(b => b.IsInitialized()).Returns(false);

        var service = Service();
        await service.InitializeAsync(Household);

        _billing.Setup(b => b.IsInitialized()).Returns(true);
        _billing.Setup(b => b.Logout(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        await service.LogoutAsync();

        service.IsAvailable.Should().BeFalse();
    }

    // ---------- refusing to trade ----------

    [Fact]
    public async Task NothingIsBoughtWhileTheStoreIsUnusable()
    {
        var result = await Service(Config(cloud: false)).PurchaseAsync("famick_home_monthly");

        result.Outcome.Should().Be(PurchaseOutcome.Failed);
        _billing.Verify(b => b.PurchaseProduct(It.IsAny<PackageDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NothingIsRestoredWhileTheStoreIsUnusable()
    {
        var result = await Service(Config(cloud: false)).RestoreAsync();

        result.Outcome.Should().Be(PurchaseOutcome.Failed);
        _billing.Verify(b => b.RestoreTransactions(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AskingForAPlanTheStoreIsNotSellingDoesNotBuyAnything()
    {
        GivenInitializedFor(Household);
        _billing.Setup(b => b.GetOfferings(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfferingsResultDto { Value = [] });

        var service = Service();
        await service.InitializeAsync(Household);

        var result = await service.PurchaseAsync("famick_home_monthly");

        result.Outcome.Should().Be(PurchaseOutcome.Failed);
        _billing.Verify(b => b.PurchaseProduct(It.IsAny<PackageDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoPlansAreOfferedWhileTheStoreIsUnusable()
    {
        var plans = await Service(Config(cloud: false)).GetPlansAsync();

        plans.Should().BeEmpty();
        _billing.Verify(b => b.GetOfferings(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
