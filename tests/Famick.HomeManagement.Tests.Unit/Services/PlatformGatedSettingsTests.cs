using Famick.HomeManagement.Core.DTOs.Setup;
using Famick.HomeManagement.Core.Platform;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Covers the window in which a platform-gated page has not yet learned its
/// platform. Blazor renders once while <c>OnInitializedAsync</c> is still
/// pending, so anything gated on a plain <c>bool</c> that defaults to "not
/// multi-tenant" is visible for that first render — which on a multi-tenant
/// server means flashing the server-scoped Settings sections before they are
/// removed.
/// </summary>
public class PlatformGatedSettingsTests
{
    [Fact]
    public async Task GetAsync_yields_when_the_cache_is_cold()
    {
        // The premise of the whole gating problem: with no primed platform the
        // status call is a real round-trip, so GetAsync hands back an incomplete
        // task and the component renders before the answer arrives.
        var gate = new TaskCompletionSource<ApiResult<SetupStatusResponse>>();
        var apiClient = new Mock<IApiClient>();
        apiClient.Setup(c => c.GetSetupStatusAsync()).Returns(gate.Task);

        var pending = new PlatformState(apiClient.Object).GetAsync();

        pending.IsCompleted.Should().BeFalse();

        gate.SetResult(ApiResult<SetupStatusResponse>.Success(
            new SetupStatusResponse { Platform = ServerPlatform.Cloud }));
        (await pending).Should().Be(ServerPlatform.Cloud);
    }

    [Fact]
    public async Task GetAsync_does_not_yield_once_the_platform_is_primed()
    {
        var apiClient = new Mock<IApiClient>(MockBehavior.Strict);
        var state = new PlatformState(apiClient.Object);
        state.Set(ServerPlatform.Cloud);

        var pending = state.GetAsync();

        pending.IsCompletedSuccessfully.Should().BeTrue();
        (await pending).Should().Be(ServerPlatform.Cloud);
        apiClient.Verify(c => c.GetSetupStatusAsync(), Times.Never);
    }

    [Fact]
    public async Task NotFoundIfMultiTenantAsync_signals_not_found_on_a_multi_tenant_server()
    {
        var state = NewState(ServerPlatform.Cloud);
        var navigation = new RecordingNavigationManager();

        var handled = await state.NotFoundIfMultiTenantAsync(navigation);

        handled.Should().BeTrue();
        navigation.NotFoundRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(ServerPlatform.SelfHosted)]
    [InlineData(ServerPlatform.HomeAssistant)]
    public async Task NotFoundIfMultiTenantAsync_lets_single_tenant_platforms_through(
        ServerPlatform platform)
    {
        var state = NewState(platform);
        var navigation = new RecordingNavigationManager();

        var handled = await state.NotFoundIfMultiTenantAsync(navigation);

        handled.Should().BeFalse();
        navigation.NotFoundRaised.Should().BeFalse();
    }

    private static PlatformState NewState(ServerPlatform platform)
    {
        var state = new PlatformState(Mock.Of<IApiClient>());
        state.Set(platform);
        return state;
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/settings/plugins");
            OnNotFound += (_, _) => NotFoundRaised = true;
        }

        public bool NotFoundRaised { get; private set; }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
        }
    }
}
