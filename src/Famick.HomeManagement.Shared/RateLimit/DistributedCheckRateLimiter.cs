using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;

namespace Famick.HomeManagement.Shared.RateLimit;

/// <summary>
/// IDistributedCache-backed implementation. Three keys per call:
/// <list type="bullet">
/// <item><c>check:ip:m:{ip}</c> — 60s TTL, threshold 10</item>
/// <item><c>check:ip:h:{ip}</c> — 3600s TTL, no hard limit (CAPTCHA gate
/// inspects via <see cref="CheckRateLimitDecision.IpHourCount"/>)</item>
/// <item><c>check:email:h:{sha256-of-lower-email}</c> — 3600s TTL, threshold 5</item>
/// </list>
///
/// IDistributedCache exposes no atomic INCR — we read, parse, write back.
/// Race windows under load can cause minor over-counting, which fails closed
/// (slightly stricter than the published thresholds). For Phase 4 traffic
/// volumes this is fine; if abuse spikes, swap in a Redis-native INCR
/// implementation in cloud only.
/// </summary>
public sealed class DistributedCheckRateLimiter : ICheckRateLimiter
{
    public const int IpMinuteLimit = 10;
    public const int EmailHourLimit = 5;

    private static readonly TimeSpan MinuteWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan HourWindow = TimeSpan.FromHours(1);

    private readonly IDistributedCache _cache;

    public DistributedCheckRateLimiter(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<CheckRateLimitDecision> ObserveAsync(
        string ipAddress,
        string emailLower,
        CancellationToken cancellationToken = default)
    {
        var emailHash = HashEmail(emailLower);

        var ipMinute = await IncrementAsync($"check:ip:m:{ipAddress}", MinuteWindow, cancellationToken);
        var ipHour = await IncrementAsync($"check:ip:h:{ipAddress}", HourWindow, cancellationToken);
        var emailHour = await IncrementAsync($"check:email:h:{emailHash}", HourWindow, cancellationToken);

        if (ipMinute > IpMinuteLimit)
        {
            return new CheckRateLimitDecision(
                CheckRateLimitOutcome.IpMinuteExceeded,
                ipHour,
                ipMinute,
                emailHour,
                RetryAfter: MinuteWindow);
        }

        if (emailHour > EmailHourLimit)
        {
            return new CheckRateLimitDecision(
                CheckRateLimitOutcome.EmailHourExceeded,
                ipHour,
                ipMinute,
                emailHour,
                RetryAfter: HourWindow);
        }

        return new CheckRateLimitDecision(
            CheckRateLimitOutcome.Allowed,
            ipHour,
            ipMinute,
            emailHour,
            RetryAfter: null);
    }

    private async Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken ct)
    {
        var current = await _cache.GetStringAsync(key, ct);
        var next = (long.TryParse(current, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0L) + 1;
        await _cache.SetStringAsync(
            key,
            next.ToString(CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = window },
            ct);
        return next;
    }

    private static string HashEmail(string emailLower)
    {
        var bytes = Encoding.UTF8.GetBytes(emailLower);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
