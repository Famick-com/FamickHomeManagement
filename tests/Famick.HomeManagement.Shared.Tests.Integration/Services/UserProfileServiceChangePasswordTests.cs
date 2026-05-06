using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Phase 1 — verifies <see cref="UserProfileService.ChangePasswordAsync"/> bumps
/// <c>jwt_min_iat</c> after a successful password change. Without this, "I changed
/// my password" would not actually invalidate already-issued access tokens.
/// </summary>
public class UserProfileServiceChangePasswordTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public UserProfileServiceChangePasswordTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record TestHarness(
        UserProfileService Service,
        IJwtMinIatService MinIatService,
        Guid UserId,
        string OldPassword,
        string NewPassword);

    private async Task<TestHarness> SeedAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var passwordHasher = new PasswordHasher(configuration);

        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var oldPassword = "Old-Password-1!";
        var newPassword = "New-Password-2!";

        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Users.Add(new User
            {
                Id = userId,
                Email = $"changepw-{userId:N}@example.com",
                FirstName = "Test",
                LastName = "User",
                PasswordHash = passwordHasher.HashPassword(oldPassword),
                TenantId = tenantId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var db = _fixture.CreateDbContext();
        var minIatService = new JwtMinIatService(db, NullLogger<JwtMinIatService>.Instance);
        var lockService = new PostgresUserAdvisoryLockService(
            db, NullLogger<PostgresUserAdvisoryLockService>.Instance);
        var fileUrlService = new Mock<IFileUrlService>().Object;

        var service = new UserProfileService(
            db,
            passwordHasher,
            fileUrlService,
            minIatService,
            lockService,
            NullLogger<UserProfileService>.Instance);

        return new TestHarness(service, minIatService, userId, oldPassword, newPassword);
    }

    [Fact]
    public async Task ChangePasswordAsync_bumps_jwt_min_iat_to_at_least_now()
    {
        var harness = await SeedAsync();

        var beforeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await harness.Service.ChangePasswordAsync(
            harness.UserId,
            new ChangePasswordRequest
            {
                CurrentPassword = harness.OldPassword,
                NewPassword = harness.NewPassword,
                ConfirmPassword = harness.NewPassword
            });

        var minIat = await harness.MinIatService.GetMinIatAsync(harness.UserId);

        minIat.Should().BeGreaterThanOrEqualTo(beforeSeconds,
            "ChangePasswordAsync must bump jwt_min_iat to at least 'now' so already-issued tokens are rejected");
    }

    [Fact]
    public async Task ChangePasswordAsync_revokes_existing_refresh_tokens()
    {
        var harness = await SeedAsync();

        // Seed an existing refresh token directly so we can verify it's revoked
        // by ChangePasswordAsync (the existing behavior, preserved alongside the
        // new jwt_min_iat bump).
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = harness.UserId,
                TenantId = (await seed.Users.FindAsync(harness.UserId))!.TenantId,
                TokenHash = "hash-" + Guid.NewGuid(),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                FamilyId = Guid.NewGuid(),
                AuthTime = DateTime.UtcNow,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await harness.Service.ChangePasswordAsync(
            harness.UserId,
            new ChangePasswordRequest
            {
                CurrentPassword = harness.OldPassword,
                NewPassword = harness.NewPassword,
                ConfirmPassword = harness.NewPassword
            });

        await using var verify = _fixture.CreateDbContext();
        var unrevoked = verify.RefreshTokens
            .IgnoreQueryFilters()
            .Count(rt => rt.UserId == harness.UserId && !rt.IsRevoked);
        unrevoked.Should().Be(0, "all refresh tokens must be revoked after password change");
    }

    [Fact]
    public async Task ChangePasswordAsync_with_wrong_current_password_does_not_bump()
    {
        var harness = await SeedAsync();

        var beforeMinIat = await harness.MinIatService.GetMinIatAsync(harness.UserId);

        var act = async () => await harness.Service.ChangePasswordAsync(
            harness.UserId,
            new ChangePasswordRequest
            {
                CurrentPassword = "wrong-password",
                NewPassword = harness.NewPassword,
                ConfirmPassword = harness.NewPassword
            });

        await act.Should().ThrowAsync<Exception>(
            "wrong current password must reject before any state change");

        var afterMinIat = await harness.MinIatService.GetMinIatAsync(harness.UserId);
        afterMinIat.Should().Be(beforeMinIat,
            "a failed password change must not bump jwt_min_iat — bumping happens only after the password actually changes");
    }
}
