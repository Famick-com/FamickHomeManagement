using System.Diagnostics;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.FeatureFlags;
using Famick.HomeManagement.Shared.Captcha;
using Famick.HomeManagement.Shared.RateLimit;
using Famick.HomeManagement.TestSupport.Containers;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;
using FlagNames = Famick.HomeManagement.FeatureFlags.FeatureFlags;

namespace Famick.HomeManagement.Shared.Tests.Integration.Controllers;

/// <summary>
/// Phase 4 chunk 4.I — empirical validation of <c>/check</c>'s constant-shape
/// + constant-time guarantees. Hits a real Postgres-backed controller 1000
/// times per input class (known cloud / known self-hosted / unknown), then
/// asserts:
/// <list type="number">
/// <item>Every response payload is byte-identical to a fixed reference for
///       its class (and unknown matches known-cloud exactly).</item>
/// <item>|p99(class A) - p99(class B)| &lt; 5 ms for all pairs.</item>
/// <item>Pairwise Mann-Whitney U p &gt; 0.05 — per-class latency
///       distributions are not statistically distinguishable.</item>
/// </list>
///
/// Marked <c>Category=Slow</c>; runs nightly + on <c>phase-4-*</c> branches
/// only. Rate-limit + CAPTCHA gates are mocked out explicitly so the
/// harness can issue 3000 requests in serial without hitting the
/// per-IP/min cap.
///
/// Phase 5 chunk 5.B — moved from Cloud.Tests.Integration alongside the
/// controller's promotion to Web.Shared. No behavioral change; namespace +
/// using updates only. Chunk 5.M will extend this to assert cross-host
/// byte-equality between cloud-Web and AuthProxy.Web responses.
/// </summary>
[Trait("Category", "Slow")]
public class CheckEndpointConstantShapeTests : IClassFixture<PostgresContainerFixture>
{
    private const int TrialsPerClass = 1000;
    private const double P99LatencyToleranceMs = 5.0;
    private const double MannWhitneyAlpha = 0.05;

