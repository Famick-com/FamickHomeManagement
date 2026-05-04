using Microsoft.FeatureManagement;

namespace Famick.HomeManagement.FeatureFlags;

internal sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureManager _featureManager;

    public FeatureFlagService(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public Task<bool> IsEnabledAsync(string flagName, CancellationToken ct = default)
        => _featureManager.IsEnabledAsync(flagName, ct);

    public bool IsEnabled(string flagName)
        => _featureManager.IsEnabledAsync(flagName).GetAwaiter().GetResult();

    public IReadOnlyList<string> GetAll() => FeatureFlags.All;
}
