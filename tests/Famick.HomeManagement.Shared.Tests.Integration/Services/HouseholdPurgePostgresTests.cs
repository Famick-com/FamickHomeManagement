using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// A household whose deletion has come due, purged against Postgres configured the way
/// <c>AddInfrastructure</c> configures it: with <c>EnableRetryOnFailure</c>. The purge runs in one
/// transaction, and the retrying strategy only accepts that when the transaction runs inside it.
/// </summary>
public class HouseholdPurgePostgresTests(PostgresContainerFixture fixture)
    : IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task AHouseholdDeletionCompletesWhenItComesDue()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        await using (var db = fixture.CreateDbContext())
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Household",
                DeletionRequestedAt = DateTime.UtcNow.AddDays(-31),
                DeletionRequestedByUserId = adminId,
                DeletionPurgeAfter = DateTime.UtcNow.AddMinutes(-1),
            });
            db.Users.Add(new User
            {
                Id = adminId, TenantId = tenantId, Username = $"u-{adminId:N}", Email = $"{adminId:N}@example.com",
                FirstName = "Test", LastName = "User", PasswordHash = "x", IsActive = true, CreatedAt = DateTime.UtcNow,
            });
            db.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), TenantId = tenantId, UserId = adminId, Role = Role.Admin });
            db.Locations.Add(new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pantry" });
            await db.SaveChangesAsync();
        }

        await using (var db = RetryingDbContext())
        {
            var deletion = new AccountDeletionService(
                db, Mock.Of<IJwtMinIatService>(), NullLogger<AccountDeletionService>.Instance);

            var summary = await deletion.PurgeDueAsync(DateTime.UtcNow);
            summary.HouseholdsPurged.Should().BeGreaterThanOrEqualTo(1);
        }

        await using (var db = fixture.CreateDbContext())
        {
            (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId)).Should().BeFalse();
            (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.TenantId == tenantId)).Should().BeFalse();
            (await db.Locations.IgnoreQueryFilters().AnyAsync(l => l.TenantId == tenantId)).Should().BeFalse();
        }
    }

    private HomeManagementDbContext RetryingDbContext() => new(
        new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseNpgsql(fixture.ConnectionString, npgsql => npgsql.EnableRetryOnFailure())
            .Options);
}
