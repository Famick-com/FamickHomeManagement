using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Phase 4 chunk 4.D — covers <see cref="LocalServerResolver"/> against real
/// Postgres so the change-detection audit-row write happens through real EF
/// semantics. Uses the same <see cref="PostgresContainerFixture"/> as the
/// audit-logger and JwtMinIat tests.
/// </summary>
public class LocalServerResolverTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public LocalServerResolverTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static IConfiguration BuildConfig(string? publicUrl)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MobileAppSetup:PublicUrl"] = publicUrl,
            })
            .Build();

    private LocalServerResolver BuildSut(HomeManagementDbContext db, string? publicUrl, bool multiTenant)
    {
        var auditLogger = new UserAuditLogger(db, NullLogger<UserAuditLogger>.Instance);
        var options = new MultiTenancyOptions { IsMultiTenantEnabled = multiTenant };
        return new LocalServerResolver(
            db,
            BuildConfig(publicUrl),
            options,
            auditLogger,
            NullLogger<LocalServerResolver>.Instance);
    }

    private async Task<User> SeedUserAsync(string? lastDelivered = null)
    {
        await using var db = _fixture.CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"ls-{Guid.NewGuid():N}@example.com",
            FirstName = "L",
            LastName = "S",
            PasswordHash = "x",
            TenantId = Guid.NewGuid(),
            IsActive = true,
            LastDeliveredLocalServer = lastDelivered,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<User> ReloadAsync(Guid userId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Users
            .IgnoreQueryFilters()
            .SingleAsync(u => u.Id == userId);
    }

    [Fact]
    public async Task ResolveAndAuditAsync_returns_null_in_cloud_mode()
    {
        var user = await SeedUserAsync();

        await using var db = _fixture.CreateDbContext();
        var sut = BuildSut(db, publicUrl: "http://something.local:8080", multiTenant: true);

        var result = await sut.ResolveAndAuditAsync(user, ipAddress: null, userAgent: null);

        result.Should().BeNull("cloud accounts have no local server");
    }

    [Fact]
    public async Task ResolveAndAuditAsync_returns_null_when_public_url_unset()
    {
        var user = await SeedUserAsync();

        await using var db = _fixture.CreateDbContext();
        var sut = BuildSut(db, publicUrl: null, multiTenant: false);

        var result = await sut.ResolveAndAuditAsync(user, ipAddress: null, userAgent: null);

        result.Should().BeNull("self-hosted without MobileAppSetup:PublicUrl emits no LocalServer");
    }

    [Fact]
    public async Task ResolveAndAuditAsync_returns_null_when_public_url_invalid()
    {
        var user = await SeedUserAsync();

        await using var db = _fixture.CreateDbContext();
        var sut = BuildSut(db, publicUrl: "not a url at all /path?bad", multiTenant: false);

        var result = await sut.ResolveAndAuditAsync(user, ipAddress: null, userAgent: null);

        result.Should().BeNull("non-canonicalizable URLs are dropped, not propagated");
    }

    [Fact]
    public async Task ResolveAndAuditAsync_first_delivery_stores_silently_with_no_audit()
    {
        var user = await SeedUserAsync(lastDelivered: null);

        await using (var db = _fixture.CreateDbContext())
        {
            // Re-load the user as a tracked entity in this context so the
            // resolver's SaveChangesAsync persists the LastDeliveredLocalServer
            // update.
            var tracked = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
            var sut = BuildSut(db, publicUrl: "http://famick.local:8080", multiTenant: false);
            var result = await sut.ResolveAndAuditAsync(tracked, ipAddress: "192.0.2.1", userAgent: "ua");
            result.Should().Be("http://famick.local:8080");
        }

        var reloaded = await ReloadAsync(user.Id);
        reloaded.LastDeliveredLocalServer.Should().Be("http://famick.local:8080");

        await using var verify = _fixture.CreateDbContext();
        var auditRows = await verify.UserAuditLogs
            .IgnoreQueryFilters()
            .Where(r => r.UserId == user.Id)
            .ToListAsync();
        auditRows.Should().BeEmpty("first-time delivery must not generate an audit row");
    }

    [Fact]
    public async Task ResolveAndAuditAsync_on_change_writes_audit_row_and_updates_user()
    {
        var user = await SeedUserAsync(lastDelivered: "http://old.local:8080");

        await using (var db = _fixture.CreateDbContext())
        {
            // Re-load tracked entity for the same row in this fresh context.
            var tracked = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
            var sut = BuildSut(db, publicUrl: "http://new.local:8080", multiTenant: false);
            var result = await sut.ResolveAndAuditAsync(tracked, ipAddress: "192.0.2.1", userAgent: "ua-test");
            result.Should().Be("http://new.local:8080");
        }

        var reloaded = await ReloadAsync(user.Id);
        reloaded.LastDeliveredLocalServer.Should().Be("http://new.local:8080");

        await using var verify = _fixture.CreateDbContext();
        var audit = await verify.UserAuditLogs
            .IgnoreQueryFilters()
            .SingleAsync(r => r.UserId == user.Id);
        audit.Action.Should().Be(UserAuditAction.LocalServerChanged);
        audit.OldValues.Should().Contain("http://old.local:8080");
        audit.NewValues.Should().Contain("http://new.local:8080");
        audit.IpAddress.Should().Be("192.0.2.1");
        audit.UserAgent.Should().Be("ua-test");
    }

    [Fact]
    public async Task ResolveAndAuditAsync_canonical_equality_skips_audit_and_db_write()
    {
        // Trailing slash, default port, casing differences should all canonicalize
        // to the same value — no audit row, no DB write.
        var user = await SeedUserAsync(lastDelivered: "http://famick.local:8080");

        await using (var db = _fixture.CreateDbContext())
        {
            var tracked = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id);
            var sut = BuildSut(db, publicUrl: "http://Famick.Local:8080/", multiTenant: false);
            var result = await sut.ResolveAndAuditAsync(tracked, ipAddress: null, userAgent: null);
            result.Should().Be("http://famick.local:8080");
        }

        await using var verify = _fixture.CreateDbContext();
        var auditRows = await verify.UserAuditLogs
            .IgnoreQueryFilters()
            .Where(r => r.UserId == user.Id)
            .ToListAsync();
        auditRows.Should().BeEmpty("no-op canonical edit must not produce an audit row");
    }
}
