namespace Famick.HomeManagement.Shared.RateLimit;

/// <summary>
/// Outcome flags returned by <see cref="ICheckRateLimiter"/>. The
/// <see cref="Outcome"/> is the controller's decision point; the counters
/// are exposed for the CAPTCHA gate to inspect.
/// </summary>
public readonly record struct CheckRateLimitDecision(
    CheckRateLimitOutcome Outcome,
    long IpHourCount,
    long IpMinuteCount,
    long EmailHourCount,
    TimeSpan? RetryAfter);

public enum CheckRateLimitOutcome
{
    /// <summary>
    /// All three counters under their thresholds. Controller may proceed
    /// directly to the lookup (CAPTCHA gate may still kick in based on
    /// <c>IpHourCount</c>).
    /// </summary>
    Allowed,

    /// <summary>
    /// Per-IP minute window exceeded (>10 req/min). 429 with Retry-After.
    /// </summary>
    IpMinuteExceeded,

    /// <summary>
    /// Per-email hour window exceeded (>5 req/hr). 429 with Retry-After.
    /// Deliberately low to make enumeration infeasible.
    /// </summary>
    EmailHourExceeded,
}
