namespace Famick.HomeManagement.Web.Shared.Authorization;

/// <summary>
/// Marks a controller action (or whole controller) as requiring recent authentication.
/// Phase 2 — the <see cref="StepUpFilter"/> reads this and rejects with
/// <c>403 STEP_UP_REQUIRED</c> when <c>now - auth_time</c> exceeds the threshold.
///
/// Default threshold is <c>JwtSettings:StepUpFreshnessSeconds</c> (300s). Set
/// <see cref="FreshnessSeconds"/> to override per-endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class StepUpAttribute : Attribute
{
    /// <summary>
    /// Per-endpoint override of the freshness window in seconds. <c>0</c> (default)
    /// means "use the configured <c>JwtSettings:StepUpFreshnessSeconds</c> value".
    /// </summary>
    public int FreshnessSeconds { get; init; }
}
