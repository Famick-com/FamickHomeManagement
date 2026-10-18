using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Shared.Tests.Unit.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Issue #122: accepting an invitation is what creates a member's account, with a password only
/// they chose. The link is single-use, expires, and can be withdrawn or replaced.
/// </summary>
public class HouseholdInvitationServiceTests : IDisposable
{
    private const string BaseUrl = "https://app.famick.com";

    private readonly HomeManagementDbContext _context;
    private readonly Mock<IMessageService> _messages = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IContactService> _contacts = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private readonly List<string> _links = new();

    public HouseholdInvitationServiceTests()
    {
        // Tenant-filtered, so a lookup that forgets to look past one household shows up here.
        _tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            _tenantProvider.Object,
            new MultiTenancyOptions { IsMultiTenantEnabled = true });
        _messages
            .Setup(m => m.SendTransactionalAsync(
                It.IsAny<string>(), MessageType.HouseholdInvitation, It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()))
            .Callback<string, MessageType, IMessageData, CancellationToken>((_, _, data, _) =>
                _links.Add(((HouseholdInvitationData)data).AcceptLink))
            .Returns(Task.CompletedTask);

        _context.Tenants.AddRange(
            new Tenant { Id = _tenantId, Name = "Test Household" },
            new Tenant { Id = _otherTenantId, Name = "Other Household" });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private HouseholdInvitationService Service() =>
        UserServicesHarness.Build(_context, _tenantProvider.Object, mailConfigured: true, _messages.Object,
            contactService: _contacts.Object).Invitations;

    private async Task<(HouseholdInvitationDto Invitation, string Token)> InviteAsync(
        string email = "ann@example.com", Guid? contactId = null)
    {
        var invitation = await Service().InviteAsync(new CreateUserRequest
        {
            Email = email, FirstName = "Ann", LastName = "Example", Roles = [Role.Editor, Role.Viewer],
            ContactId = contactId
        }, BaseUrl);
        return (invitation, TokenOf(_links[^1]));
    }

    private static string TokenOf(string link) => Uri.UnescapeDataString(link[(link.IndexOf("token=") + 6)..]);

    private static AcceptInvitationRequest Accept(string token) =>
        new() { Token = token, Password = "chosen-by-ann", ConfirmPassword = "chosen-by-ann" };

    [Fact]
    public async Task Accepting_creates_the_member_with_their_own_password_and_the_invited_roles()
    {
        var (invitation, token) = await InviteAsync();

        var result = await Service().AcceptAsync(Accept(token));

        result.Success.Should().BeTrue();
        var user = await _context.Users.Include(u => u.UserRoles).SingleAsync();
        user.TenantId.Should().Be(_tenantId);
        user.Email.Should().Be("ann@example.com");
        user.PasswordHash.Should().Be("hashed:chosen-by-ann");
        user.MustChangePassword.Should().BeFalse();
        user.UserRoles.Select(r => r.Role).Should().BeEquivalentTo([Role.Editor, Role.Viewer]);
        _contacts.Verify(c => c.CreateContactForUserAsync(It.Is<User>(u => u.Id == user.Id), It.IsAny<CancellationToken>()), Times.Once);

        var stored = await _context.HouseholdInvitations.SingleAsync(i => i.Id == invitation.Id);
        stored.AcceptedAt.Should().NotBeNull();
        stored.AcceptedUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Accepting_links_the_contact_the_admin_chose()
    {
        var contactId = Guid.NewGuid();
        _context.Contacts.Add(new Contact { Id = contactId, TenantId = _tenantId, FirstName = "Ann", IsActive = true });
        await _context.SaveChangesAsync();
        var (_, token) = await InviteAsync(contactId: contactId);

        await Service().AcceptAsync(Accept(token));

        var user = await _context.Users.SingleAsync();
        (await _context.Contacts.SingleAsync(c => c.Id == contactId)).LinkedUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Accepting_with_a_short_password_is_refused_and_the_link_still_works()
    {
        var (_, token) = await InviteAsync();

        var result = await Service().AcceptAsync(new AcceptInvitationRequest { Token = token, Password = "short", ConfirmPassword = "short" });

        result.Success.Should().BeFalse();
        result.Message.Should().Be(Core.Validators.Common.PasswordRules.TooShortMessage);
        (await _context.Users.AnyAsync()).Should().BeFalse();
        (await Service().AcceptAsync(Accept(token))).Success.Should().BeTrue();
    }

    [Fact]
    public async Task A_link_works_once()
    {
        var (_, token) = await InviteAsync();
        await Service().AcceptAsync(Accept(token));

        var again = await Service().AcceptAsync(Accept(token));

        again.Success.Should().BeFalse();
        (await _context.Users.CountAsync()).Should().Be(1);
        (await Service().GetDetailsAsync(token)).IsValid.Should().BeFalse("a used link is no longer an invitation");
        (await Service().GetPendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task An_address_with_an_account_cannot_accept_and_is_told_so_only_through_the_link()
    {
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(), TenantId = _otherTenantId, Email = "ann@example.com", Username = "ann@example.com",
            FirstName = "Ann", LastName = "Elsewhere", PasswordHash = "theirs", IsActive = true
        });
        await _context.SaveChangesAsync();

        var invitation = await Service().InviteAsync(new CreateUserRequest
        {
            Email = "ann@example.com", FirstName = "Ann", LastName = "Example", Roles = [Role.Editor]
        }, BaseUrl);
        _links.Should().BeEmpty("the notice to an existing account carries no link to accept");

        var stored = await _context.HouseholdInvitations.SingleAsync(i => i.Id == invitation.Id);
        stored.TenantId.Should().Be(_tenantId);
        (await _context.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "ann@example.com")).Should().Be(1);
        (await _context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "ann@example.com"))
            .PasswordHash.Should().Be("theirs");
    }

