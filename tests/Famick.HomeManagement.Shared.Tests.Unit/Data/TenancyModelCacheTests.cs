using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Famick.HomeManagement.Shared.Tests.Unit.Data;

/// <summary>
/// Contexts made in different tenancy modes in one process each read through their own mode's
/// filters, rather than whichever mode's model was built first.
/// </summary>
public class TenancyModelCacheTests
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly InMemoryDatabaseRoot _root = new();
    private readonly string _name = $"tenancy-model-{Guid.NewGuid():N}";

    private HomeManagementDbContext Context(MultiTenancyOptions tenancy, Guid? tenantId = null) => new(
        new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(_name, _root).Options,
        new FixedTenantProvider(tenantId),
        tenancy);

    [Fact]
    public async Task Each_tenancy_mode_reads_through_its_own_filters()
    {
        await using (var seed = Context(new MultiTenancyOptions { IsMultiTenantEnabled = false }))
        {
            foreach (var tenantId in new[] { _tenantA, _tenantB })
            {
                seed.Users.Add(new User
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, Username = $"u-{tenantId:N}", Email = $"{tenantId:N}@example.com"
                });
            }
            await seed.SaveChangesAsync();
        }

        // Most contexts in a test run are multi-tenant, so that model is usually cached already.
        // Build it first here too, so the result doesn't depend on which tests ran before.
        await using (var warm = Context(new MultiTenancyOptions { IsMultiTenantEnabled = true }, _tenantA))
            await warm.Users.CountAsync();

        await using var fixedToA = Context(new MultiTenancyOptions { IsMultiTenantEnabled = false, FixedTenantId = _tenantA });
        await using var multiAsB = Context(new MultiTenancyOptions { IsMultiTenantEnabled = true }, _tenantB);

        // A self-hosted server without a fixed tenant still knows its tenant; it just doesn't filter by it.
        await using var unfiltered = Context(new MultiTenancyOptions { IsMultiTenantEnabled = false }, _tenantA);

        (await fixedToA.Users.Select(u => u.TenantId).ToListAsync()).Should().Equal(_tenantA);
        (await multiAsB.Users.Select(u => u.TenantId).ToListAsync()).Should().Equal(_tenantB);
        (await unfiltered.Users.CountAsync()).Should().Be(2);
    }

    private sealed class FixedTenantProvider(Guid? tenantId) : ITenantProvider
    {
        public Guid? TenantId { get; private set; } = tenantId;
        public Guid? UserId { get; private set; }
        public void SetTenantId(Guid id) => TenantId = id;
        public void SetUserId(Guid userId) => UserId = userId;
        public void ClearTenantId() => TenantId = null;
        public void ClearUserId() => UserId = null;
    }
}
