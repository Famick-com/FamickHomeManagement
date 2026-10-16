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
/// Issue #122: a sign-in email names one person across every household. Adding a member never
/// mails a password; a server that can send email invites them instead, and answers the same
/// whether or not the address already has an account.
/// </summary>
public class UserManagementServiceTests : IDisposable
{
    private const string BaseUrl = "https://app.famick.com";

    private readonly HomeManagementDbContext _context;
    private readonly Mock<IMessageService> _messages = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();

    public UserManagementServiceTests()
    {
        // Tenant-filtered, the way an admin's request is: a check that forgets to look past this
        // household would pass against an unfiltered context.
        _tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            _tenantProvider.Object,
            new MultiTenancyOptions { IsMultiTenantEnabled = true });

        _context.Tenants.AddRange(
            new Tenant { Id = _tenantId, Name = "Test Household" },
            new Tenant { Id = _otherTenantId, Name = "Other Household" });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private UserManagementService Service(bool mailConfigured) =>
        UserServicesHarness.Build(_context, _tenantProvider.Object, mailConfigured, _messages.Object).Users;

    private static CreateUserRequest Request(string email, string? password = null) => new()
    {
        Email = email,
        FirstName = "Ann",
        LastName = "Example",
        Password = password,
        Roles = [Role.Editor],
        MustChangePassword = false,
        SendWelcomeEmail = true
    };

    private void AddUser(Guid tenantId, string email)
    {
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Email = email, Username = email,
            FirstName = "Existing", LastName = "User", PasswordHash = "x", IsActive = true
        });
        _context.SaveChanges();
    }

    #region Without mail: the account is created directly

    [Fact]
    public async Task Without_mail_the_account_is_created_with_the_admins_password_and_must_change_it()
    {
        var response = await Service(mailConfigured: false)
            .CreateUserAsync(Request("ann@example.com", "MySecurePassword123!"), BaseUrl);

        var user = await _context.Users.SingleAsync(u => u.Id == response.UserId);
        user.PasswordHash.Should().Be("hashed:MySecurePassword123!");
        user.MustChangePassword.Should().BeTrue("the request asked otherwise, but someone else chose this password");
        user.TenantId.Should().Be(_tenantId);
        response.InvitationId.Should().BeNull();
    }

    [Fact]
    public async Task Without_mail_a_generated_password_is_returned_to_the_admin()
    {
        var response = await Service(mailConfigured: false).CreateUserAsync(Request("ann@example.com"), BaseUrl);

        response.GeneratedPassword.Should().NotBeNullOrEmpty();
        response.GeneratedPassword!.Length.Should().Be(12);
    }

    [Fact]
    public async Task A_password_is_never_emailed()
    {
        var response = await Service(mailConfigured: false)
            .CreateUserAsync(Request("ann@example.com", "MySecurePassword123!"), BaseUrl);

        response.WelcomeEmailSent.Should().BeFalse();
        _messages.Verify(m => m.SendTransactionalAsync(
                It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task The_email_is_stored_normalised()
    {
        var response = await Service(mailConfigured: false).CreateUserAsync(Request("  Ann@Example.COM "), BaseUrl);

        (await _context.Users.SingleAsync(u => u.Id == response.UserId)).Email.Should().Be("ann@example.com");
    }

    [Fact]
    public async Task Without_mail_an_email_in_another_household_is_refused()
    {
        AddUser(_otherTenantId, "ann@example.com");

        var create = () => Service(mailConfigured: false).CreateUserAsync(Request("ANN@example.com"), BaseUrl);

        await create.Should().ThrowAsync<DuplicateEntityException>();
        (await _context.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "ann@example.com")).Should().Be(1);
    }

    #endregion

    #region With mail: an invitation, the same either way

    [Fact]
    public async Task With_mail_an_invitation_is_made_and_no_user()
    {
        var response = await Service(mailConfigured: true).CreateUserAsync(Request("ann@example.com", "AdminChosen1!"), BaseUrl);

        response.UserId.Should().BeEmpty();
        response.InvitationId.Should().NotBeNull();
        response.GeneratedPassword.Should().BeNull();
        (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == "ann@example.com")).Should().BeFalse();

        var invitation = await _context.HouseholdInvitations.SingleAsync();
        invitation.TenantId.Should().Be(_tenantId);
        invitation.Roles.Should().Equal(Role.Editor);
        _messages.Verify(m => m.SendTransactionalAsync(
                "ann@example.com", MessageType.HouseholdInvitation,
                It.Is<HouseholdInvitationData>(d => d.AcceptLink.StartsWith(BaseUrl + "/accept-invitation?token=")
                                                    && d.HouseholdName == "Test Household"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task With_mail_an_address_with_an_account_elsewhere_gets_the_same_response_and_a_notice()
    {
        AddUser(_otherTenantId, "taken@example.com");

        var fresh = await Service(mailConfigured: true).CreateUserAsync(Request("fresh@example.com"), BaseUrl);
        var taken = await Service(mailConfigured: true).CreateUserAsync(Request("taken@example.com"), BaseUrl);

        taken.Should().BeEquivalentTo(fresh, o => o
            .Excluding(r => r.InvitationId)
            .Excluding(r => r.Email));
        (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.TenantId == _tenantId)).Should().BeFalse();
        (await _context.HouseholdInvitations.CountAsync()).Should().Be(2, "both are pending invitations to the admin");
        (await _context.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "taken@example.com")).Should().Be(1);

        _messages.Verify(m => m.SendTransactionalAsync(
                "taken@example.com", MessageType.HouseholdInvitationExistingAccount,
                It.IsAny<HouseholdInvitationExistingAccountData>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _messages.Verify(m => m.SendTransactionalAsync(
                "taken@example.com", MessageType.HouseholdInvitation,
                It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task With_mail_a_member_of_this_household_is_refused()
    {
        AddUser(_tenantId, "ann@example.com");

        var create = () => Service(mailConfigured: true).CreateUserAsync(Request("ann@example.com"), BaseUrl);

        await create.Should().ThrowAsync<DuplicateEntityException>();
    }

    [Fact]
    public async Task With_mail_a_second_invitation_to_the_same_address_is_refused()
    {
        await Service(mailConfigured: true).CreateUserAsync(Request("ann@example.com"), BaseUrl);

        var again = () => Service(mailConfigured: true).CreateUserAsync(Request("Ann@Example.com"), BaseUrl);

        await again.Should().ThrowAsync<DuplicateEntityException>();
    }

    #endregion

    #region Update

    [Fact]
    public async Task Changing_an_email_to_one_used_in_another_household_is_refused()
    {
        AddUser(_otherTenantId, "taken@example.com");
        var created = await Service(mailConfigured: false).CreateUserAsync(Request("ann@example.com"), BaseUrl);

        var update = () => Service(mailConfigured: false).UpdateUserAsync(created.UserId, new UpdateUserRequest
        {
            Email = "Taken@Example.com", FirstName = "Ann", LastName = "Example", Roles = [Role.Editor], IsActive = true
        });

        await update.Should().ThrowAsync<DuplicateEntityException>();
    }

    #endregion
}
