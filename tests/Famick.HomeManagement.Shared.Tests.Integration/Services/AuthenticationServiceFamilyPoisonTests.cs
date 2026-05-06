using System.IdentityModel.Tokens.Jwt;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Phase 1 — end-to-end integration tests for the security-load-bearing pieces of
/// <see cref="AuthenticationService"/>: refresh-token family poisoning on reuse,
/// <c>auth_time</c> preservation across rotation, and <c>jwt_min_iat</c> bumps on
/// reuse-detection and logout-all.
///
/// Each test wires the real <see cref="AuthenticationService"/> against a real
/// Postgres container (no mocks for the data path) so EF query filters, advisory
/// locks, and the bulk-revoke <c>ExecuteUpdateAsync</c> all run their production
/// SQL paths.
/// </summary>
public class AuthenticationServiceFamilyPoisonTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public AuthenticationServiceFamilyPoisonTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record TestHarness(
        Func<AuthenticationService> ServiceFactory,
        Func<IJwtMinIatService> MinIatFactory,
        Guid UserId,
        string Email,
        string PlaintextPassword);

    private async Task<TestHarness> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"poison-{userId:N}@example.com";
        var password = "Test-Password-1!";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Issuer"] = "https://test.famick.com",
                ["JwtSettings:Audience"] = "https://test.famick.com",
                ["JwtSettings:ExpirationMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7",
                ["JwtSettings:RefreshTokenExtendedExpirationDays"] = "30"
            })
            .Build();
        var passwordHasher = new PasswordHasher(configuration);

        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Users.Add(new User
            {
                Id = userId,
                Email = email,
                FirstName = "Test",
                LastName = "User",
                PasswordHash = passwordHasher.HashPassword(password),
                TenantId = tenantId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        // Each call to ServiceFactory() returns a fresh service graph with its own
        // DbContext. In production each HTTP request gets a scoped DbContext, so the
        // EF first-level cache is empty per request. Reusing one DbContext across
        // calls in tests would serve stale tracked entities (e.g. a refresh token
        // whose IsRevoked was updated via ExecuteUpdateAsync in a prior call).
        var signingKeyService = new JwtSigningKeyService(
            configuration, NullLogger<JwtSigningKeyService>.Instance);
        var tokenService = new TokenService(configuration, signingKeyService);
        var contactService = new Mock<IContactService>().Object;
        var multiTenancyOptions = new MultiTenancyOptions { IsMultiTenantEnabled = true };

        AuthenticationService BuildService()
        {
            var db = _fixture.CreateDbContext();
            var minIatService = new JwtMinIatService(db, NullLogger<JwtMinIatService>.Instance);
            var lockService = new PostgresUserAdvisoryLockService(
                db, NullLogger<PostgresUserAdvisoryLockService>.Instance);
            return new AuthenticationService(
                db, passwordHasher, tokenService, configuration, contactService,
                minIatService, lockService,
                NullLogger<AuthenticationService>.Instance, multiTenancyOptions);
        }

        IJwtMinIatService BuildMinIat() =>
            new JwtMinIatService(_fixture.CreateDbContext(), NullLogger<JwtMinIatService>.Instance);

        return new TestHarness(BuildService, BuildMinIat, userId, email, password);
    }

    private static long GetClaim(string token, string claimType) =>
        long.Parse(new JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims.Single(c => c.Type == claimType).Value);

    [Fact]
    public async Task Login_then_refresh_rotates_and_preserves_auth_time()
    {
        var harness = await SeedAsync();

        var loginResult = await harness.ServiceFactory().LoginAsync(
            new LoginRequest { Email = harness.Email, Password = harness.PlaintextPassword },
            "127.0.0.1", "test", default);

        var loginAuthTime = GetClaim(loginResult.AccessToken, "auth_time");

        // Wait a moment so the refresh path's "now" diverges from the login's "now".
        await Task.Delay(1100);

        var refreshResult = await harness.ServiceFactory().RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = loginResult.RefreshToken },
            "127.0.0.1", "test", default);

        var refreshAuthTime = GetClaim(refreshResult.AccessToken, "auth_time");
        var refreshIat = GetClaim(refreshResult.AccessToken, "iat");

        refreshAuthTime.Should().Be(loginAuthTime,
            "auth_time must reflect the original first-factor authentication, not the rotation time");
        refreshIat.Should().BeGreaterThan(loginAuthTime,
            "iat is the new token's issuance time and should be later than the original auth_time");
    }

    [Fact]
    public async Task Refresh_token_reuse_poisons_entire_family()
    {
        var harness = await SeedAsync();

        var login = await harness.ServiceFactory().LoginAsync(
            new LoginRequest { Email = harness.Email, Password = harness.PlaintextPassword },
            "127.0.0.1", "test", default);

        // First rotation succeeds: A -> B. A is now revoked.
        var rotation1 = await harness.ServiceFactory().RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = login.RefreshToken },
            "127.0.0.1", "test", default);

        // Replay A. This is the canonical reuse-detection signal: a revoked
        // refresh token is being presented again. Family must be poisoned.
        var replay = async () => await harness.ServiceFactory().RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = login.RefreshToken },
            "127.0.0.1", "test", default);

        await replay.Should().ThrowAsync<InvalidCredentialsException>(
            "reuse of a rotated refresh token must be rejected");

        // Verify: every refresh token in the family is revoked, including B
        // (the legitimate descendant the attacker hadn't seen yet).
        await using var verify = _fixture.CreateDbContext();
        var familyTokens = await verify.RefreshTokens
            .IgnoreQueryFilters()
            .Where(rt => rt.UserId == harness.UserId)
            .ToListAsync();

        familyTokens.Should().AllSatisfy(rt =>
            rt.IsRevoked.Should().BeTrue(
                $"every token in the family must be poisoned; token {rt.Id} was not"));

        // Verify: jwt_min_iat was bumped, so any access tokens already issued
        // from this family also fail validation.
        var minIat = await harness.MinIatFactory().GetMinIatAsync(harness.UserId);
        minIat.Should().BeGreaterThan(0L,
            "reuse-detection must bump jwt_min_iat so issued access tokens also fail");

        // Replaying B (the legitimate-but-now-poisoned descendant) also fails.
        var replayB = async () => await harness.ServiceFactory().RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = rotation1.RefreshToken },
            "127.0.0.1", "test", default);
        await replayB.Should().ThrowAsync<InvalidCredentialsException>(
            "B was poisoned alongside A — replaying it must also fail");
    }

    [Fact]
    public async Task LogoutAll_bumps_jwt_min_iat_so_access_tokens_are_rejected()
    {
        var harness = await SeedAsync();

        var login = await harness.ServiceFactory().LoginAsync(
            new LoginRequest { Email = harness.Email, Password = harness.PlaintextPassword },
            "127.0.0.1", "test", default);

        var loginIat = GetClaim(login.AccessToken, "iat");

        // Sleep so the bump's now_seconds is at least 1 second after login's iat.
        await Task.Delay(1100);

        await harness.ServiceFactory().RevokeAllUserTokensAsync(harness.UserId, default);

        var minIat = await harness.MinIatFactory().GetMinIatAsync(harness.UserId);

        minIat.Should().BeGreaterThan(loginIat,
            "RevokeAllUserTokensAsync must bump jwt_min_iat past the existing access token's iat");
    }

    [Fact]
    public async Task AuthTime_persists_across_two_consecutive_rotations()
    {
        var harness = await SeedAsync();

        var login = await harness.ServiceFactory().LoginAsync(
            new LoginRequest { Email = harness.Email, Password = harness.PlaintextPassword },
            "127.0.0.1", "test", default);
        var loginAuthTime = GetClaim(login.AccessToken, "auth_time");

        await Task.Delay(1100);
        var rot1 = await harness.ServiceFactory().RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = login.RefreshToken },
            "127.0.0.1", "test", default);

        await Task.Delay(1100);
        var rot2 = await harness.ServiceFactory().RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = rot1.RefreshToken },
            "127.0.0.1", "test", default);

        GetClaim(rot1.AccessToken, "auth_time").Should().Be(loginAuthTime);
        GetClaim(rot2.AccessToken, "auth_time").Should().Be(loginAuthTime,
            "auth_time must be carried forward through every rotation in the family");
    }
}
