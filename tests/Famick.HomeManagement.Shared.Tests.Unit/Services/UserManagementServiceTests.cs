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
        Core.Validators.Common.PasswordRules.IsAcceptable(response.GeneratedPassword).Should().BeTrue();
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
    public async Task Updating_a_member_never_changes_their_email()
    {
        AddUser(_otherTenantId, "taken@example.com");
        var created = await Service(mailConfigured: false).CreateUserAsync(Request("ann@example.com"), BaseUrl);

        var updated = await Service(mailConfigured: false).UpdateUserAsync(created.UserId, new UpdateUserRequest
        {
            FirstName = "Annie", LastName = "Example", Roles = [Role.Editor], IsActive = true
        });

        updated.Email.Should().Be("ann@example.com");
        updated.FirstName.Should().Be("Annie");
        var user = await _context.Users.SingleAsync(u => u.Id == created.UserId);
        user.Email.Should().Be("ann@example.com");
        user.Username.Should().Be("ann@example.com");
        (await _context.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "taken@example.com")).Should().Be(1);
    }

    [Fact]
    public void An_email_sent_with_an_update_is_ignored()
    {
        // Older clients still send the member's email; it must bind without error and go nowhere.
        var request = System.Text.Json.JsonSerializer.Deserialize<UpdateUserRequest>(
            """{"email":"someone@example.com","firstName":"Ann","lastName":"Example","roles":[1],"isActive":true}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        request.Should().NotBeNull();
        request!.FirstName.Should().Be("Ann");
        typeof(UpdateUserRequest).GetProperty("Email").Should().BeNull();
    }

    #endregion

    #region Password rule

    [Fact]
    public async Task An_admin_cannot_create_a_member_with_a_short_password()
    {
        var create = async () => await Service(mailConfigured: false).CreateUserAsync(Request("ann@example.com", "short"), BaseUrl);

        (await create.Should().ThrowAsync<FluentValidation.ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateUserRequest.Password));
        (await _context.Users.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task An_admin_cannot_reset_a_member_to_a_short_password()
    {
        AddUser(_tenantId, "ann@example.com");
        var user = await _context.Users.SingleAsync();

        var reset = async () => await Service(mailConfigured: false)
            .AdminResetPasswordAsync(user.Id, new AdminResetPasswordRequest { NewPassword = "short" }, BaseUrl);

        await reset.Should().ThrowAsync<FluentValidation.ValidationException>();
        (await _context.Users.AsNoTracking().SingleAsync()).PasswordHash.Should().Be("x");
    }

    [Fact]
    public async Task An_admin_reset_with_no_password_still_generates_one()
    {
        AddUser(_tenantId, "ann@example.com");
        var user = await _context.Users.SingleAsync();

        var response = await Service(mailConfigured: false)
            .AdminResetPasswordAsync(user.Id, new AdminResetPasswordRequest { NewPassword = null }, BaseUrl);

        response.GeneratedPassword.Should().NotBeNull();
        Core.Validators.Common.PasswordRules.IsAcceptable(response.GeneratedPassword).Should().BeTrue();
    }

    #endregion

    #region Admin reset with mail: the member is emailed a link (#136)

    private async Task<User> MemberWithASessionAsync()
    {
        AddUser(_tenantId, "ann@example.com");
        var user = await _context.Users.SingleAsync();
        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TenantId = _tenantId, TokenHash = "session",
            ExpiresAt = DateTime.UtcNow.AddDays(7), FamilyId = Guid.NewGuid()
        });
        await _context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task With_mail_a_reset_emails_the_member_a_link_and_hands_the_admin_no_password()
    {
        var user = await MemberWithASessionAsync();
        AdminPasswordResetData? sent = null;
        _messages
            .Setup(m => m.SendTransactionalAsync(
                "ann@example.com", MessageType.AdminPasswordReset, It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()))
            .Callback<string, MessageType, IMessageData, CancellationToken>((_, _, data, _) => sent = (AdminPasswordResetData)data)
            .Returns(Task.CompletedTask);

        var response = await Service(mailConfigured: true)
            .AdminResetPasswordAsync(user.Id, new AdminResetPasswordRequest(), BaseUrl);

        response.ResetEmailSent.Should().BeTrue();
        response.GeneratedPassword.Should().BeNull();
        sent.Should().NotBeNull();
        sent!.ResetLink.Should().StartWith($"{BaseUrl}/reset-password?token=");
        sent.ExpiresInHours.Should().Be(24);

        var token = await _context.PasswordResetTokens.SingleAsync();
        token.UserId.Should().Be(user.Id);
        token.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task With_mail_a_reset_ends_every_session_and_the_old_password_at_once()
    {
        var user = await MemberWithASessionAsync();

        await Service(mailConfigured: true)
            .AdminResetPasswordAsync(user.Id, new AdminResetPasswordRequest(), BaseUrl);

        var stored = await _context.Users.AsNoTracking().SingleAsync();
        stored.PasswordHash.Should().NotBe("x", "the old password must stop working before the member uses the link");
        stored.MustChangePassword.Should().BeFalse("the member chooses the next password themselves");
        (await _context.RefreshTokens.SingleAsync()).IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task With_mail_a_password_the_admin_typed_is_ignored()
    {
        var user = await MemberWithASessionAsync();

        var response = await Service(mailConfigured: true).AdminResetPasswordAsync(
            user.Id, new AdminResetPasswordRequest { NewPassword = "MySecurePassword123!" }, BaseUrl);

        response.ResetEmailSent.Should().BeTrue();
        (await _context.Users.AsNoTracking().SingleAsync()).PasswordHash
            .Should().NotBe("hashed:MySecurePassword123!");
    }

    [Fact]
    public async Task With_mail_a_reset_whose_email_fails_falls_back_to_a_temporary_password()
    {
        // Otherwise the member is signed out with no link and no password: locked out.
        var user = await MemberWithASessionAsync();
        _messages
            .Setup(m => m.SendTransactionalAsync(
                It.IsAny<string>(), MessageType.AdminPasswordReset, It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mail provider down"));

        var response = await Service(mailConfigured: true)
            .AdminResetPasswordAsync(user.Id, new AdminResetPasswordRequest(), BaseUrl);

        response.ResetEmailSent.Should().BeFalse();
        response.GeneratedPassword.Should().NotBeNull();
        var stored = await _context.Users.AsNoTracking().SingleAsync();
        stored.PasswordHash.Should().Be($"hashed:{response.GeneratedPassword}");
        stored.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Without_mail_a_reset_sends_nothing_and_returns_the_password()
    {
        var user = await MemberWithASessionAsync();

        var response = await Service(mailConfigured: false)
            .AdminResetPasswordAsync(user.Id, new AdminResetPasswordRequest(), BaseUrl);

        response.ResetEmailSent.Should().BeFalse();
        response.GeneratedPassword.Should().NotBeNull();
        (await _context.Users.AsNoTracking().SingleAsync()).MustChangePassword.Should().BeTrue();
        (await _context.RefreshTokens.SingleAsync()).IsRevoked.Should().BeTrue();
        _messages.Verify(m => m.SendTransactionalAsync(
                It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
