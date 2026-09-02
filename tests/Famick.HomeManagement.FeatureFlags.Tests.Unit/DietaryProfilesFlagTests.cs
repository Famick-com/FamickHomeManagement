using Famick.HomeManagement.FeatureFlags;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Famick.HomeManagement.FeatureFlags.Tests.Unit;

/// <summary>
/// The dietary-profiles flag, which is off because of what it gates rather than because
/// the feature is unfinished.
/// </summary>
/// <remarks>
/// An allergy recorded against a named household member is health information. HIPAA does
/// not reach a household app — it binds health plans, clearinghouses and providers — but
/// state laws written for non-covered entities do, and Washington's carries a private
/// right of action. Collection is paused until that is settled, so the value of this flag
/// is a legal position rather than a product one, and it defaulting to on would be a
/// silent reversal of it.
/// </remarks>
public class DietaryProfilesFlagTests
{
    [Fact]
    public void IsOffWhenNothingConfiguresIt()
    {
        var service = BuildService(new Dictionary<string, string?>());

        service.IsEnabled(FeatureFlags.DietaryProfilesEnabled).Should().BeFalse(
            "collection stays off unless a deployment deliberately turns it on");
    }

    [Fact]
    public void IsRegisteredSoItCanBeFoundAndSwitched()
    {
        // A flag missing from All is invisible to the diagnostics endpoint, which is how
        // someone would check whether collection is actually off in a given deployment.
        FeatureFlags.All.Should().Contain(FeatureFlags.DietaryProfilesEnabled);
    }

    [Fact]
    public void CanStillBeTurnedOn()
    {
        // The pause has to be reversible: this is waiting on an answer, not a removal.
        var service = BuildService(new Dictionary<string, string?>
        {
            ["FeatureManagement:dietary_profiles_enabled"] = "true"
        });

        service.IsEnabled(FeatureFlags.DietaryProfilesEnabled).Should().BeTrue();
    }

    private static IFeatureFlagService BuildService(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddFeatureFlags(configuration)
            .BuildServiceProvider()
            .GetRequiredService<IFeatureFlagService>();
    }
}
