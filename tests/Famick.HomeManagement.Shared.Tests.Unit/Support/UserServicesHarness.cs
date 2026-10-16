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
/// behind it, the way DI does. <paramref name="mailConfigured"/> picks between inviting (a server
/// that can send email) and creating the account directly (one that cannot).
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
        var users = new UserManagementService(
            db, hasher, tenantProvider, invitations, factory, NullLogger<UserManagementService>.Instance);

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
