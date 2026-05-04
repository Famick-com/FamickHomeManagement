using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.FeatureFlags.Tests.Unit;

public class FeatureFlagServiceTests
{
    private static IFeatureFlagService BuildService(IDictionary<string, string?>? configValues = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues ?? new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFeatureFlags(config);
        return services.BuildServiceProvider().GetRequiredService<IFeatureFlagService>();
    }

    [Fact]
    public async Task IsEnabledAsync_returns_false_for_unconfigured_flag()
    {
        var service = BuildService();
        (await service.IsEnabledAsync(FeatureFlags.StepUpEnabled)).Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_returns_true_when_configured_true()
    {
        var service = BuildService(new Dictionary<string, string?>
        {
            ["FeatureManagement:step_up_enabled"] = "true"
        });

        (await service.IsEnabledAsync(FeatureFlags.StepUpEnabled)).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_returns_false_when_configured_false()
    {
        var service = BuildService(new Dictionary<string, string?>
        {
            ["FeatureManagement:step_up_enabled"] = "false"
        });

        (await service.IsEnabledAsync(FeatureFlags.StepUpEnabled)).Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_sync_helper_returns_same_result_as_async()
    {
        var service = BuildService(new Dictionary<string, string?>
        {
            ["FeatureManagement:proxy_tunnel_enabled"] = "true"
        });

        service.IsEnabled(FeatureFlags.ProxyTunnelEnabled).Should().BeTrue();
        service.IsEnabled(FeatureFlags.StepUpEnabled).Should().BeFalse();
    }

    [Fact]
    public async Task Each_registered_flag_can_be_independently_toggled()
    {
        var values = new Dictionary<string, string?>();
        foreach (var flag in FeatureFlags.All)
        {
            values[$"FeatureManagement:{flag}"] = "true";
        }

        var service = BuildService(values);

        foreach (var flag in FeatureFlags.All)
        {
            (await service.IsEnabledAsync(flag)).Should().BeTrue($"flag {flag} should be true");
        }
    }

    [Fact]
    public void GetAll_returns_the_canonical_flag_list()
    {
        var service = BuildService();
        service.GetAll().Should().BeEquivalentTo(FeatureFlags.All);
    }
}