    [Fact]
    public async Task Details_say_whether_the_link_can_be_accepted()
    {
        var (_, token) = await InviteAsync();

        var before = await Service().GetDetailsAsync(token);
        before.IsValid.Should().BeTrue();
        before.CanAccept.Should().BeTrue();
        before.HouseholdName.Should().Be("Test Household");
        before.Email.Should().Be("ann@example.com");

        // The address registers some other way before the link is used.
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(), TenantId = _otherTenantId, Email = "ann@example.com", Username = "ann@example.com",
            FirstName = "Ann", LastName = "Elsewhere", PasswordHash = "theirs", IsActive = true
        });
        await _context.SaveChangesAsync();

        (await Service().GetDetailsAsync(token)).CanAccept.Should().BeFalse();
        var accept = await Service().AcceptAsync(Accept(token));
        accept.Success.Should().BeFalse();
        (await _context.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "ann@example.com")).Should().Be(1);
    }

    [Fact]
    public async Task An_expired_link_does_not_work()
    {
        var (invitation, token) = await InviteAsync();
        var stored = await _context.HouseholdInvitations.SingleAsync(i => i.Id == invitation.Id);
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _context.SaveChangesAsync();

        (await Service().GetDetailsAsync(token)).IsValid.Should().BeFalse();
        (await Service().AcceptAsync(Accept(token))).Success.Should().BeFalse();
        (await Service().GetPendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task A_withdrawn_invitation_does_not_work()
    {
        var (invitation, token) = await InviteAsync();

        await Service().RevokeAsync(invitation.Id);

        (await Service().AcceptAsync(Accept(token))).Success.Should().BeFalse();
        (await Service().GetPendingAsync()).Should().BeEmpty();
        (await _context.Users.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Resending_replaces_the_link()
    {
        var (invitation, oldToken) = await InviteAsync();

        await Service().ResendAsync(invitation.Id, BaseUrl);
        var newToken = TokenOf(_links[^1]);

        newToken.Should().NotBe(oldToken);
        (await Service().GetDetailsAsync(oldToken)).IsValid.Should().BeFalse();
        (await Service().AcceptAsync(Accept(newToken))).Success.Should().BeTrue();
    }

    [Fact]
    public async Task Another_households_invitation_cannot_be_seen_resent_or_withdrawn()
    {
        var (invitation, token) = await InviteAsync();
        _tenantProvider.Setup(t => t.TenantId).Returns(_otherTenantId);

        (await Service().GetPendingAsync()).Should().BeEmpty();
        await FluentActions.Invoking(() => Service().ResendAsync(invitation.Id, BaseUrl))
            .Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Invoking(() => Service().RevokeAsync(invitation.Id))
            .Should().ThrowAsync<EntityNotFoundException>();
        (await Service().GetDetailsAsync(token)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Only_the_hash_of_the_token_is_stored()
    {
        var (invitation, token) = await InviteAsync();

        var stored = await _context.HouseholdInvitations.SingleAsync(i => i.Id == invitation.Id);
        stored.TokenHash.Should().NotBeNullOrEmpty().And.NotContain(token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task An_unknown_token_is_invalid(string token)
    {
        (await Service().GetDetailsAsync(token)).IsValid.Should().BeFalse();
        (await Service().AcceptAsync(Accept(token))).Success.Should().BeFalse();
    }
}
