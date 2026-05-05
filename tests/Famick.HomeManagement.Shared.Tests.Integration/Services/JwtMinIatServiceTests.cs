using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Phase 1 — covers the Postgres <c>JwtMinIatService</c> against a real Postgres
/// container. The service backs the destination-side JWT revocation check, so
/// correctness here is security-load-bearing.
/// </summary>
public class JwtMinIatServiceTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public JwtMinIatServiceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<Guid> CreateUserAsync()
    {
        // Seed a minimal user row so the FK from UserJwtMinIats resolves. We bypass
        // the tenant filter so this test class is self-contained — no need to set
        // up a tenant context.
        await using var db = _fixture.CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"test-{Guid.NewGuid():N}@example.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "x",
            TenantId = tenantId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task GetMinIatAsync_returns_zero_for_unknown_user()
    {
        await using var db = _fixture.CreateDbContext();
        var service = new JwtMinIatService(db, NullLogger<JwtMinIatService>.Instance);

        var result = await service.GetMinIatAsync(Guid.NewGuid());

        result.Should().Be(0L,
            "with no row, jwt_min_iat defaults to 0 — no token is rejected on this check");
    }

    [Fact]
    public async Task GetMinIatAsync_returns_stored_value_after_bump()
    {
        var userId = await CreateUserAsync();
        await using var db = _fixture.CreateDbContext();
        var service = new JwtMinIatService(db, NullLogger<JwtMinIatService>.Instance);

        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await service.BumpAsync(userId, nowSeconds);

        await using var freshDb = _fixture.CreateDbContext();
        var freshService = new JwtMinIatService(freshDb, NullLogger<JwtMinIatService>.Instance);
        var result = await freshService.GetMinIatAsync(userId);

        result.Should().Be(nowSeconds);
    }

    [Fact]
    public async Task BumpAsync_is_monotonic_smaller_value_is_no_op()
    {
        var userId = await CreateUserAsync();
        await using var db = _fixture.CreateDbContext();
        var service = new JwtMinIatService(db, NullLogger<JwtMinIatService>.Instance);

        var laterTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var earlierTime = laterTime - 100;

        await service.BumpAsync(userId, laterTime);
        await service.BumpAsync(userId, earlierTime);

        await using var freshDb = _fixture.CreateDbContext();
        var freshService = new JwtMinIatService(freshDb, NullLogger<JwtMinIatService>.Instance);
        var result = await freshService.GetMinIatAsync(userId);

        result.Should().Be(laterTime,
            "BumpAsync must never move backwards — earlier values are silently ignored");
    }

    [Fact]
    public async Task BumpAsync_advances_value_on_each_subsequent_call()
    {
        var userId = await CreateUserAsync();
        await using var db = _fixture.CreateDbContext();
        var service = new JwtMinIatService(db, NullLogger<JwtMinIatService>.Instance);

        var t1 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await service.BumpAsync(userId, t1);

        var t2 = t1 + 60;
        await service.BumpAsync(userId, t2);

        await using var freshDb = _fixture.CreateDbContext();
        var freshService = new JwtMinIatService(freshDb, NullLogger<JwtMinIatService>.Instance);
        var result = await freshService.GetMinIatAsync(userId);

        result.Should().Be(t2);
    }

    [Fact]
    public async Task BumpAsync_for_unknown_user_is_no_op_not_error()
    {
        // The service should log a warning and return cleanly rather than throw —
        // a stray bump for a deleted user shouldn't poison an unrelated request path.
        await using var db = _fixture.CreateDbContext();
        var service = new JwtMinIatService(db, NullLogger<JwtMinIatService>.Instance);

        var act = async () => await service.BumpAsync(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BumpAsync_creates_one_row_per_user_with_unique_index()
    {
        var userId = await CreateUserAsync();
        var t1 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var t2 = t1 + 100;

        await using (var db = _fixture.CreateDbContext())
        {
            var service = new JwtMinIatService(db, NullLogger<JwtMinIatService>.Instance);
            await service.BumpAsync(userId, t1);
        }
        await using (var db = _fixture.CreateDbContext())
        {
            var service = new JwtMinIatService(db, NullLogger<JwtMinIatService>.Instance);
            await service.BumpAsync(userId, t2);
        }

        await using var verifyDb = _fixture.CreateDbContext();
        var rowCount = verifyDb.UserJwtMinIats
            .IgnoreQueryFilters()
            .Count(x => x.UserId == userId);

        rowCount.Should().Be(1,
            "the unique index on UserId enforces one row per user — bump must update, not insert");
    }
}
