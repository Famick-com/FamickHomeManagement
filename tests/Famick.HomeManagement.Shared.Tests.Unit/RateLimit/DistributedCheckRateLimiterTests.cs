using Famick.HomeManagement.Shared.RateLimit;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.RateLimit;

/// <summary>
/// Phase 4 chunk 4.C — <see cref="DistributedCheckRateLimiter"/> uses
/// IDistributedCache, so this exercises against
/// <see cref="MemoryDistributedCache"/>. The Redis-backed path differs only
/// in storage; the counter logic is the same.
/// </summary>
public class DistributedCheckRateLimiterTests
{
    private static DistributedCheckRateLimiter BuildSut()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new DistributedCheckRateLimiter(cache);
    }

    [Fact]
    public async Task ObserveAsync_allows_first_request()
    {
        var sut = BuildSut();

        var decision = await sut.ObserveAsync("203.0.113.5", "user@example.com");

        decision.Outcome.Should().Be(CheckRateLimitOutcome.Allowed);
        decision.IpMinuteCount.Should().Be(1);
        decision.EmailHourCount.Should().Be(1);
        decision.IpHourCount.Should().Be(1);
    }

    [Fact]
    public async Task ObserveAsync_blocks_after_10_requests_per_ip_minute()
    {
        var sut = BuildSut();
        const string ip = "203.0.113.5";

        // Walk through 10 distinct emails so the per-email cap doesn't fire first.
        for (var i = 1; i <= DistributedCheckRateLimiter.IpMinuteLimit; i++)
        {
            var d = await sut.ObserveAsync(ip, $"user{i}@example.com");
            d.Outcome.Should().Be(CheckRateLimitOutcome.Allowed, $"request #{i} should still be under the IP/minute cap");
        }

        var eleventh = await sut.ObserveAsync(ip, "user11@example.com");

        eleventh.Outcome.Should().Be(CheckRateLimitOutcome.IpMinuteExceeded);
        eleventh.RetryAfter.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ObserveAsync_blocks_after_5_requests_per_email_hour()
    {
        var sut = BuildSut();
        const string email = "target@example.com";

        // Walk through 5 distinct IPs so the per-IP/minute cap doesn't fire first.
        for (var i = 1; i <= DistributedCheckRateLimiter.EmailHourLimit; i++)
        {
            var d = await sut.ObserveAsync($"198.51.100.{i}", email);
            d.Outcome.Should().Be(CheckRateLimitOutcome.Allowed, $"request #{i} should still be under the email/hour cap");
        }

        var sixth = await sut.ObserveAsync("198.51.100.99", email);

        sixth.Outcome.Should().Be(CheckRateLimitOutcome.EmailHourExceeded);
        sixth.RetryAfter.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task ObserveAsync_isolates_counters_per_ip()
    {
        var sut = BuildSut();

        for (var i = 0; i < 9; i++)
            await sut.ObserveAsync("203.0.113.5", $"a{i}@example.com");

        var freshIp = await sut.ObserveAsync("198.51.100.1", "fresh@example.com");

        freshIp.IpMinuteCount.Should().Be(1, "a different IP has its own counter");
        freshIp.Outcome.Should().Be(CheckRateLimitOutcome.Allowed);
    }

    [Fact]
    public async Task ObserveAsync_normalizes_email_via_hash()
    {
        // Same email, different IPs — counter should aggregate by email hash.
        var sut = BuildSut();
        const string email = "victim@example.com";

        var d1 = await sut.ObserveAsync("203.0.113.1", email);
        var d2 = await sut.ObserveAsync("203.0.113.2", email);
        var d3 = await sut.ObserveAsync("203.0.113.3", email);

        d1.EmailHourCount.Should().Be(1);
        d2.EmailHourCount.Should().Be(2);
        d3.EmailHourCount.Should().Be(3);
    }

    [Fact]
    public async Task ObserveAsync_increments_ip_hour_counter_even_when_under_minute_cap()
    {
        // CAPTCHA gate (controller side) inspects IpHourCount — ensure it
        // accrues independently of the minute counter.
        var sut = BuildSut();

        for (var i = 0; i < 3; i++)
            await sut.ObserveAsync("203.0.113.5", $"a{i}@example.com");

        var fourth = await sut.ObserveAsync("203.0.113.5", "a3@example.com");

        fourth.IpHourCount.Should().Be(4);
        fourth.IpMinuteCount.Should().Be(4);
    }
}
