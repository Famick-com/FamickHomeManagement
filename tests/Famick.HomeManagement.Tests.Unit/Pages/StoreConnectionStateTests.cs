using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for store integration connection state display logic.
/// Recreates display model to avoid MAUI project dependency.
///
/// Under the capability split, product/price/availability works via client
/// credentials (no OAuth), so a linked + available store is usable. The user
/// OAuth link is an optional "cart link" surfaced separately.
/// </summary>
public class StoreConnectionStateTests
{
    private class TestStoreListItem
    {
        public bool IsConnected { get; set; }
        public bool SupportsCartLink { get; set; }
        public bool CartLinked { get; set; }
        public bool RequiresReauth { get; set; }
        public string? IntegrationType { get; set; }
        public bool HasIntegration => !string.IsNullOrEmpty(IntegrationType);

        // Primary status badge (a separate "Cart linked" badge is shown alongside).
        public string IntegrationBadgeText =>
            IsConnected ? "Price & availability active" : "Unavailable";
    }

    /// <summary>
    /// Mirrors the integration section rendering logic from StoreDetailPage.
    /// </summary>
    private static string DetermineIntegrationState(bool hasIntegration, bool isConnected, bool supportsCartLink, bool cartLinked)
    {
        if (!hasIntegration) return "not-linked";
        if (supportsCartLink && cartLinked) return "cart-linked";
        if (isConnected) return "price-active";
        return "unavailable";
    }

    /// <summary>
    /// The optional cart-link button text, or null when no cart button is shown.
    /// </summary>
    private static string? DetermineCartButtonText(bool supportsCartLink, bool cartLinked, bool requiresReauth)
    {
        if (!supportsCartLink) return null;
        if (cartLinked) return "Unlink cart";
        return requiresReauth ? "Re-authenticate cart" : "Link shopping cart";
    }

    [Fact]
    public void Available_ShowsPriceAvailabilityBadge()
    {
        var item = new TestStoreListItem { IntegrationType = "kroger", IsConnected = true };
        item.IntegrationBadgeText.Should().Be("Price & availability active");
    }

    [Fact]
    public void Unavailable_ShowsUnavailableBadge()
    {
        var item = new TestStoreListItem { IntegrationType = "kroger", IsConnected = false };
        item.IntegrationBadgeText.Should().Be("Unavailable");
    }

    [Fact]
    public void NoIntegration_ShowsNotLinkedState()
    {
        DetermineIntegrationState(false, false, false, false).Should().Be("not-linked");
    }

    [Fact]
    public void ClientCredentialsOnly_ShowsPriceActiveState_NoCartButton()
    {
        // Available, product-capable, no cart-link support.
        DetermineIntegrationState(true, true, supportsCartLink: false, cartLinked: false)
            .Should().Be("price-active");
        DetermineCartButtonText(supportsCartLink: false, cartLinked: false, requiresReauth: false)
            .Should().BeNull();
    }

    [Fact]
    public void CartCapableNotLinked_ShowsLinkCartButton()
    {
        DetermineIntegrationState(true, true, supportsCartLink: true, cartLinked: false)
            .Should().Be("price-active");
        DetermineCartButtonText(supportsCartLink: true, cartLinked: false, requiresReauth: false)
            .Should().Be("Link shopping cart");
    }

    [Fact]
    public void CartLinked_ShowsCartLinkedState_AndUnlinkButton()
    {
        DetermineIntegrationState(true, true, supportsCartLink: true, cartLinked: true)
            .Should().Be("cart-linked");
        DetermineCartButtonText(supportsCartLink: true, cartLinked: true, requiresReauth: false)
            .Should().Be("Unlink cart");
    }

    [Fact]
    public void CartReauthNeeded_ShowsReauthCartButton()
    {
        DetermineCartButtonText(supportsCartLink: true, cartLinked: false, requiresReauth: true)
            .Should().Be("Re-authenticate cart");
    }

    [Fact]
    public void OptimisticUpdate_AfterCartLinkSuccess_SetsCartLinkedState()
    {
        // Simulates what OnConnectClicked does after a successful cart-link OAuth.
        var store = new TestStoreListItem
        {
            IntegrationType = "kroger",
            IsConnected = true,
            SupportsCartLink = true,
            CartLinked = false,
            RequiresReauth = true
        };

        DetermineCartButtonText(store.SupportsCartLink, store.CartLinked, store.RequiresReauth)
            .Should().Be("Re-authenticate cart");

        // After successful OAuth - optimistic update
        store.CartLinked = true;
        store.RequiresReauth = false;

        DetermineIntegrationState(store.HasIntegration, store.IsConnected, store.SupportsCartLink, store.CartLinked)
            .Should().Be("cart-linked");
    }
}