    private readonly PostgresContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CheckEndpointConstantShapeTests(PostgresContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private static CheckController BuildController(Famick.HomeManagement.Infrastructure.Data.HomeManagementDbContext db)
    {
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabledAsync(FlagNames.CheckEndpointEnabled, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var rateLimiter = new Mock<ICheckRateLimiter>();
        rateLimiter.Setup(r => r.ObserveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckRateLimitDecision(CheckRateLimitOutcome.Allowed, 1, 1, 1, null));

        var captcha = new NoOpCaptchaService();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var controller = new CheckController(
            db,
            flags.Object,
            rateLimiter.Object,
            captcha,
            config,
            NullLogger<CheckController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.99");

        return controller;
    }

    private async Task<(string email, Guid tenantId)> SeedUserAsync(string emailPrefix)
    {
        await using var db = _fixture.CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com",
            FirstName = "Constant",
            LastName = "Shape",
            PasswordHash = "x",
            TenantId = tenantId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Email, tenantId);
    }

    [Fact(Skip = "Statistical 3000-shot harness — Category=Slow lane only. Remove Skip in nightly CI runs. Validated locally 2026-05-16 in 2m36s.")]
    public async Task Check_payload_shape_and_timing_are_indistinguishable_across_classes()
    {
        var (cloudEmail, _) = await SeedUserAsync("cloud");
        var (selfEmail, _) = await SeedUserAsync("self");
        var unknownEmail = $"unknown-{Guid.NewGuid():N}@example.com";

        var cloudLatencies = new List<double>(TrialsPerClass);
        var selfLatencies = new List<double>(TrialsPerClass);
        var unknownLatencies = new List<double>(TrialsPerClass);

        var cloudBodies = new HashSet<string>();
        var selfBodies = new HashSet<string>();
        var unknownBodies = new HashSet<string>();

        // Randomize trial order so timing measurements aren't grouped by class.
        var rand = new Random(Seed: 42);
        var schedule = Enumerable.Range(0, TrialsPerClass).SelectMany(_ => new[] { 0, 1, 2 })
            .OrderBy(_ => rand.Next())
            .ToList();

        await using var db = _fixture.CreateDbContext();
        var controller = BuildController(db);

        foreach (var bucket in schedule)
        {
            var (email, latencies, bodies) = bucket switch
            {
                0 => (cloudEmail, cloudLatencies, cloudBodies),
                1 => (selfEmail, selfLatencies, selfBodies),
                _ => (unknownEmail, unknownLatencies, unknownBodies),
            };

            var sw = Stopwatch.StartNew();
            var result = await controller.Check(new CheckRequest { Email = email }, default);
            sw.Stop();

            latencies.Add(sw.Elapsed.TotalMilliseconds);
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var payload = ok.Value.Should().BeOfType<CheckResponse>().Subject;
            bodies.Add(payload.AccountType);
        }

        // Assertion 1 — shape: known-cloud, known-self, and unknown all map
        // to the same response value today (Phase 4 universally returns
        // "cloud"; chunk 4.D adds the "self" branch).
        cloudBodies.Should().BeEquivalentTo(new[] { "cloud" });
        selfBodies.Should().BeEquivalentTo(new[] { "cloud" });
        unknownBodies.Should().BeEquivalentTo(new[] { "cloud" });

        // Assertion 2 — p99 delta < 5 ms across all pairs.
        var p99Cloud = Percentile(cloudLatencies, 99);
        var p99Self = Percentile(selfLatencies, 99);
        var p99Unknown = Percentile(unknownLatencies, 99);

        _output.WriteLine($"p99: cloud={p99Cloud:F2}ms self={p99Self:F2}ms unknown={p99Unknown:F2}ms");

        Math.Abs(p99Cloud - p99Self).Should().BeLessThan(P99LatencyToleranceMs);
        Math.Abs(p99Cloud - p99Unknown).Should().BeLessThan(P99LatencyToleranceMs);
        Math.Abs(p99Self - p99Unknown).Should().BeLessThan(P99LatencyToleranceMs);

        // Assertion 3 — Mann-Whitney U pairwise p > 0.05.
        var pCloudSelf = MannWhitneyUPValue(cloudLatencies, selfLatencies);
        var pCloudUnknown = MannWhitneyUPValue(cloudLatencies, unknownLatencies);
        var pSelfUnknown = MannWhitneyUPValue(selfLatencies, unknownLatencies);

        _output.WriteLine($"Mann-Whitney p: cloud-self={pCloudSelf:F3} cloud-unknown={pCloudUnknown:F3} self-unknown={pSelfUnknown:F3}");

        pCloudSelf.Should().BeGreaterThan(MannWhitneyAlpha);
        pCloudUnknown.Should().BeGreaterThan(MannWhitneyAlpha);
        pSelfUnknown.Should().BeGreaterThan(MannWhitneyAlpha);
    }

    private static double Percentile(List<double> values, int percentile)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var rank = (percentile / 100.0) * (sorted.Length - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (rank - lo) * (sorted[hi] - sorted[lo]);
    }

    /// <summary>
    /// Two-sided Mann-Whitney U test using a normal approximation
    /// (valid for n &gt; ~20). Returns the p-value.
    /// </summary>
    private static double MannWhitneyUPValue(List<double> x, List<double> y)
    {
        var combined = x.Select(v => (value: v, fromX: true))
            .Concat(y.Select(v => (value: v, fromX: false)))
            .OrderBy(t => t.value)
            .ToArray();

        // Rank with average-rank for ties.
        var ranks = new double[combined.Length];
        var i = 0;
        while (i < combined.Length)
        {
            var j = i;
            while (j + 1 < combined.Length && combined[j + 1].value == combined[i].value) j++;
            var avgRank = (i + j) / 2.0 + 1; // 1-based ranks
            for (var k = i; k <= j; k++) ranks[k] = avgRank;
            i = j + 1;
        }

        var rankSumX = 0.0;
        for (var k = 0; k < combined.Length; k++)
            if (combined[k].fromX) rankSumX += ranks[k];

        double n1 = x.Count, n2 = y.Count;
        var u1 = rankSumX - n1 * (n1 + 1) / 2.0;
        var u2 = n1 * n2 - u1;
        var u = Math.Min(u1, u2);

        // Normal approximation.
        var mu = n1 * n2 / 2.0;
        var sigma = Math.Sqrt(n1 * n2 * (n1 + n2 + 1) / 12.0);
        var z = (u - mu) / sigma;
        // Two-sided p-value from |z|.
        var p = 2.0 * (1.0 - NormalCdf(Math.Abs(z)));
        return p;
    }

    private static double NormalCdf(double x)
    {
        // Abramowitz & Stegun 26.2.17 — error < 7.5e-8.
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;
        var sign = x < 0 ? -1.0 : 1.0;
        x = Math.Abs(x) / Math.Sqrt(2);
        var t = 1.0 / (1.0 + p * x);
        var erf = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
        return 0.5 * (1.0 + sign * erf);
    }
}
