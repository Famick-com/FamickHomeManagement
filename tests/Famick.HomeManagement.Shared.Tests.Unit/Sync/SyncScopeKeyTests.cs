using Famick.HomeManagement.Shared.Sync;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Sync;

/// <summary>
/// The scope key decides which on-device mapping file a sync run reads. Two accounts
/// sharing a key means one account's sync treats the other's mappings as its own and
/// deletes the device records behind them, so the properties asserted here are the ones
/// that keep that from happening.
/// </summary>
public class SyncScopeKeyTests
{
    private const string TenantA = "8f0c9a3e-1d24-4c8b-9f11-2b6e5a7c0d31";
    private const string TenantB = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d";
    private const string UserA = "c4e2f018-7b95-4a63-8d0e-5f1c2a9b3e77";
    private const string UserB = "9d8c7b6a-5e4f-4d3c-2b1a-0f9e8d7c6b5a";

    [Fact]
    public void SameAccountProducesTheSameKey()
    {
        SyncScopeKey.Compute(TenantA, UserA)
            .Should().Be(SyncScopeKey.Compute(TenantA, UserA));
    }

    [Fact]
    public void DifferentUsersInTheSameTenantGetDifferentKeys()
    {
        // Two people in one household share a tenant. They still each have their own
        // device records, so they must not share a mapping file.
        SyncScopeKey.Compute(TenantA, UserA)
            .Should().NotBe(SyncScopeKey.Compute(TenantA, UserB));
    }

    [Fact]
    public void SameUserIdInDifferentTenantsGetsDifferentKeys()
    {
        // A self-hosted server and a cloud tenant seed independently, so a user id can
        // repeat across them.
        SyncScopeKey.Compute(TenantA, UserA)
            .Should().NotBe(SyncScopeKey.Compute(TenantB, UserA));
    }

    [Fact]
    public void KeyIsIndependentOfCasingAndSurroundingWhitespace()
    {
        // The claims are read straight from a JWT; casing of a GUID's hex is not
        // guaranteed to be stable across the paths that write it.
        SyncScopeKey.Compute(TenantA.ToUpperInvariant(), $"  {UserA.ToUpperInvariant()} ")
            .Should().Be(SyncScopeKey.Compute(TenantA, UserA));
    }

    [Theory]
    [InlineData(null, UserA)]
    [InlineData(TenantA, null)]
    [InlineData("", UserA)]
    [InlineData(TenantA, "   ")]
    [InlineData(null, null)]
    public void MissingEitherClaimYieldsNoKey(string? tenantId, string? userId)
    {
        // Null must stay null rather than collapsing to a constant. A shared fallback key
        // would be the unscoped mapping file all over again.
        SyncScopeKey.Compute(tenantId, userId).Should().BeNull();
    }

    [Fact]
    public void KeyIsFilenameSafe()
    {
        var key = SyncScopeKey.Compute(TenantA, UserA);

        key.Should().NotBeNull();
        key!.Should().MatchRegex("^[0-9a-f]{16}$");
        key.Should().NotContainAny(Path.GetInvalidFileNameChars().Select(c => c.ToString()));
    }

    [Fact]
    public void KeyDoesNotLeakTheAccountIdentifiers()
    {
        var key = SyncScopeKey.Compute(TenantA, UserA)!;

        key.Should().NotContain(TenantA);
        key.Should().NotContain(UserA);
    }

    [Fact]
    public void TenantAndUserAreNotInterchangeable()
    {
        // A naive concatenation would let a tenant/user pair collide with its transpose.
        SyncScopeKey.Compute(TenantA, UserA)
            .Should().NotBe(SyncScopeKey.Compute(UserA, TenantA));
    }
}
