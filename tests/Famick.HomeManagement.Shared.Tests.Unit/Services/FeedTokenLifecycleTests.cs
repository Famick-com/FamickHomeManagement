using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Shared.Tests.Unit.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// A calendar or contact feed URL is a standing credential. It must stop working when its owner
/// loses access, and only its owner may manage it.
/// </summary>
public class FeedTokenLifecycleTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();

    private Guid _currentUser;

    private readonly HomeManagementDbContext _context;
    private readonly CalendarFeedService _calendar;
    private readonly ContactFeedService _contacts;
    private readonly UserManagementService _users;

    public FeedTokenLifecycleTests()
    {
        _currentUser = _owner;

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        tenantProvider.Setup(t => t.UserId).Returns(() => _currentUser);

        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantProvider.Object);

        _calendar = new CalendarFeedService(_context, Mock.Of<ILogger<CalendarFeedService>>());
        _contacts = new ContactFeedService(_context, Mock.Of<IFileStorageService>(), Mock.Of<ILogger<ContactFeedService>>());
        (_users, _) = UserServicesHarness.Build(_context, tenantProvider.Object, mailConfigured: false);

        _context.Tenants.Add(new Tenant { Id = _tenantId, Name = "Household" });
        foreach (var (id, name) in new[] { (_owner, "owner"), (_other, "other") })
        {
            _context.Users.Add(new User
            {
                Id = id,
                TenantId = _tenantId,
                Email = $"{name}@example.com",
                Username = $"{name}@example.com",
                FirstName = name,
                LastName = "Member",
                PasswordHash = "hashed",
            });
        }
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ATokenIsShownOnceAndOnlyItsHashIsKept()
    {
        var created = await _calendar.CreateTokenAsync(new CreateIcsTokenRequest(), _owner);

        created.Token.Should().NotBeNullOrEmpty();
        (await _calendar.GenerateIcsFeedAsync(created.Token!)).Should().NotBeNull();

        (await _calendar.GetTokensAsync(_owner)).Single().Token.Should().BeNull();
        (await _context.UserCalendarIcsTokens.SingleAsync()).TokenHash
            .Should().Be(FeedTokens.Hash(created.Token!)).And.NotBe(created.Token);
    }

    [Fact]
    public async Task FeedsStopWhenTheirOwnerIsDeactivated()
    {
        var (ics, vcf) = await CreateBothAsync(_owner);

        var owner = await _context.Users.SingleAsync(u => u.Id == _owner);
        owner.IsActive = false;
        await _context.SaveChangesAsync();

        (await _calendar.GenerateIcsFeedAsync(ics)).Should().BeNull();
        (await _contacts.GenerateVcfFeedAsync(vcf)).Should().BeNull();
    }

    [Fact]
    public async Task FeedsStopWhenTheHouseholdIsDeactivated()
    {
        var (ics, vcf) = await CreateBothAsync(_owner);

        (await _context.Tenants.SingleAsync()).IsActive = false;
        await _context.SaveChangesAsync();

        (await _calendar.GenerateIcsFeedAsync(ics)).Should().BeNull();
        (await _contacts.GenerateVcfFeedAsync(vcf)).Should().BeNull();
    }

    [Fact]
    public async Task DeactivatingAMemberRevokesTheirFeedsForGood()
    {
        var (ics, vcf) = await CreateBothAsync(_other);

        await _users.UpdateUserAsync(_other, Update("other", isActive: false));
        await _users.UpdateUserAsync(_other, Update("other", isActive: true));

        (await _calendar.GenerateIcsFeedAsync(ics)).Should().BeNull("reactivating does not bring old URLs back");
        (await _contacts.GenerateVcfFeedAsync(vcf)).Should().BeNull();
    }

    [Fact]
    public async Task AnAdminPasswordResetRevokesTheMembersFeeds()
    {
        var (ics, vcf) = await CreateBothAsync(_other);

        await _users.AdminResetPasswordAsync(_other, new AdminResetPasswordRequest());

        (await _calendar.GenerateIcsFeedAsync(ics)).Should().BeNull();
        (await _contacts.GenerateVcfFeedAsync(vcf)).Should().BeNull();
    }

    [Fact]
    public async Task OnlyTheOwnerCanRevokeOrDeleteAToken()
    {
        var ics = await _calendar.CreateTokenAsync(new CreateIcsTokenRequest(), _owner);
        var vcf = await _contacts.CreateTokenAsync(new CreateVcfTokenRequest(), _owner);

        await FluentActions.Awaiting(() => _calendar.RevokeTokenAsync(ics.Id, _other))
            .Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => _calendar.DeleteTokenAsync(ics.Id, _other))
            .Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => _contacts.RevokeTokenAsync(vcf.Id, _other))
            .Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => _contacts.DeleteTokenAsync(vcf.Id, _other))
            .Should().ThrowAsync<EntityNotFoundException>();

        (await _calendar.GenerateIcsFeedAsync(ics.Token!)).Should().NotBeNull();
        (await _contacts.GenerateVcfFeedAsync(vcf.Token!)).Should().NotBeNull();

        await _calendar.RevokeTokenAsync(ics.Id, _owner);
        await _contacts.DeleteTokenAsync(vcf.Id, _owner);

        (await _calendar.GenerateIcsFeedAsync(ics.Token!)).Should().BeNull();
        (await _contacts.GenerateVcfFeedAsync(vcf.Token!)).Should().BeNull();
    }

    private async Task<(string Ics, string Vcf)> CreateBothAsync(Guid userId)
    {
        var ics = await _calendar.CreateTokenAsync(new CreateIcsTokenRequest(), userId);
        var vcf = await _contacts.CreateTokenAsync(new CreateVcfTokenRequest(), userId);

        (await _calendar.GenerateIcsFeedAsync(ics.Token!)).Should().NotBeNull();
        (await _contacts.GenerateVcfFeedAsync(vcf.Token!)).Should().NotBeNull();
        return (ics.Token!, vcf.Token!);
    }

    private static UpdateUserRequest Update(string name, bool isActive) => new()
    {
        Email = $"{name}@example.com",
        FirstName = name,
        LastName = "Member",
        Roles = [Role.Viewer],
        IsActive = isActive,
    };
}
