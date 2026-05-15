using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Phase 4 chunk 4.A — covers <see cref="UserAuditLogger"/> against real
/// Postgres so jsonb serialization and FK behavior are exercised end-to-end.
/// </summary>
public class UserAuditLoggerTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public UserAuditLoggerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(Guid userId, Guid tenantId)> SeedUserAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"audit-{Guid.NewGuid():N}@example.com",
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
        return (user.Id, tenantId);
    }

    [Fact]
    public async Task LogAsync_writes_row_with_jsonb_values()
    {
        var (userId, tenantId) = await SeedUserAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            var sut = new UserAuditLogger(db, NullLogger<UserAuditLogger>.Instance);

            await sut.LogAsync(
                userId,
                tenantId,
                UserAuditAction.LocalServerChanged,
                oldValues: new { localServer = "http://old.local:8080" },
                newValues: new { localServer = "http://new.local:8080" },
                description: "URL rotated",
                ipAddress: "192.0.2.1",
                userAgent: "Famick/1.0 (test)");
        }

        await using var verify = _fixture.CreateDbContext();
        var row = await verify.UserAuditLogs
            .IgnoreQueryFilters()
            .SingleAsync(r => r.UserId == userId);

        row.Action.Should().Be(UserAuditAction.LocalServerChanged);
        row.TenantId.Should().Be(tenantId);
        row.OldValues.Should().Contain("http://old.local:8080");
        row.NewValues.Should().Contain("http://new.local:8080");
        row.Description.Should().Be("URL rotated");
        row.IpAddress.Should().Be("192.0.2.1");
        row.UserAgent.Should().Be("Famick/1.0 (test)");
        row.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task LogAsync_persists_null_jsonb_when_values_are_null()
    {
        var (userId, tenantId) = await SeedUserAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            var sut = new UserAuditLogger(db, NullLogger<UserAuditLogger>.Instance);
            await sut.LogAsync(
                userId,
                tenantId,
                UserAuditAction.LocalServerChanged,
                oldValues: null,
                newValues: null,
                description: null,
                ipAddress: null,
                userAgent: null);
        }

        await using var verify = _fixture.CreateDbContext();
        var row = await verify.UserAuditLogs
            .IgnoreQueryFilters()
            .SingleAsync(r => r.UserId == userId);

        row.OldValues.Should().BeNull("null inputs serialize to SQL NULL, not the string \"null\"");
        row.NewValues.Should().BeNull();
        row.Description.Should().BeNull();
        row.IpAddress.Should().BeNull();
        row.UserAgent.Should().BeNull();
    }

    [Fact]
    public async Task LogAsync_writes_multiple_rows_for_repeated_events()
    {
        var (userId, tenantId) = await SeedUserAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            var sut = new UserAuditLogger(db, NullLogger<UserAuditLogger>.Instance);
            for (var i = 0; i < 3; i++)
            {
                await sut.LogAsync(
                    userId,
                    tenantId,
                    UserAuditAction.LocalServerChanged,
                    oldValues: new { iteration = i },
                    newValues: new { iteration = i + 1 },
                    description: $"event {i}",
                    ipAddress: null,
                    userAgent: null);
            }
        }

        await using var verify = _fixture.CreateDbContext();
        var rows = await verify.UserAuditLogs
            .IgnoreQueryFilters()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        rows.Should().HaveCount(3);
    }
}
