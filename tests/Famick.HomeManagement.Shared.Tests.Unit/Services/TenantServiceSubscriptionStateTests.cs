using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// The subscription state <c>GET /api/v1/tenant</c> reports, which the web client loads at startup.
/// </summary>
public class TenantServiceSubscriptionStateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static TenantService Create(bool multiTenant, SubscriptionTier tier, DateTime? trialEndsAt)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(TenantId);
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new HomeManagementDbContext(options, tenantProvider.Object);
        context.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Household",
            SubscriptionTier = tier,
            TrialEndsAt = trialEndsAt,
        });
        context.SaveChanges();

        return new TenantService(
            context,
            tenantProvider.Object,
            new AddressHasher(new PassThroughAddressCanonicalizer()),
            Mock.Of<IMultiTenancyOptions>(o => o.IsMultiTenantEnabled == multiTenant),
            NullLogger<TenantService>.Instance);
    }

    [Fact]
    public async Task Self_hosted_reports_unrestricted_with_no_tier()
    {
        // The row's tier is unused on self-hosted, and its trial has long since ended.
        var sut = Create(multiTenant: false, SubscriptionTier.Free, trialEndsAt: DateTime.UtcNow.AddDays(-90));

        var tenant = await sut.GetCurrentTenantAsync();

        tenant.Should().NotBeNull();
        tenant!.IsUnrestricted.Should().BeTrue();
        tenant.SubscriptionTier.Should().BeEmpty();
        tenant.IsExpired.Should().BeFalse();
        tenant.IsTrialActive.Should().BeFalse();
        tenant.TrialEndsAt.Should().BeNull();
    }

    [Fact]
    public async Task Cloud_reports_what_the_row_says()
    {
        var sut = Create(multiTenant: true, SubscriptionTier.Organize, trialEndsAt: null);

        var tenant = await sut.GetCurrentTenantAsync();

        tenant.Should().NotBeNull();
        tenant!.IsUnrestricted.Should().BeFalse();
        tenant.SubscriptionTier.Should().Be("Organize");
    }
}
