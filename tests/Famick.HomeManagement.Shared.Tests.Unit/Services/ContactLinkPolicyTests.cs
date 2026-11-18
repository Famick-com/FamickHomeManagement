using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// A copy whose details a linked household keeps takes this household's notes, tags,
/// relationships and visibility, and refuses every change to the details it does not own.
/// </summary>
public class ContactLinkPolicyTests : IDisposable
{
    private const string Smiths = "The Smiths";

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private readonly HomeManagementDbContext _context;
    private readonly FakeLinkPolicy _policy = new();
    private readonly ContactService _service;
    private readonly DietaryProfileService _dietary;

    private Contact _household = null!;
    private Contact _mirrorGroup = null!;
    private Contact _mirror = null!;
    private Contact _own = null!;

    public ContactLinkPolicyTests()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        tenantProvider.Setup(t => t.UserId).Returns(_userId);

        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantProvider.Object);

        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.SaveContactProfileImageAsync(
                It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("photo.jpg");

        _service = new ContactService(
            _context,
            tenantProvider.Object,
            storage.Object,
            Mock.Of<IFileUrlService>(),
            new AddressHasher(new PassThroughAddressCanonicalizer()),
            Mock.Of<ILogger<ContactService>>(),
            _policy);

        _dietary = new DietaryProfileService(
            _context, tenantProvider.Object, Mock.Of<ILogger<DietaryProfileService>>(), _policy);
    }

    public void Dispose() => _context.Dispose();

    public static TheoryData<string> OwnedChanges => new()
    {
        "name", "phone", "update phone", "remove phone", "email", "social", "address", "photo",
        "remove photo", "dietary", "device sync", "delete", "move", "group name", "delete group",
    };

    [Theory]
    [MemberData(nameof(OwnedChanges))]
    public async Task AChangeToAnOwnedDetailOnACopyIsRefused(string change)
    {
        await SeedAsync();

        var act = () => ApplyAsync(change, change is "group name" or "delete group" ? _mirrorGroup : _mirror);

        (await act.Should().ThrowAsync<ContactManagedElsewhereException>())
            .Which.ManagedByHousehold.Should().Be(Smiths);
    }

    [Theory]
    [MemberData(nameof(OwnedChanges))]
    public async Task TheSameChangeToThisHouseholdsOwnContactGoesThrough(string change)
    {
        await SeedAsync();
        var target = change is "group name" or "delete group" ? await AddGroupAsync() : _own;

        await FluentActions.Awaiting(() => ApplyAsync(change, target)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotesVisibilityTagsAndRelationshipsOnACopyStayEditable()
    {
        await SeedAsync();

        var updated = await _service.UpdateAsync(_mirror.Id, UpdateFrom(_mirror, r =>
        {
            r.Notes = "Allergic to cats";
            r.Visibility = ContactVisibilityLevel.UserPrivate;
        }));
        var tag = await _service.CreateTagAsync(new CreateContactTagRequest { Name = "Family" });
        await _service.AddTagToContactAsync(_mirror.Id, tag.Id);
        await _service.AddRelationshipAsync(_mirror.Id,
            new AddRelationshipRequest { TargetContactId = _own.Id, RelationshipType = RelationshipType.Sibling });

        updated.Notes.Should().Be("Allergic to cats");
        updated.Visibility.Should().Be(ContactVisibilityLevel.UserPrivate);
        (await _context.ContactTagLinks.CountAsync(l => l.ContactId == _mirror.Id)).Should().Be(1);
        (await _context.ContactRelationships.CountAsync(r => r.SourceContactId == _mirror.Id)).Should().Be(1);
    }

    [Fact]
    public async Task AnUnchangedFormSentBackForACopyIsNotRefused()
    {
        // Clients send the whole form; only a change to an owned detail counts.
        await SeedAsync();

        await FluentActions.Awaiting(() => _service.UpdateAsync(_mirror.Id, UpdateFrom(_mirror, _ => { })))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task AContactCannotBeAddedToAGroupAnotherHouseholdKeeps()
    {
        await SeedAsync();

        await FluentActions.Awaiting(() => _service.CreateAsync(
                new CreateContactRequest { FirstName = "New", ParentContactId = _mirrorGroup.Id }))
            .Should().ThrowAsync<ContactManagedElsewhereException>();
        await FluentActions.Awaiting(() => _service.MoveContactToGroupAsync(_own.Id, _mirrorGroup.Id))
            .Should().ThrowAsync<ContactManagedElsewhereException>();
    }

    [Fact]
    public async Task ReadsSayWhichHouseholdKeepsACopyAndWhoHoldsCopiesOfOurOwn()
    {
        await SeedAsync();
        _policy.Holders[_own.Id] = ["The Joneses"];

        var mirror = await _service.GetByIdAsync(_mirror.Id);
        var own = await _service.GetByIdAsync(_own.Id);
        var group = await _service.GetGroupByIdAsync(_mirrorGroup.Id);
        var list = await _service.ListAsync(new ContactFilterRequest());
        var groups = await _service.ListGroupsAsync(new ContactFilterRequest());
        var search = await _service.SearchAsync("Ann");

        mirror!.ManagedByHousehold.Should().Be(Smiths);
        own!.ManagedByHousehold.Should().BeNull();
        own.LinkedHouseholds.Should().Equal("The Joneses");
        group.ManagedByHousehold.Should().Be(Smiths);
        group.Members!.Single(m => m.Id == _mirror.Id).ManagedByHousehold.Should().Be(Smiths);
        list.Items.Single(c => c.Id == _mirror.Id).ManagedByHousehold.Should().Be(Smiths);
        list.Items.Single(c => c.Id == _own.Id).ManagedByHousehold.Should().BeNull();
        groups.Items.Single(g => g.Id == _mirrorGroup.Id).ManagedByHousehold.Should().Be(Smiths);
        search.Single(c => c.Id == _mirror.Id).ManagedByHousehold.Should().Be(Smiths);
    }

    private async Task ApplyAsync(string change, Contact target)
    {
        switch (change)
        {
            case "name":
                await _service.UpdateAsync(target.Id, UpdateFrom(target, r => r.FirstName = "Renamed"));
                break;
            case "phone":
                await _service.AddPhoneAsync(target.Id, new AddPhoneRequest { PhoneNumber = "555-0100" });
                break;
            case "update phone":
                await _service.UpdatePhoneAsync(await PhoneOfAsync(target), new AddPhoneRequest { PhoneNumber = "555-0199" });
                break;
            case "remove phone":
                await _service.RemovePhoneAsync(await PhoneOfAsync(target));
                break;
            case "email":
                await _service.AddEmailAsync(target.Id, new AddEmailRequest { Email = "ann@example.com" });
                break;
            case "social":
                await _service.AddSocialMediaAsync(target.Id,
                    new AddSocialMediaRequest { Service = SocialMediaService.Instagram, Username = "ann" });
                break;
            case "address":
                await _service.AddAddressAsync(target.Id, new AddContactAddressRequest
                {
                    AddressLine1 = "1 Main St", City = "Springfield", StateProvince = "IL", PostalCode = "62701",
                    Country = "US", Tag = AddressTag.Home,
                });
                break;
            case "photo":
                await _service.UploadProfileImageAsync(target.Id, new MemoryStream([1, 2, 3]), "photo.jpg");
                break;
            case "remove photo":
                await _service.DeleteProfileImageAsync(target.Id);
                break;
            case "dietary":
                await _dietary.UpdateAsync(target.Id, new UpdateDietaryProfileRequest { DietaryNotes = "No nuts" });
                break;
            case "device sync":
                await _service.ApplyDeviceSyncUpdateAsync(target.Id, new DeviceSyncUpdateRequest { FirstName = "Synced" });
                break;
            case "delete":
                await _service.DeleteAsync(target.Id);
                break;
            case "move":
                await _service.MoveContactToGroupAsync(target.Id, _household.Id);
                break;
            case "group name":
                await _service.UpdateGroupAsync(target.Id, new UpdateContactGroupRequest
                {
                    GroupName = "Renamed", ContactType = ContactType.Household, IsActive = true,
                });
                break;
            case "delete group":
                await _service.DeleteGroupAsync(target.Id);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change), change, null);
        }
    }

    private async Task<Guid> PhoneOfAsync(Contact contact)
    {
        var phone = new ContactPhoneNumber
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ContactId = contact.Id, PhoneNumber = "555-0101",
        };
        _context.ContactPhoneNumbers.Add(phone);
        await _context.SaveChangesAsync();
        return phone.Id;
    }

    private static UpdateContactRequest UpdateFrom(Contact contact, Action<UpdateContactRequest> change)
    {
        var request = new UpdateContactRequest
        {
            FirstName = contact.FirstName,
            LastName = contact.LastName,
            Gender = contact.Gender,
            UseGravatar = contact.UseGravatar,
            Notes = contact.Notes,
        };
        change(request);
        return request;
    }

    private async Task<Contact> AddGroupAsync()
    {
        var group = Contact("Our friends", parent: null);
        _context.Contacts.Add(group);
        await _context.SaveChangesAsync();
        return group;
    }

    private async Task SeedAsync()
    {
        _context.Tenants.Add(new Tenant { Id = _tenantId, Name = "Household" });
        _context.Users.Add(new User
        {
            Id = _userId, TenantId = _tenantId, Email = "owner@example.com", FirstName = "Owner",
            LastName = "Member", PasswordHash = "hashed",
        });

        _household = Contact("Household", parent: null);
        _household.IsTenantHousehold = true;
        _mirrorGroup = Contact(Smiths, parent: null);
        _mirror = Contact("Ann", parent: _mirrorGroup.Id);
        _own = Contact("Bob", parent: _household.Id);
        _context.Contacts.AddRange(_household, _mirrorGroup, _mirror, _own);
        await _context.SaveChangesAsync();

        _policy.ManagedBy[_mirrorGroup.Id] = Smiths;
        _policy.ManagedBy[_mirror.Id] = Smiths;
    }

    private Contact Contact(string name, Guid? parent) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        FirstName = parent == null ? null : name,
        CompanyName = parent == null ? name : null,
        ContactType = parent == null ? ContactType.Household : null,
        ParentContactId = parent,
        ProfileImageFileName = parent == null ? null : "existing.jpg",
        CreatedByUserId = _userId,
        Visibility = ContactVisibilityLevel.TenantShared,
        IsActive = true,
    };

    private sealed class FakeLinkPolicy : IContactLinkPolicy
    {
        public Dictionary<Guid, string> ManagedBy { get; } = new();
        public Dictionary<Guid, List<string>> Holders { get; } = new();

        public Task<IReadOnlyDictionary<Guid, ContactLinkInfo>> GetLinkInfoAsync(
            IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default)
        {
            IReadOnlyDictionary<Guid, ContactLinkInfo> result = contactIds
                .Where(id => ManagedBy.ContainsKey(id) || Holders.ContainsKey(id))
                .Distinct()
                .ToDictionary(id => id, id => new ContactLinkInfo(
                    ManagedBy.GetValueOrDefault(id), Holders.GetValueOrDefault(id) ?? []));
            return Task.FromResult(result);
        }
    }
}
