using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Messaging.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Support;

/// <summary>
/// Builds <see cref="UserManagementService"/> with a real <see cref="HouseholdInvitationService"/>
/// and <see cref="PasswordResetService"/> behind it, the way DI does. <paramref name="mailConfigured"/>
/// picks between emailing a link (a server that can send email) and handing the admin a password
/// (one that cannot), for both creating an account and resetting one.
/// </summary>
internal static class UserServicesHarness
{
    public static (UserManagementService Users, HouseholdInvitationService Invitations) Build(
        HomeManagementDbContext db,
        ITenantProvider tenantProvider,
        bool mailConfigured,
        IMessageService? messageService = null,
        IPasswordHasher? passwordHasher = null,
        IContactService? contactService = null)
    {
        var hasher = passwordHasher ?? HashingAsPrefix();
        var settings = mailConfigured
            ? new EmailSettings { Provider = EmailProvider.AwsSes, FromEmail = "noreply@example.com" }
            : new EmailSettings();

        var factory = new HouseholdMemberFactory(
            db, contactService ?? Mock.Of<IContactService>(), NullLogger<HouseholdMemberFactory>.Instance);
        var invitations = new HouseholdInvitationService(
            db,
            tenantProvider,
            messageService ?? Mock.Of<IMessageService>(),
            hasher,
            factory,
            Options.Create(settings),
            NullLogger<HouseholdInvitationService>.Instance);
        var passwordReset = new PasswordResetService(
            db,
            Mock.Of<IEmailService>(),
            messageService ?? Mock.Of<IMessageService>(),
            hasher,
            Options.Create(settings),
            NullLogger<PasswordResetService>.Instance);
        var users = new UserManagementService(
            db, hasher, tenantProvider, invitations, passwordReset, factory, NullLogger<UserManagementService>.Instance);

        return (users, invitations);
    }

    /// <summary>Hashes "pw" to "hashed:pw", so a test can see which password was stored.</summary>
    public static IPasswordHasher HashingAsPrefix()
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => $"hashed:{p}");
        return hasher.Object;
    }
}
