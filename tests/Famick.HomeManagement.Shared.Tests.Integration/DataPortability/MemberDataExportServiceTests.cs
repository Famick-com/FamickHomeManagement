using System.IO.Compression;
using System.Text.Json;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.DataPortability;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.DataPortability;

/// <summary>
/// Issue #99: a departing member's own data. What it must carry is what the removal takes away;
/// what it must not do is reach anybody but the member.
/// </summary>
public class MemberDataExportServiceTests(PostgresContainerFixture fixture)
    : IClassFixture<PostgresContainerFixture>
{
    private const string BaseUrl = "https://app.famick.com";

    [Fact]
    public async Task TheArchiveCarriesExactlyWhatLeavesWithTheMember()
    {
        var seed = await SeedAsync();
        var storage = new ArchiveStore();

        await using var db = fixture.CreateDbContext();
        var link = await Service(db, seed.TenantId, storage).CreateOwnExportLinkAsync(seed.MemberId);

        link.Should().StartWith(BaseUrl);

        var archive = storage.Single();
        var contacts = ReadJson(archive, "contacts.json").EnumerateArray()
            .ToDictionary(c => c.GetProperty("id").GetGuid());

        // Theirs and not shared with the household: in, with the rows hanging off them.
        contacts.Keys.Should().BeEquivalentTo(new[] { seed.PrivateContactId, seed.NamedShareContactId });
        contacts[seed.PrivateContactId].GetProperty("phoneNumbers")[0].GetProperty("value").GetString()
            .Should().Be("555-0100");
        contacts[seed.PrivateContactId].GetProperty("tags")[0].GetString().Should().Be("Book club");

        var vcf = ReadText(archive, "contacts.vcf");
        vcf.Should().Contain("Secret").And.Contain("555-0100");

        // The household's contacts are in the household's export, not this one; and another
        // member's private contact is not this member's to take.
        vcf.Should().NotContain("Shared Friend").And.NotContain("Admins Secret");

        var profile = ReadJson(archive, "profile.json");
        profile.GetProperty("account").GetProperty("email").GetString().Should().Be(seed.MemberEmail);
        profile.GetProperty("contactCard").GetProperty("dietaryNotes").GetString().Should().Be("No shellfish");
    }

    [Fact]
    public async Task TheRestOfTheHouseholdCannotSeeOrOpenAMembersExport()
    {
        var seed = await SeedAsync();
        var storage = new ArchiveStore();

        await using var db = fixture.CreateDbContext();
        await Service(db, seed.TenantId, storage).CreateOwnExportLinkAsync(seed.MemberId);

        var transferId = await db.HouseholdDataTransfers
            .Where(t => t.TenantId == seed.TenantId && t.Kind == HouseholdDataTransferKind.MemberExport)
            .Select(t => t.Id)
            .SingleAsync();

        // Every household read is open to any member. A member export reached through one would
        // hand the Admin exactly the contacts they were never allowed to see.
        var household = HouseholdService(db, seed.TenantId, storage);
        (await household.GetCapabilitiesAsync()).LatestExport.Should().BeNull();
        (await household.GetExportAsync(transferId)).Should().BeNull();
        (await household.GetDownloadLinkAsync(transferId)).Should().BeNull();
        (await household.OpenArchiveAsync(transferId, null, null)).Status.Should().Be(ExportDownloadStatus.Unavailable);
        (await household.DeleteExportAsync(transferId)).Should().BeFalse();

        // And the other way round: a member export lookup does not serve a household archive.
        (await Service(db, seed.TenantId, storage).OpenArchiveAsync(transferId, null, null)).Status
            .Should().Be(ExportDownloadStatus.Ok);
    }

    [Fact]
    public async Task ARemovedMemberIsEmailedTheirCopyAndItOutlivesTheirAccount()
    {
        var seed = await SeedAsync();
        var storage = new ArchiveStore();
        var messages = new Mock<IMessageService>();

        string? sentTo = null;
        MemberDataExportReadyData? sent = null;
        messages.Setup(m => m.SendTransactionalAsync(
                It.IsAny<string>(), MessageType.MemberDataExportReady, It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()))
            .Callback((string to, MessageType _, IMessageData data, CancellationToken _) =>
            {
                sentTo = to;
                sent = (MemberDataExportReadyData)data;
            })
            .Returns(Task.CompletedTask);

        await using (var db = fixture.CreateDbContext())
        {
            await Service(db, seed.TenantId, storage, messages.Object).SendToMemberAsync(seed.MemberId, seed.AdminId);
        }

        // To the member, not the Admin who removed them.
        sentTo.Should().Be(seed.MemberEmail);
        sent!.ContactCount.Should().Be(2);
        sent.DownloadLink.Should().StartWith(BaseUrl).And.Contain("token=");

        await using (var db = fixture.CreateDbContext())
        {
            await UserManagement(db, seed.TenantId).DeleteUserAsync(seed.MemberId, seed.AdminId);
        }

        // The link is all they have left, so the archive must not go with the account.
        await using (var db = fixture.CreateDbContext())
        {
            var transferId = await db.HouseholdDataTransfers
                .Where(t => t.TenantId == seed.TenantId && t.Kind == HouseholdDataTransferKind.MemberExport)
                .Select(t => t.Id)
                .SingleAsync();

            (await Service(db, seed.TenantId, storage).OpenArchiveAsync(transferId, null, null)).Status
                .Should().Be(ExportDownloadStatus.Ok);
        }
    }

    [Fact]
    public async Task WithoutMailItRefusesRatherThanBuildingAnArchiveNobodyReceives()
    {
        var seed = await SeedAsync();
        var storage = new ArchiveStore();

        await using var db = fixture.CreateDbContext();
        var service = Service(db, seed.TenantId, storage, messages: Mock.Of<IMessageService>(), mailConfigured: false);

        service.CanEmailMembers.Should().BeFalse();
        await FluentActions.Awaiting(() => service.SendToMemberAsync(seed.MemberId, seed.AdminId))
            .Should().ThrowAsync<BusinessRuleViolationException>();

        storage.Count.Should().Be(0);
    }

    [Fact]
    public async Task ALinkThatWouldNotWorkFromAnEmailClientIsNotOffered()
    {
        var seed = await SeedAsync();

        await using var db = fixture.CreateDbContext();

        // No base URL configured: the link would be relative, and dead in a mail client.
        Service(db, seed.TenantId, new ArchiveStore(baseUrl: ""), Mock.Of<IMessageService>())
            .CanEmailMembers.Should().BeFalse();

        // Cleartext to a public host would put a week-long token on the wire.
        Service(db, seed.TenantId, new ArchiveStore(baseUrl: "http://famick.example.com"), Mock.Of<IMessageService>())
            .CanEmailMembers.Should().BeFalse();

        Service(db, seed.TenantId, new ArchiveStore(), Mock.Of<IMessageService>())
            .CanEmailMembers.Should().BeTrue();
    }

    [Fact]
    public async Task RemovingAMemberDeletesTheCopiesTheyTookButNotTheOneEmailedToThem()
    {
        var seed = await SeedAsync();
        var storage = new ArchiveStore();

        await using (var db = fixture.CreateDbContext())
        {
            // One they downloaded themselves, then the one the Admin sends as they remove them.
            await Service(db, seed.TenantId, storage).CreateOwnExportLinkAsync(seed.MemberId);
            await Service(db, seed.TenantId, storage, Mock.Of<IMessageService>())
                .SendToMemberAsync(seed.MemberId, seed.AdminId);
        }

        Guid ownCopy, emailedCopy;
        await using (var db = fixture.CreateDbContext())
        {
            var transfers = await db.HouseholdDataTransfers
                .Where(t => t.TenantId == seed.TenantId && t.Kind == HouseholdDataTransferKind.MemberExport)
                .ToListAsync();
            ownCopy = transfers.Single(t => t.RequestedByUserId == seed.MemberId).Id;
            emailedCopy = transfers.Single(t => t.RequestedByUserId == seed.AdminId).Id;

            await UserManagement(db, seed.TenantId, storage).DeleteUserAsync(seed.MemberId, seed.AdminId);
        }

        // Its row went with the member, and nothing would ever find the file again.
        storage.Contains(ownCopy).Should().BeFalse();

        // The emailed link is the only copy they have left.
        storage.Contains(emailedCopy).Should().BeTrue();
    }

    #region Harness

    private static MemberDataExportService Service(
        HomeManagementDbContext db, Guid tenantId, ArchiveStore storage,
        IMessageService? messages = null, bool mailConfigured = true)
    {
        var tokens = new Mock<IFileAccessTokenService>();
        tokens.Setup(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns("signed-token");

        var settings = mailConfigured
            ? new EmailSettings { Provider = EmailProvider.AwsSes, FromEmail = "noreply@example.com" }
            : new EmailSettings();

        return new MemberDataExportService(
            db, Tenant(tenantId), storage.Object, tokens.Object, Options.Create(settings),
            NullLogger<MemberDataExportService>.Instance, messages);
    }

    private static HouseholdDataPortabilityService HouseholdService(
        HomeManagementDbContext db, Guid tenantId, ArchiveStore storage) =>
        new(db, Tenant(tenantId), storage.Object, Mock.Of<IFileAccessTokenService>(),
            new HouseholdArchiveWriter(storage.Object, NullLogger<HouseholdArchiveWriter>.Instance),
            NullLogger<HouseholdDataPortabilityService>.Instance);

    private static UserManagementService UserManagement(
        HomeManagementDbContext db, Guid tenantId, ArchiveStore? storage = null) =>
        new(db, Mock.Of<IPasswordHasher>(), Tenant(tenantId), Mock.Of<IHouseholdInvitationService>(),
            new HouseholdMemberFactory(db, Mock.Of<IContactService>(), NullLogger<HouseholdMemberFactory>.Instance),
            NullLogger<UserManagementService>.Instance,
            storage?.Object);

    private static ITenantProvider Tenant(Guid tenantId)
    {
        var provider = new Mock<ITenantProvider>();
        provider.SetupGet(p => p.TenantId).Returns(tenantId);
        return provider.Object;
    }

    /// <summary>Archive storage that keeps what it is given, so a test can open the result.</summary>
    private sealed class ArchiveStore
    {
        private readonly Dictionary<Guid, byte[]> _archives = new();

        public ArchiveStore(string baseUrl = BaseUrl)
        {
            var mock = new Mock<IFileStorageService>();

            mock.Setup(s => s.SaveExportArchiveAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async (Guid id, Stream stream, string name, CancellationToken ct) =>
                {
                    using var copy = new MemoryStream();
                    await stream.CopyToAsync(copy, ct);
                    _archives[id] = copy.ToArray();
                    return name;
                });

            mock.Setup(s => s.GetExportArchiveInfoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, string _, CancellationToken _) =>
                    _archives.TryGetValue(id, out var bytes) ? new StoredFileInfo(bytes.Length, DateTime.UtcNow) : null);

            mock.Setup(s => s.GetExportArchiveStreamAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, string _, long? _, long? _, CancellationToken _) =>
                    _archives.TryGetValue(id, out var bytes) ? new MemoryStream(bytes) : null);

            mock.Setup(s => s.DeleteExportArchiveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((Guid id, string _, CancellationToken _) => _archives.Remove(id))
                .Returns(Task.CompletedTask);

            mock.Setup(s => s.GetExportArchiveUrl(It.IsAny<Guid>(), It.IsAny<string?>()))
                .Returns((Guid id, string? token) =>
                    $"{baseUrl}/api/v1/data-portability/exports/{id}/download" + (token == null ? "" : $"?token={token}"));

            Object = mock.Object;
        }

        public IFileStorageService Object { get; }
        public int Count => _archives.Count;
        public bool Contains(Guid transferId) => _archives.ContainsKey(transferId);
        public byte[] Single() => _archives.Values.Single();
    }

    private static JsonElement ReadJson(byte[] archive, string entry) =>
        JsonDocument.Parse(ReadText(archive, entry)).RootElement.Clone();

    private static string ReadText(byte[] archive, string entry)
    {
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry(entry)!.Open());
        return reader.ReadToEnd();
    }

    private sealed record Seed(
        Guid TenantId, Guid AdminId, Guid MemberId, string MemberEmail, Guid PrivateContactId, Guid NamedShareContactId);

    private async Task<Seed> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using var db = fixture.CreateDbContext();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Household" });
        db.Users.AddRange(NewUser(tenantId, adminId), NewUser(tenantId, memberId));
        db.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), TenantId = tenantId, UserId = adminId, Role = Role.Admin });
        await db.SaveChangesAsync();

        var household = NewContact(tenantId, adminId, ContactVisibilityLevel.TenantShared, "Household");
        household.IsTenantHousehold = true;
        db.Contacts.Add(household);
        await db.SaveChangesAsync();

        var card = NewContact(tenantId, memberId, ContactVisibilityLevel.TenantShared, "Member", household.Id);
        card.DietaryNotes = "No shellfish";
        var privateContact = NewContact(tenantId, memberId, ContactVisibilityLevel.UserPrivate, "Secret", household.Id);
        var namedShare = NewContact(tenantId, memberId, ContactVisibilityLevel.SharedWithUsers, "Named", household.Id);
        var shared = NewContact(tenantId, memberId, ContactVisibilityLevel.TenantShared, "Shared Friend", household.Id);
        var adminsPrivate = NewContact(tenantId, adminId, ContactVisibilityLevel.UserPrivate, "Admins Secret", household.Id);
        db.Contacts.AddRange(card, privateContact, namedShare, shared, adminsPrivate);

        var tag = new ContactTag { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Book club" };
        db.ContactTags.Add(tag);
        await db.SaveChangesAsync();

        db.ContactPhoneNumbers.Add(new ContactPhoneNumber
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ContactId = privateContact.Id, PhoneNumber = "555-0100",
        });
        db.ContactTagLinks.Add(new ContactTagLink
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ContactId = privateContact.Id, TagId = tag.Id,
        });

        var member = await db.Users.SingleAsync(u => u.Id == memberId);
        member.ContactId = card.Id;
        await db.SaveChangesAsync();

        return new Seed(tenantId, adminId, memberId, member.Email, privateContact.Id, namedShare.Id);
    }

    private static User NewUser(Guid tenantId, Guid id) => new()
    {
        Id = id, TenantId = tenantId, Username = $"u-{id:N}", Email = $"{id:N}@example.com",
        FirstName = "Test", LastName = "User", PasswordHash = "x", IsActive = true,
    };

    private static Contact NewContact(
        Guid tenantId, Guid createdBy, ContactVisibilityLevel visibility, string name, Guid? parentId = null) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, FirstName = name, CreatedByUserId = createdBy,
        Visibility = visibility, ParentContactId = parentId,
    };

    #endregion
}
