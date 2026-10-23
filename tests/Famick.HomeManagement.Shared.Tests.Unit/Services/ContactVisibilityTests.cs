using Famick.HomeManagement.Core.DTOs.Contacts;
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
/// A contact one member keeps private must not reach another member by any read path, and
/// must not be changeable by them.
/// </summary>
public class ContactVisibilityTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();
    private readonly Guid _friend = Guid.NewGuid();

    private Guid _currentUser;

    private readonly HomeManagementDbContext _context;
    private readonly ContactService _service;
    private readonly ContactFeedService _feed;
    private readonly DietaryProfileService _dietary;

    public ContactVisibilityTests()
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

        _service = new ContactService(
            _context,
            tenantProvider.Object,
            Mock.Of<IFileStorageService>(),
            Mock.Of<IFileUrlService>(),
            new AddressHasher(new PassThroughAddressCanonicalizer()),
            Mock.Of<ILogger<ContactService>>());

        _feed = new ContactFeedService(_context, Mock.Of<IFileStorageService>(), Mock.Of<ILogger<ContactFeedService>>());
        _dietary = new DietaryProfileService(_context, tenantProvider.Object, Mock.Of<ILogger<DietaryProfileService>>());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task APrivateContactIsHiddenFromAnotherMembersReads()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.UserPrivate);

        _currentUser = _other;

        (await _service.GetByIdAsync(contact.Id)).Should().BeNull();
        (await _service.ListAsync(new ContactFilterRequest())).Items.Should().NotContain(c => c.Id == contact.Id);
        (await _service.SearchAsync("Secret")).Should().BeEmpty();
        await FluentActions.Awaiting(() => _service.GetAuditLogAsync(contact.Id))
            .Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => _service.GetAddressesAsync(contact.Id))
            .Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => _dietary.GetAsync(contact.Id))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task APrivateContactIsVisibleToItsCreator()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.UserPrivate);

        (await _service.GetByIdAsync(contact.Id)).Should().NotBeNull();
        (await _service.ListAsync(new ContactFilterRequest())).Items.Should().Contain(c => c.Id == contact.Id);
    }

    [Fact]
    public async Task APrivateContactIsAbsentFromAnotherMembersFeed()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.UserPrivate);
        await SeedFeedTokenAsync(_other, "other-token");
        await SeedFeedTokenAsync(_owner, "owner-token");

        (await _feed.GenerateVcfFeedAsync("other-token")).Should().NotContain(contact.Id.ToString());
        (await _feed.GenerateVcfFeedAsync("owner-token")).Should().Contain($"UID:{contact.Id}");
    }

    [Fact]
    public async Task AGroupNeitherListsNorCountsAMemberTheReaderCannotSee()
    {
        var (group, contact) = await SeedAsync(ContactVisibilityLevel.UserPrivate);

        _currentUser = _other;

        var detail = await _service.GetByIdAsync(group.Id);
        detail!.Members.Should().NotContain(m => m.Id == contact.Id);

        var listed = (await _service.ListGroupsAsync(new ContactFilterRequest())).Items.Single(g => g.Id == group.Id);
        listed.MemberCount.Should().Be(0);
        listed.MemberFirstNames.Should().NotContain("Secret");
    }

    [Fact]
    public async Task AnotherMemberCannotChangeAPrivateContact()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.UserPrivate);
        var phone = await AddPhoneAsync(contact.Id);

        _currentUser = _other;

        await FluentActions.Awaiting(() => _service.UpdateAsync(contact.Id, new UpdateContactRequest { FirstName = "Changed" }))
            .Should().ThrowAsync<EntityNotFoundException>();
        await FluentActions.Awaiting(() => _service.DeleteAsync(contact.Id))
            .Should().ThrowAsync<EntityNotFoundException>();

        // Addressed by a child id, the error names the child, so it does not confirm the contact.
        (await FluentActions.Awaiting(() => _service.RemovePhoneAsync(phone.Id))
            .Should().ThrowAsync<EntityNotFoundException>())
            .WithMessage($"*{nameof(ContactPhoneNumber)}*");
    }

    [Fact]
    public async Task AContactSharedByNameIsVisibleOnlyToThosePeople()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.SharedWithUsers);
        await _service.ShareContactAsync(contact.Id, new ShareContactRequest { SharedWithUserId = _friend });

        _currentUser = _friend;
        (await _service.GetByIdAsync(contact.Id)).Should().NotBeNull();

        _currentUser = _other;
        (await _service.GetByIdAsync(contact.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AViewOnlyShareCannotEdit()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.SharedWithUsers);
        await _service.ShareContactAsync(contact.Id, new ShareContactRequest { SharedWithUserId = _friend, CanEdit = false });

        _currentUser = _friend;

        await FluentActions.Awaiting(() => _service.UpdateAsync(contact.Id, new UpdateContactRequest
            {
                FirstName = "Changed",
                Visibility = ContactVisibilityLevel.SharedWithUsers,
            }))
            .Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task AnEditShareCanEditButNotChangeWhoSeesIt()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.SharedWithUsers);
        await _service.ShareContactAsync(contact.Id, new ShareContactRequest { SharedWithUserId = _friend, CanEdit = true });

        _currentUser = _friend;

        var updated = await _service.UpdateAsync(contact.Id, new UpdateContactRequest
        {
            FirstName = "Renamed",
            Visibility = ContactVisibilityLevel.SharedWithUsers,
        });
        updated.FirstName.Should().Be("Renamed");

        await FluentActions.Awaiting(() => _service.UpdateAsync(contact.Id, new UpdateContactRequest
            {
                FirstName = "Renamed",
                Visibility = ContactVisibilityLevel.TenantShared,
            }))
            .Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task OnlyTheCreatorManagesShares()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.TenantShared);

        _currentUser = _other;

        await FluentActions.Awaiting(() => _service.ShareContactAsync(contact.Id, new ShareContactRequest { SharedWithUserId = _other }))
            .Should().ThrowAsync<BusinessRuleViolationException>();

        _currentUser = _owner;
        var share = await _service.ShareContactAsync(contact.Id, new ShareContactRequest { SharedWithUserId = _friend });

        _currentUser = _other;
        await FluentActions.Awaiting(() => _service.UpdateShareAsync(share.Id, canEdit: true))
            .Should().ThrowAsync<BusinessRuleViolationException>();
        await FluentActions.Awaiting(() => _service.RemoveShareAsync(share.Id))
            .Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task AnUpdateThatLeavesVisibilityOutKeepsIt()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.SharedWithUsers);
        await _service.ShareContactAsync(contact.Id, new ShareContactRequest { SharedWithUserId = _friend, CanEdit = true });

        _currentUser = _friend;
        (await _service.UpdateAsync(contact.Id, new UpdateContactRequest { FirstName = "ByFriend" }))
            .Visibility.Should().Be(ContactVisibilityLevel.SharedWithUsers);

        _currentUser = _owner;
        (await _service.UpdateAsync(contact.Id, new UpdateContactRequest { FirstName = "ByOwner" }))
            .Visibility.Should().Be(ContactVisibilityLevel.SharedWithUsers);

        _currentUser = _other;
        (await _service.GetByIdAsync(contact.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AnUpdateThatLeavesActiveOutKeepsIt()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.TenantShared);
        await _service.UpdateAsync(contact.Id, new UpdateContactRequest { FirstName = "Retired", IsActive = false });

        (await _service.UpdateAsync(contact.Id, new UpdateContactRequest { FirstName = "Renamed" }))
            .IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AHouseholdContactStaysOpenToEveryMember()
    {
        var (_, contact) = await SeedAsync(ContactVisibilityLevel.TenantShared);

        _currentUser = _other;

        (await _service.GetByIdAsync(contact.Id)).Should().NotBeNull();
        var updated = await _service.UpdateAsync(contact.Id, new UpdateContactRequest { FirstName = "Edited" });
        updated.FirstName.Should().Be("Edited");
    }

    [Fact]
    public async Task TheCallerIsToldWhatTheyMayDoWithAContact()
    {
        var (group, household) = await SeedAsync(ContactVisibilityLevel.TenantShared);
        var shared = await AddContactAsync(group.Id, ContactVisibilityLevel.SharedWithUsers);
        var viewShare = await _service.ShareContactAsync(shared.Id, new ShareContactRequest { SharedWithUserId = _friend, CanEdit = false });
        await _service.ShareContactAsync(shared.Id, new ShareContactRequest { SharedWithUserId = _other, CanEdit = true });

        var asOwner = await _service.GetByIdAsync(shared.Id);
        asOwner!.IsOwner.Should().BeTrue();
        asOwner.CanEdit.Should().BeTrue();

        _currentUser = _other;
        var householdAsMember = await _service.GetByIdAsync(household.Id);
        householdAsMember!.IsOwner.Should().BeFalse();
        householdAsMember.CanEdit.Should().BeTrue();
        (await _service.GetByIdAsync(shared.Id))!.CanEdit.Should().BeTrue();

        _currentUser = _friend;
        var viewOnly = await _service.GetByIdAsync(shared.Id);
        viewOnly!.IsOwner.Should().BeFalse();
        viewOnly.CanEdit.Should().BeFalse();

        _currentUser = _owner;
        await _service.UpdateShareAsync(viewShare.Id, canEdit: true);
        _currentUser = _friend;
        (await _service.GetByIdAsync(shared.Id))!.CanEdit.Should().BeTrue();
    }

    private async Task<Contact> AddContactAsync(Guid groupId, ContactVisibilityLevel visibility)
    {
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = "Another",
            LastName = "Friend",
            ParentContactId = groupId,
            CreatedByUserId = _owner,
            Visibility = visibility,
            IsActive = true,
        };
        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();
        return contact;
    }

    private async Task<(Contact Group, Contact Contact)> SeedAsync(ContactVisibilityLevel visibility)
    {
        _context.Tenants.Add(new Tenant { Id = _tenantId, Name = "Household" });
        foreach (var (id, name) in new[] { (_owner, "owner"), (_other, "other"), (_friend, "friend") })
        {
            _context.Users.Add(new User
            {
                Id = id,
                TenantId = _tenantId,
                Email = $"{name}@example.com",
                FirstName = name,
                LastName = "Member",
                PasswordHash = "hashed",
            });
        }

        var group = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyName = "Household",
            ContactType = ContactType.Household,
            IsTenantHousehold = true,
            CreatedByUserId = _owner,
            Visibility = ContactVisibilityLevel.TenantShared,
            IsActive = true,
        };

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = "Secret",
            LastName = "Friend",
            ParentContactId = group.Id,
            CreatedByUserId = _owner,
            Visibility = visibility,
            IsActive = true,
        };

        _context.Contacts.AddRange(group, contact);
        await _context.SaveChangesAsync();
        return (group, contact);
    }

    private async Task<ContactPhoneNumber> AddPhoneAsync(Guid contactId)
    {
        var phone = new ContactPhoneNumber
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ContactId = contactId,
            PhoneNumber = "555-0100",
        };
        _context.ContactPhoneNumbers.Add(phone);
        await _context.SaveChangesAsync();
        return phone;
    }

    private async Task SeedFeedTokenAsync(Guid userId, string token)
    {
        _context.UserContactVcfTokens.Add(new UserContactVcfToken
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = userId,
            TokenHash = FeedTokens.Hash(token),
        });
        await _context.SaveChangesAsync();
    }
}
