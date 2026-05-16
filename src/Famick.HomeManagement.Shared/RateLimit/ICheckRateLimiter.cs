namespace Famick.HomeManagement.Shared.RateLimit;

/// <summary>
/// Increment-and-check primitive for the <c>/check</c> endpoint's dual
/// rate-limit (per-IP minute, per-email hour) and per-IP hour counter that
/// drives the CAPTCHA escalation. Backed by <c>IDistributedCache</c> so cloud
/// gets Redis and self-hosted/test gets in-memory transparently.
///
/// <para>This is a fixed-window counter (cheaper, simpler than sliding) —
/// good enough for enumeration defense. Sliding can be added later if
/// abuse patterns warrant.</para>
/// </summary>
public interface ICheckRateLimiter
{
    /// <summary>
    /// Increments all three counters and returns the resulting decision.
    /// Always increments — caller decides whether to allow downstream work
    /// based on <see cref="CheckRateLimitDecision.Outcome"/>.
    /// </summary>
    Task<CheckRateLimitDecision> ObserveAsync(
        string ipAddress,
        string emailLower,
        CancellationToken cancellationToken = default);
}
