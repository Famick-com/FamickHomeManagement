using Famick.HomeManagement.Core.DTOs.Tenant;
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

    // ---- RefreshAsync: what a page reload depends on ---------------------------------------

    private static (SubscriptionStateProvider Sut, Mock<IApiClient> Api) CreateForRefresh(
        string? tokenInMemory,
        bool sessionRestores,
        TenantDto tenant)
    {
        var api = new Mock<IApiClient>();
        var tokens = new Mock<ITokenStorage>();
        tokens.Setup(t => t.GetAccessTokenAsync()).ReturnsAsync(tokenInMemory);
        api.Setup(a => a.RefreshSessionAsync()).ReturnsAsync(sessionRestores);
        api.Setup(a => a.GetAsync<TenantDto>("api/v1/tenant")).ReturnsAsync(ApiResult<TenantDto>.Success(tenant));

        var sut = new SubscriptionStateProvider(
            api.Object, tokens.Object, new MessageBus(NullLogger<MessageBus>.Instance, []));
        return (sut, api);
    }

    [Fact]
    public async Task After_a_reload_the_session_is_restored_before_the_household_is_fetched()
    {
        // A reload leaves no access token in memory; the session is the refresh cookie.
        var (sut, api) = CreateForRefresh(
            tokenInMemory: null,
            sessionRestores: true,
            new TenantDto { SubscriptionTier = string.Empty, IsUnrestricted = true });

        await sut.RefreshAsync();

        api.Verify(a => a.RefreshSessionAsync(), Times.Once);
        sut.IsUnrestricted.Should().BeTrue();
        sut.IsFeatureAvailable(SubscriptionFeatureMap.Inventory).Should().BeTrue();
    }

    [Fact]
    public async Task With_no_session_nothing_is_fetched()
    {
        var (sut, api) = CreateForRefresh(
            tokenInMemory: null,
            sessionRestores: false,
            new TenantDto { SubscriptionTier = "Home" });

        await sut.RefreshAsync();

        api.Verify(a => a.GetAsync<TenantDto>(It.IsAny<string>()), Times.Never);
        sut.CurrentTier.Should().Be(SubscriptionTier.Organize);
    }

    [Fact]
    public async Task With_a_token_in_memory_the_session_is_not_refreshed_again()
    {
        var (sut, api) = CreateForRefresh(
            tokenInMemory: "access-token",
            sessionRestores: true,
            new TenantDto { SubscriptionTier = "Home" });

        await sut.RefreshAsync();

        api.Verify(a => a.RefreshSessionAsync(), Times.Never);
        sut.CurrentTier.Should().Be(SubscriptionTier.Home);
    }
}
