using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class AddressHasherTests
{
    [Fact]
    public async Task ComputeAsync_ReturnsNull_WhenAllComponentsEmpty()
    {
        var hasher = new AddressHasher(new PassThroughAddressCanonicalizer());

        var hash = await hasher.ComputeAsync(
            new AddressComponentsInput(null, null, null, null, null),
            AddressProvenance.Unverified);

        hash.Should().BeNull();
    }

    [Fact]
    public async Task ComputeAsync_FallsBackToLegacyHash_WithPassThroughCanonicalizer()
    {
        // Regression guard: when libpostal is disabled, the new hasher must
        // produce byte-for-byte the same hash as the legacy
        // ComputeNormalizedHash for the same inputs.
        var hasher = new AddressHasher(new PassThroughAddressCanonicalizer());
        var input = new AddressComponentsInput("123 Main St", "Springfield", "IL", "62701", "USA");

        var hash = await hasher.ComputeAsync(input, AddressProvenance.Unverified);

        hash.Should().Be(LegacyHash("123 Main St", "Springfield", "IL", "62701", "USA"));
    }

    [Fact]
    public async Task ComputeAsync_VerifiedProvenance_SkipsCanonicalizer()
    {
        // Verified inputs must not invoke the canonicalizer at all — even
        // a libpostal-flavored fake should be untouched. Hash should match
        // what the legacy algorithm produced for the same raw inputs.
        var fake = new TrackingCanonicalizer();
        var hasher = new AddressHasher(fake);
        var input = new AddressComponentsInput("123 Main St", "Springfield", "IL", "62701", "USA");

        var hash = await hasher.ComputeAsync(input, AddressProvenance.Verified);

        fake.CallCount.Should().Be(0);
        hash.Should().Be(LegacyHash("123 Main St", "Springfield", "IL", "62701", "USA"));
    }

    [Fact]
    public async Task ComputeAsync_ProducesIdenticalHash_WhenCanonicalizerCollapsesVariations()
    {
        // Unverified inputs go through the canonicalizer. A canonicalizer
        // that collapses "St" → "Street" must produce the same hash for
        // both spellings.
        var fake = new ScriptedCanonicalizer(
            ("123 Main St|Springfield|IL|62701|USA",
                new CanonicalAddressComponents("123 main street", "springfield", "il", "62701", "usa")),
            ("123 Main Street|Springfield|IL|62701|USA",
                new CanonicalAddressComponents("123 main street", "springfield", "il", "62701", "usa")));
        var hasher = new AddressHasher(fake);

        var hash1 = await hasher.ComputeAsync(
            new AddressComponentsInput("123 Main St", "Springfield", "IL", "62701", "USA"),
            AddressProvenance.Unverified);
        var hash2 = await hasher.ComputeAsync(
            new AddressComponentsInput("123 Main Street", "Springfield", "IL", "62701", "USA"),
            AddressProvenance.Unverified);

        hash1.Should().NotBeNull();
        hash1.Should().Be(hash2);
    }

    [Fact]
    public async Task ComputeAsync_ExcludesLine2_FromHash()
    {
        // Building-as-row contract: Line 2 is per-contact, never feeds the
        // hash. Two addresses that differ only in apt/suite should hash
        // identically — they're the same building.
        var hasher = new AddressHasher(new PassThroughAddressCanonicalizer());
        var input = new AddressComponentsInput("123 Main St", "Springfield", "IL", "62701", "USA");

        var hash = await hasher.ComputeAsync(input, AddressProvenance.Verified);

        hash.Should().Be(LegacyHash("123 Main St", "Springfield", "IL", "62701", "USA"));
    }

    /// <summary>
    /// Replica of the previous static <c>ComputeNormalizedHash</c> /
    /// <c>GenerateAddressHash</c> algorithm. Used as a regression oracle
    /// to prove the new hasher produces identical output when the
    /// canonicalizer is the pass-through default.
    /// </summary>
    private static string? LegacyHash(string? l1, string? city, string? state, string? postal, string? country)
    {
        var parts = new[]
        {
            l1?.Trim().ToLowerInvariant(),
            city?.Trim().ToLowerInvariant(),
            state?.Trim().ToLowerInvariant(),
            postal?.Trim().ToLowerInvariant(),
            country?.Trim().ToLowerInvariant()
        };
        var combined = string.Join("|", parts.Where(p => !string.IsNullOrEmpty(p)));
        if (string.IsNullOrEmpty(combined)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class TrackingCanonicalizer : IAddressCanonicalizer
    {
        public string ProviderName => "Tracking";
        public int CallCount { get; private set; }

        public Task<CanonicalAddressComponents> CanonicalizeAsync(
            AddressComponentsInput input, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new CanonicalAddressComponents(
                input.Line1?.Trim(), input.City?.Trim(), input.State?.Trim(),
                input.PostalCode?.Trim(), input.Country?.Trim()));
        }
    }

    private sealed class ScriptedCanonicalizer : IAddressCanonicalizer
    {
        private readonly Dictionary<string, CanonicalAddressComponents> _script;

        public ScriptedCanonicalizer(params (string key, CanonicalAddressComponents output)[] entries)
        {
            _script = entries.ToDictionary(e => e.key, e => e.output);
        }

        public string ProviderName => "Scripted";

        public Task<CanonicalAddressComponents> CanonicalizeAsync(
            AddressComponentsInput input, CancellationToken ct = default)
        {
            var key = $"{input.Line1}|{input.City}|{input.State}|{input.PostalCode}|{input.Country}";
            if (_script.TryGetValue(key, out var output))
                return Task.FromResult(output);
            // Fallback to passthrough so unscripted inputs don't blow up.
            return Task.FromResult(new CanonicalAddressComponents(
                input.Line1?.Trim(), input.City?.Trim(), input.State?.Trim(),
                input.PostalCode?.Trim(), input.Country?.Trim()));
        }
    }
}
