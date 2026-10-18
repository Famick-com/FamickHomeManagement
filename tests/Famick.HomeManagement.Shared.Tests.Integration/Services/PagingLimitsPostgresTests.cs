using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.Notifications;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Validators.Common;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Paged lists bring their paging into range instead of failing. On Postgres a page below 1
/// used to become a negative OFFSET, which the database refuses (a 500); the in-memory provider
/// silently allows it, which is why these run against Postgres.
/// </summary>
public class PagingLimitsPostgresTests(PostgresContainerFixture fixture) : IClassFixture<PostgresContainerFixture>
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private HomeManagementDbContext Context()
    {
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        tenant.SetupGet(t => t.UserId).Returns(_userId);
        return new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseNpgsql(fixture.ConnectionString).Options,
            tenant.Object);
    }

    private ContactService Contacts(HomeManagementDbContext db)
    {
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        tenant.SetupGet(t => t.UserId).Returns(_userId);
        return new ContactService(db, tenant.Object, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            new AddressHasher(new PassThroughAddressCanonicalizer()), NullLogger<ContactService>.Instance);
    }

    private async Task SeedAsync(int contacts)
    {
        await using var db = Context();
        db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Household" });
        db.Users.Add(new User
        {
            Id = _userId, TenantId = _tenantId, Username = $"u-{_userId:N}", Email = $"{_userId:N}@example.com",
            FirstName = "Test", LastName = "User", PasswordHash = "x", IsActive = true,
        });
        for (var i = 0; i < contacts; i++)
        {
            db.Contacts.Add(new Contact
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, FirstName = $"Contact {i}", LastName = "Example",
                CreatedByUserId = _userId, IsActive = true,
            });
        }
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, UserId = _userId, Type = default(MessageType),
            Title = "Hello", Summary = "World",
        });
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_page_below_one_is_the_first_page(int page)
    {
        await SeedAsync(contacts: 3);
        await using var db = Context();

        var result = await Contacts(db).ListAsync(new ContactFilterRequest { Page = page, PageSize = 2 });

        result.Page.Should().Be(1);
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task An_oversized_page_size_is_capped()
    {
        await SeedAsync(contacts: 3);
        await using var db = Context();

        var result = await Contacts(db).ListAsync(new ContactFilterRequest { Page = 1, PageSize = 1_000_000 });

        result.PageSize.Should().Be(QueryLimits.MaxPageSize);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task A_huge_page_number_is_empty_rather_than_an_overflow()
    {
        await SeedAsync(contacts: 3);
        await using var db = Context();

        var result = await Contacts(db).ListAsync(new ContactFilterRequest { Page = int.MaxValue, PageSize = 100 });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Contact_groups_page_the_same_way()
    {
        await SeedAsync(contacts: 0);
        await using var db = Context();

        var result = await Contacts(db).ListGroupsAsync(new ContactFilterRequest { Page = 0, PageSize = 0 });

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task Notifications_page_the_same_way()
    {
        await SeedAsync(contacts: 0);
        await using var db = Context();

        var result = await new NotificationService(db, NullLogger<NotificationService>.Instance)
            .GetNotificationsAsync(_userId, new NotificationListRequest { Page = 0, PageSize = 100_000 });

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(QueryLimits.MaxPageSize);
        result.Items.Should().ContainSingle();
    }
}
