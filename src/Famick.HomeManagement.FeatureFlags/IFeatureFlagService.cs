namespace Famick.HomeManagement.FeatureFlags;

/// <summary>
/// Thin wrapper around <see cref="Microsoft.FeatureManagement.IFeatureManager"/>
/// that adds a sync helper for hot paths (middleware, request filters) where
/// awaiting an async flag check is awkward.
///
/// Default implementation falls back to <c>false</c> for any unknown flag — this
/// matches the security posture in the master plan: flags ship deployable-inactive,
/// then phases enable them.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Async check (preferred). Returns <c>false</c> if the flag is not configured.
    /// </summary>
    Task<bool> IsEnabledAsync(string flagName, CancellationToken ct = default);

    /// <summary>
    /// Sync check for hot paths. Blocks the calling thread on the underlying async call.
    /// Use <see cref="IsEnabledAsync"/> in async contexts.
    /// </summary>
    bool IsEnabled(string flagName);

    /// <summary>
    /// Returns every registered flag name. Used by diagnostics endpoints and tests
    /// to verify the registered set matches <see cref="FeatureFlags.All"/>.
    /// </summary>
    IReadOnlyList<string> GetAll();
}
