using Famick.HomeManagement.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Issue #106: every sign-in path issues its first refresh token through
/// <see cref="RefreshToken.StartFamily"/>, so it must never produce the empty family
/// or the default auth time that the non-password paths used to leave behind.
/// </summary>
public class RefreshTokenStartFamilyTests
{
    private static RefreshToken Start(DateTime authTime) => RefreshToken.StartFamily(
        Guid.NewGuid(), Guid.NewGuid(), "hash",
        expiresAt: DateTime.UtcNow.AddDays(7),
        authTime,
        deviceInfo: null, ipAddress: null, rememberMe: true);

    [Fact]
    public void Each_call_starts_its_own_non_empty_family()
    {
        var first = Start(DateTime.UtcNow);
        var second = Start(DateTime.UtcNow);

        first.FamilyId.Should().NotBe(Guid.Empty);
        second.FamilyId.Should().NotBe(Guid.Empty);
        first.FamilyId.Should().NotBe(second.FamilyId);
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Records_the_given_auth_time_and_session_details()
    {
        var authTime = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        var token = Start(authTime);

        token.AuthTime.Should().Be(authTime);
        token.RememberMe.Should().BeTrue();
        token.IsRevoked.Should().BeFalse();
        token.DeviceInfo.Should().BeEmpty();
        token.IpAddress.Should().BeEmpty();
    }
}
