using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for store integration connection state display logic.
/// Recreates display model to avoid MAUI project dependency.
/// </summary>
public class StoreConnectionStateTests
{
    private class TestStoreListItem
    {
        public bool IsConnected { get; set; }
        public bool RequiresReauth { get; set; }
        public string? IntegrationType { get; set; }
        public bool HasIntegration => !string.IsNullOrEmpty(IntegrationType);

        public string IntegrationBadgeText =>
            IsConnected ? "Connected"
            : RequiresReauth ? "Re-auth needed"
            : "Disconnected";
    }

    /// <summary>
    /// Mirrors the integration section rendering logic from StoreDetailPage
    /// </summary>
    private static string DetermineIntegrationState(bool hasIntegration, bool isConnected, bool requiresReauth)
    {
        if (!hasIntegration) return "not-linked";
        if (isConnected) return "connected";
        return "disconnected"; // includes reauth case
    }

    private static string DetermineButtonText(bool requiresReauth)
    {
        return requiresReauth ? "Re-authenticate" : "Connect";
    }

    [Fact]
    public void Connected_ShowsConnectedBadge()
    {
        var item = new TestStoreListItem { IntegrationType = "kroger", IsConnected = true };
        item.IntegrationBadgeText.Should().Be("Connected");
    }

    [Fact]
    public void Disconnected_ShowsDisconnectedBadge()
    {
        var item = new TestStoreListItem { IntegrationType = "kroger", IsConnected = false };
        item.IntegrationBadgeText.Should().Be("Disconnected");
    }

    [Fact]
    public void RequiresReauth_ShowsReauthBadge()
    {
        var item = new TestStoreListItem { IntegrationType = "kroger", IsConnected = false, RequiresReauth = true };
        item.IntegrationBadgeText.Should().Be("Re-auth needed");
    }

    [Fact]
    public void NoIntegration_ShowsNotLinkedState()
    {
        var state = DetermineIntegrationState(false, false, false);
        state.Should().Be("not-linked");
    }

    [Fact]
    public void Connected_ShowsConnectedState()
    {
        var state = DetermineIntegrationState(true, true, false);
        state.Should().Be("connected");
    }

    [Fact]
    public void Disconnected_ShowsDisconnectedState()
    {
        var state = DetermineIntegrationState(true, false, false);
        state.Should().Be("disconnected");
    }

    [Fact]
    public void RequiresReauth_ShowsReauthButton()
    {
        var text = DetermineButtonText(true);
        text.Should().Be("Re-authenticate");
    }

    [Fact]
    public void NotReauth_ShowsConnectButton()
    {
        var text = DetermineButtonText(false);
        text.Should().Be("Connect");
    }

    [Fact]
    public void OptimisticUpdate_AfterOAuthSuccess_SetsConnectedState()
    {
        // Simulates what OnConnectClicked does after successful OAuth
        var store = new TestStoreListItem
        {
            IntegrationType = "kroger",
            IsConnected = false,
            RequiresReauth = true
        };

        // Before OAuth
        store.IntegrationBadgeText.Should().Be("Re-auth needed");

        // After successful OAuth - optimistic update
        store.IsConnected = true;
        store.RequiresReauth = false;

        store.IntegrationBadgeText.Should().Be("Connected");
    }
}
