using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class ContactMappingTests
{
    private readonly IMapper _mapper;

    public ContactMappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ContactMappingProfile>();
            cfg.AddProfile<TenantMappingProfile>(); // Required for Address -> AddressDto
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        // Validation skipped: profiles are tested in isolation
        _mapper = config.CreateMapper();
    }

    #region Contact -> ContactDto

    [Fact]
    public void Contact_To_ContactDto_MapsScalarProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var createdByUserId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = id,
            TenantId = tenantId,
            FirstName = "John",
            MiddleName = "Michael",
            LastName = "Doe",
            PreferredName = "Johnny",
            CompanyName = "Acme Inc",
            Title = "Manager",
            Gender = Gender.Male,
            BirthYear = 1990,
            BirthMonth = 6,
            BirthDay = 15,
            BirthDatePrecision = DatePrecision.Full,
            Notes = "Test notes",
            ContactType = ContactType.Household,
            ParentContactId = null,
            IsTenantHousehold = true,
            UseGravatar = false,
            Visibility = ContactVisibilityLevel.UserPrivate,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            CreatedByUser = new User { FirstName = "Admin", LastName = "User" },
            CreatedAt = new DateTime(2025, 1, 1),
            UpdatedAt = new DateTime(2025, 2, 1)
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.Id.Should().Be(id);
        dto.FirstName.Should().Be("John");
        dto.MiddleName.Should().Be("Michael");
        dto.LastName.Should().Be("Doe");
        dto.PreferredName.Should().Be("Johnny");
        dto.CompanyName.Should().Be("Acme Inc");
        dto.Title.Should().Be("Manager");
        dto.Gender.Should().Be(Gender.Male);
        dto.BirthYear.Should().Be(1990);
        dto.BirthMonth.Should().Be(6);
        dto.BirthDay.Should().Be(15);
        dto.BirthDatePrecision.Should().Be(DatePrecision.Full);
        dto.Notes.Should().Be("Test notes");
        dto.ContactType.Should().Be(ContactType.Household);
        dto.IsTenantHousehold.Should().BeTrue();
        dto.UseGravatar.Should().BeFalse();
        dto.Visibility.Should().Be(ContactVisibilityLevel.UserPrivate);
        dto.IsActive.Should().BeTrue();
        dto.CreatedByUserId.Should().Be(createdByUserId);
        dto.CreatedAt.Should().Be(new DateTime(2025, 1, 1));
        dto.UpdatedAt.Should().Be(new DateTime(2025, 2, 1));
    }

    [Fact]
    public void Contact_To_ContactDto_LinkedUserName_ConcatenatesFirstAndLastName()
    {
        var contact = new Contact
        {
            LinkedUserId = Guid.NewGuid(),
            LinkedUser = new User { FirstName = "Jane", LastName = "Smith" },
            CreatedByUser = new User { FirstName = "Admin", LastName = "User" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.LinkedUserName.Should().Be("Jane Smith");
        dto.LinkedUserId.Should().Be(contact.LinkedUserId);
    }

    [Fact]
    public void Contact_To_ContactDto_LinkedUserName_NullWhenNoLinkedUser()
    {
        var contact = new Contact
        {
            LinkedUserId = null,
            LinkedUser = null,
            CreatedByUser = new User { FirstName = "Admin", LastName = "User" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.LinkedUserName.Should().BeNull();
    }

    [Fact]
    public void Contact_To_ContactDto_CreatedByUserName_ConcatenatesNames()
    {
        var contact = new Contact
        {
            CreatedByUser = new User { FirstName = "Admin", LastName = "User" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.CreatedByUserName.Should().Be("Admin User");
    }

    [Fact]
    public void Contact_To_ContactDto_IsGroup_TrueWhenNoParentContactId()
    {
        var contact = new Contact
        {
            ParentContactId = null,
            CreatedByUser = new User { FirstName = "A", LastName = "B" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.IsGroup.Should().BeTrue();
    }

    [Fact]
    public void Contact_To_ContactDto_IsGroup_FalseWhenParentContactIdSet()
    {
        var contact = new Contact
        {
            ParentContactId = Guid.NewGuid(),
            CreatedByUser = new User { FirstName = "A", LastName = "B" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.IsGroup.Should().BeFalse();
    }

    [Fact]
    public void Contact_To_ContactDto_ParentGroupName_MapsFromParentContact()
    {
        var contact = new Contact
        {
            ParentContactId = Guid.NewGuid(),
            ParentContact = new Contact { CompanyName = "Smith Family" },
            CreatedByUser = new User { FirstName = "A", LastName = "B" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.ParentGroupName.Should().Be("Smith Family");
    }

    [Fact]
    public void Contact_To_ContactDto_ParentGroupName_NullWhenNoParentContact()
    {
        var contact = new Contact
        {
            ParentContactId = null,
            ParentContact = null,
            CreatedByUser = new User { FirstName = "A", LastName = "B" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.ParentGroupName.Should().BeNull();
    }

    [Fact]
    public void Contact_To_ContactDto_Tags_MapsFromTagLinks()
    {
        var tag1 = new ContactTag { Name = "Family", Color = "#FF0000" };
        var tag2 = new ContactTag { Name = "VIP", Color = "#00FF00" };
        var contact = new Contact
        {
            Tags = new List<ContactTagLink>
            {
                new() { Tag = tag1 },
                new() { Tag = tag2 }
            },
            CreatedByUser = new User { FirstName = "A", LastName = "B" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.Tags.Should().HaveCount(2);
        dto.Tags[0].Name.Should().Be("Family");
        dto.Tags[1].Name.Should().Be("VIP");
    }

    [Fact]
    public void Contact_To_ContactDto_IgnoredFields_RemainAtDefault()
    {
        var contact = new Contact
        {
            CreatedByUser = new User { FirstName = "A", LastName = "B" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.ProfileImageUrl.Should().BeNull();
        dto.GravatarUrl.Should().BeNull();
        dto.IsHouseholdMember.Should().BeFalse();
        dto.Members.Should().BeNull();
    }

    [Fact]
    public void Contact_To_ContactDto_CollectionMappings()
    {
        var contact = new Contact
        {
            Addresses = new List<ContactAddress>
            {
                new() { Id = Guid.NewGuid(), ContactId = Guid.NewGuid(), AddressId = Guid.NewGuid(), IsPrimary = true, Address = new Address { City = "NYC" } }
            },
            PhoneNumbers = new List<ContactPhoneNumber>
            {
                new() { PhoneNumber = "555-1234", IsPrimary = true }
            },
            EmailAddresses = new List<ContactEmailAddress>
            {
                new() { Email = "test@example.com", IsPrimary = true }
            },
            SocialMedia = new List<ContactSocialMedia>
            {
                new() { Service = SocialMediaService.Twitter, Username = "johndoe" }
            },
            RelationshipsAsSource = new List<ContactRelationship>
            {
                new() { RelationshipType = RelationshipType.Father, TargetContact = new Contact { FirstName = "Jane", LastName = "Doe" } }
            },
            SharedWithUsers = new List<ContactUserShare>
            {
                new() { SharedWithUser = new User { FirstName = "Bob", LastName = "Smith" } }
            },
            CreatedByUser = new User { FirstName = "A", LastName = "B" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.Addresses.Should().HaveCount(1);
        dto.PhoneNumbers.Should().HaveCount(1);
        dto.EmailAddresses.Should().HaveCount(1);
        dto.SocialMedia.Should().HaveCount(1);
        dto.Relationships.Should().HaveCount(1);
        dto.SharedWithUsers.Should().HaveCount(1);
    }

    [Fact]
    public void Contact_To_ContactDto_EmptyCollections_MapToEmptyLists()
    {
        var contact = new Contact
        {
            CreatedByUser = new User { FirstName = "A", LastName = "B" }
        };

        var dto = _mapper.Map<ContactDto>(contact);

        dto.Addresses.Should().BeEmpty();
        dto.PhoneNumbers.Should().BeEmpty();
        dto.EmailAddresses.Should().BeEmpty();
        dto.SocialMedia.Should().BeEmpty();
        dto.Relationships.Should().BeEmpty();
        dto.Tags.Should().BeEmpty();
        dto.SharedWithUsers.Should().BeEmpty();
    }

    #endregion

    #region Contact -> ContactSummaryDto

    [Fact]
    public void Contact_To_ContactSummaryDto_MapsScalarProperties()
    {
        var id = Guid.NewGuid();
        var contact = new Contact
        {
            Id = id,
            FirstName = "John",
            LastName = "Doe",
            PreferredName = "Johnny",
            CompanyName = "Acme",
            ContactType = ContactType.Business,
            Visibility = ContactVisibilityLevel.TenantShared,
            IsActive = true,
            CreatedAt = new DateTime(2025, 3, 1)
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.Id.Should().Be(id);
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Doe");
        dto.PreferredName.Should().Be("Johnny");
        dto.CompanyName.Should().Be("Acme");
        dto.ContactType.Should().Be(ContactType.Business);
        dto.Visibility.Should().Be(ContactVisibilityLevel.TenantShared);
        dto.IsActive.Should().BeTrue();
        dto.CreatedAt.Should().Be(new DateTime(2025, 3, 1));
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_IsUserLinked_TrueWhenLinkedUserIdSet()
    {
        var contact = new Contact { LinkedUserId = Guid.NewGuid() };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.IsUserLinked.Should().BeTrue();
        dto.LinkedUserId.Should().Be(contact.LinkedUserId);
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_IsUserLinked_FalseWhenNull()
    {
        var contact = new Contact { LinkedUserId = null };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.IsUserLinked.Should().BeFalse();
        dto.LinkedUserId.Should().BeNull();
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_IsGroup_TrueWhenNoParent()
    {
        var contact = new Contact { ParentContactId = null };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.IsGroup.Should().BeTrue();
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_IsGroup_FalseWhenHasParent()
    {
        var contact = new Contact { ParentContactId = Guid.NewGuid() };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.IsGroup.Should().BeFalse();
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_PrimaryEmail_SelectsPrimaryFirst()
    {
        var contact = new Contact
        {
            EmailAddresses = new List<ContactEmailAddress>
            {
                new() { Email = "older@test.com", IsPrimary = false, CreatedAt = new DateTime(2025, 1, 1) },
                new() { Email = "primary@test.com", IsPrimary = true, CreatedAt = new DateTime(2025, 2, 1) }
            }
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.PrimaryEmail.Should().Be("primary@test.com");
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_PrimaryEmail_FallsBackToOldestWhenNoPrimary()
    {
        var contact = new Contact
        {
            EmailAddresses = new List<ContactEmailAddress>
            {
                new() { Email = "newer@test.com", IsPrimary = false, CreatedAt = new DateTime(2025, 3, 1) },
                new() { Email = "oldest@test.com", IsPrimary = false, CreatedAt = new DateTime(2025, 1, 1) }
            }
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.PrimaryEmail.Should().Be("oldest@test.com");
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_PrimaryEmail_NullWhenNoEmails()
    {
        var contact = new Contact
        {
            EmailAddresses = new List<ContactEmailAddress>()
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.PrimaryEmail.Should().BeNull();
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_PrimaryPhone_SelectsPrimaryFirst()
    {
        var contact = new Contact
        {
            PhoneNumbers = new List<ContactPhoneNumber>
            {
                new() { PhoneNumber = "111-1111", IsPrimary = false, CreatedAt = new DateTime(2025, 1, 1) },
                new() { PhoneNumber = "222-2222", IsPrimary = true, CreatedAt = new DateTime(2025, 2, 1) }
            }
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.PrimaryPhone.Should().Be("222-2222");
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_PrimaryPhone_FallsBackToOldestWhenNoPrimary()
    {
        var contact = new Contact
        {
            PhoneNumbers = new List<ContactPhoneNumber>
            {
                new() { PhoneNumber = "333-3333", IsPrimary = false, CreatedAt = new DateTime(2025, 5, 1) },
                new() { PhoneNumber = "444-4444", IsPrimary = false, CreatedAt = new DateTime(2025, 1, 1) }
            }
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.PrimaryPhone.Should().Be("444-4444");
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_PrimaryPhone_NullWhenNoPhones()
    {
        var contact = new Contact
        {
            PhoneNumbers = new List<ContactPhoneNumber>()
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.PrimaryPhone.Should().BeNull();
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_PrimaryAddress_SelectsPrimaryWithFormattedAddress()
    {
        var contact = new Contact
        {
            Addresses = new List<ContactAddress>
            {
                new()
                {
                    IsPrimary = false,
                    CreatedAt = new DateTime(2025, 1, 1),
                    Address = new Address { FormattedAddress = "123 Old St", City = "Old City", StateProvince = "OS" }
                },
                new()
                {
                    IsPrimary = true,
                    CreatedAt = new DateTime(2025, 2, 1),
                    Address = new Address { FormattedAddress = "456 Main St", City = "New City", StateProvince = "NS" }
                }
            }
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.PrimaryAddress.Should().Be("456 Main St");
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_PrimaryAddress_FallsToCityStateWhenNoFormattedAddress()
    {
        var contact = new Contact
        {
            Addresses = new List<ContactAddress>
            {
                new()
                {
                    IsPrimary = true,
                    Address = new Address { FormattedAddress = null, City = "Springfield", StateProvince = "IL" }
                }
            }
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.PrimaryAddress.Should().Be("Springfield, IL");
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_PrimaryAddress_FallsBackToOldestWhenNoPrimary()
    {
        var contact = new Contact
        {
            Addresses = new List<ContactAddress>
            {
                new()
                {
                    IsPrimary = false,
                    CreatedAt = new DateTime(2025, 6, 1),
                    Address = new Address { FormattedAddress = "789 New Ave" }
                },
                new()
                {
                    IsPrimary = false,
                    CreatedAt = new DateTime(2025, 1, 1),
                    Address = new Address { FormattedAddress = "321 Old Blvd" }
                }
            }
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.PrimaryAddress.Should().Be("321 Old Blvd");
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_PrimaryAddress_NullWhenNoAddresses()
    {
        var contact = new Contact
        {
            Addresses = new List<ContactAddress>()
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.PrimaryAddress.Should().BeNull();
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_TagNames_MapsFromTagLinks()
    {
        var contact = new Contact
        {
            Tags = new List<ContactTagLink>
            {
                new() { Tag = new ContactTag { Name = "Friend", Color = "#0000FF" } },
                new() { Tag = new ContactTag { Name = "Neighbor", Color = null } }
            }
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.TagNames.Should().BeEquivalentTo(new List<string> { "Friend", "Neighbor" });
        dto.TagColors.Should().BeEquivalentTo(new List<string?> { "#0000FF", null });
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_EmptyTags_MapsToEmptyLists()
    {
        var contact = new Contact
        {
            Tags = new List<ContactTagLink>()
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.TagNames.Should().BeEmpty();
        dto.TagColors.Should().BeEmpty();
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_ParentGroupName_MapsFromParentContact()
    {
        var contact = new Contact
        {
            ParentContactId = Guid.NewGuid(),
            ParentContact = new Contact { CompanyName = "Doe Family" }
        };

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.ParentGroupName.Should().Be("Doe Family");
    }

    [Fact]
    public void Contact_To_ContactSummaryDto_IgnoredFields_RemainAtDefault()
    {
        var contact = new Contact();

        var dto = _mapper.Map<ContactSummaryDto>(contact);

        dto.ProfileImageUrl.Should().BeNull();
        dto.GravatarUrl.Should().BeNull();
    }

    #endregion

    #region CreateContactRequest -> Contact

    [Fact]
    public void CreateContactRequest_To_Contact_MapsEditableFields()
    {
        var request = new CreateContactRequest
        {
            FirstName = "Alice",
            MiddleName = "Marie",
            LastName = "Wonderland",
            PreferredName = "Ali",
            CompanyName = "Wonder Co",
            Title = "CEO",
            Gender = Gender.Female,
            BirthYear = 1985,
            BirthMonth = 3,
            BirthDay = 20,
            BirthDatePrecision = DatePrecision.Full,
            DeathYear = null,
            Notes = "Some notes",
            Visibility = ContactVisibilityLevel.UserPrivate,
            UseGravatar = false,
            ParentContactId = Guid.NewGuid()
        };

        var entity = _mapper.Map<Contact>(request);

        entity.FirstName.Should().Be("Alice");
        entity.MiddleName.Should().Be("Marie");
        entity.LastName.Should().Be("Wonderland");
        entity.PreferredName.Should().Be("Ali");
        entity.CompanyName.Should().Be("Wonder Co");
        entity.Title.Should().Be("CEO");
        entity.Gender.Should().Be(Gender.Female);
        entity.BirthYear.Should().Be(1985);
        entity.Notes.Should().Be("Some notes");
        entity.Visibility.Should().Be(ContactVisibilityLevel.UserPrivate);
        entity.UseGravatar.Should().BeFalse();
        entity.ParentContactId.Should().Be(request.ParentContactId);
    }

    [Fact]
    public void CreateContactRequest_To_Contact_IgnoredFieldsRemainAtDefault()
    {
        var request = new CreateContactRequest { FirstName = "Test" };

        var entity = _mapper.Map<Contact>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.CreatedAt.Should().BeAfter(DateTime.MinValue); // BaseEntity initializes to UtcNow
        entity.HouseholdTenantId.Should().BeNull();
        entity.LinkedUserId.Should().BeNull();
        entity.UsesTenantAddress.Should().BeFalse();
        entity.CreatedByUserId.Should().Be(Guid.Empty);
        entity.IsActive.Should().BeTrue(); // Default from entity
        entity.ProfileImageFileName.Should().BeNull();
        entity.ContactType.Should().BeNull();
        entity.IsTenantHousehold.Should().BeFalse();
        entity.UsesGroupAddress.Should().BeFalse();
        entity.Website.Should().BeNull();
        entity.BusinessCategory.Should().BeNull();
        entity.LinkedUser.Should().BeNull();
        entity.ParentContact.Should().BeNull();
        entity.Members.Should().BeEmpty();
        entity.Addresses.Should().BeEmpty();
        entity.PhoneNumbers.Should().BeEmpty();
        entity.EmailAddresses.Should().BeEmpty();
        entity.SocialMedia.Should().BeEmpty();
        entity.RelationshipsAsSource.Should().BeEmpty();
        entity.RelationshipsAsTarget.Should().BeEmpty();
        entity.Tags.Should().BeEmpty();
        entity.SharedWithUsers.Should().BeEmpty();
        entity.AuditLogs.Should().BeEmpty();
    }

    #endregion

    #region UpdateContactRequest -> Contact

    [Fact]
    public void UpdateContactRequest_To_Contact_MapsEditableFields()
    {
        var request = new UpdateContactRequest
        {
            FirstName = "Bob",
            LastName = "Updated",
            Gender = Gender.Male,
            IsActive = false,
            UseGravatar = true
        };

        var entity = _mapper.Map<Contact>(request);

        entity.FirstName.Should().Be("Bob");
        entity.LastName.Should().Be("Updated");
        entity.Gender.Should().Be(Gender.Male);
        entity.IsActive.Should().BeFalse();
        entity.UseGravatar.Should().BeTrue();
    }

    [Fact]
    public void UpdateContactRequest_To_Contact_IgnoredFieldsRemainAtDefault()
    {
        var request = new UpdateContactRequest { FirstName = "Test" };

        var entity = _mapper.Map<Contact>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.ParentContactId.Should().BeNull();
        entity.ContactType.Should().BeNull();
        entity.IsTenantHousehold.Should().BeFalse();
        entity.LinkedUserId.Should().BeNull();
    }

    #endregion

    #region ContactAddress -> ContactAddressDto

    [Fact]
    public void ContactAddress_To_ContactAddressDto_MapsAllFields()
    {
        var contactAddress = new ContactAddress
        {
            Id = Guid.NewGuid(),
            ContactId = Guid.NewGuid(),
            AddressId = Guid.NewGuid(),
            Tag = AddressTag.Work,
            IsPrimary = true,
            CreatedAt = new DateTime(2025, 1, 1),
            Address = new Address { City = "Boston", StateProvince = "MA" }
        };

        var dto = _mapper.Map<ContactAddressDto>(contactAddress);

        dto.Id.Should().Be(contactAddress.Id);
        dto.ContactId.Should().Be(contactAddress.ContactId);
        dto.AddressId.Should().Be(contactAddress.AddressId);
        dto.Tag.Should().Be(AddressTag.Work);
        dto.IsPrimary.Should().BeTrue();
        dto.Address.Should().NotBeNull();
        dto.Address.City.Should().Be("Boston");
    }

    [Fact]
    public void ContactAddress_To_ContactAddressDto_IsTenantAddress_TrueWhenContactUsesTenantAddressAndIsPrimary()
    {
        var contactAddress = new ContactAddress
        {
            IsPrimary = true,
            Contact = new Contact { UsesTenantAddress = true },
            Address = new Address()
        };

        var dto = _mapper.Map<ContactAddressDto>(contactAddress);

        dto.IsTenantAddress.Should().BeTrue();
    }

    [Fact]
    public void ContactAddress_To_ContactAddressDto_IsTenantAddress_FalseWhenNotPrimary()
    {
        var contactAddress = new ContactAddress
        {
            IsPrimary = false,
            Contact = new Contact { UsesTenantAddress = true },
            Address = new Address()
        };

        var dto = _mapper.Map<ContactAddressDto>(contactAddress);

        dto.IsTenantAddress.Should().BeFalse();
    }

    [Fact]
    public void ContactAddress_To_ContactAddressDto_IsTenantAddress_FalseWhenContactNull()
    {
        var contactAddress = new ContactAddress
        {
            IsPrimary = true,
            Contact = null!,
            Address = new Address()
        };

        var dto = _mapper.Map<ContactAddressDto>(contactAddress);

        dto.IsTenantAddress.Should().BeFalse();
    }

    #endregion

    #region ContactPhoneNumber -> ContactPhoneNumberDto

    [Fact]
    public void ContactPhoneNumber_To_ContactPhoneNumberDto_MapsAllFields()
    {
        var phone = new ContactPhoneNumber
        {
            Id = Guid.NewGuid(),
            ContactId = Guid.NewGuid(),
            PhoneNumber = "555-9876",
            NormalizedNumber = "5559876",
            Tag = PhoneTag.Work,
            IsPrimary = true,
            CreatedAt = new DateTime(2025, 5, 1)
        };

        var dto = _mapper.Map<ContactPhoneNumberDto>(phone);

        dto.Id.Should().Be(phone.Id);
        dto.ContactId.Should().Be(phone.ContactId);
        dto.PhoneNumber.Should().Be("555-9876");
        dto.NormalizedNumber.Should().Be("5559876");
        dto.Tag.Should().Be(PhoneTag.Work);
        dto.IsPrimary.Should().BeTrue();
    }

    #endregion

    #region ContactEmailAddress -> ContactEmailAddressDto

    [Fact]
    public void ContactEmailAddress_To_ContactEmailAddressDto_MapsAllFields()
    {
        var email = new ContactEmailAddress
        {
            Id = Guid.NewGuid(),
            ContactId = Guid.NewGuid(),
            Email = "test@example.com",
            NormalizedEmail = "test@example.com",
            Tag = EmailTag.Work,
            IsPrimary = false,
            Label = "Office",
            CreatedAt = new DateTime(2025, 4, 1)
        };

        var dto = _mapper.Map<ContactEmailAddressDto>(email);

        dto.Id.Should().Be(email.Id);
        dto.Email.Should().Be("test@example.com");
        dto.NormalizedEmail.Should().Be("test@example.com");
        dto.Tag.Should().Be(EmailTag.Work);
        dto.IsPrimary.Should().BeFalse();
        dto.Label.Should().Be("Office");
    }

    #endregion

    #region AddEmailRequest -> ContactEmailAddress

    [Fact]
    public void AddEmailRequest_To_ContactEmailAddress_MapsEditableFields()
    {
        var request = new AddEmailRequest
        {
            Email = "new@example.com",
            Tag = EmailTag.School,
            IsPrimary = true,
            Label = "University"
        };

        var entity = _mapper.Map<ContactEmailAddress>(request);

        entity.Email.Should().Be("new@example.com");
        entity.Tag.Should().Be(EmailTag.School);
        entity.IsPrimary.Should().BeTrue();
        entity.Label.Should().Be("University");
    }

    [Fact]
    public void AddEmailRequest_To_ContactEmailAddress_IgnoredFieldsRemainAtDefault()
    {
        var request = new AddEmailRequest { Email = "x@y.com" };

        var entity = _mapper.Map<ContactEmailAddress>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.ContactId.Should().Be(Guid.Empty);
        entity.NormalizedEmail.Should().BeNull();
        entity.Contact.Should().BeNull();
    }

    #endregion

    #region AddPhoneRequest -> ContactPhoneNumber

    [Fact]
    public void AddPhoneRequest_To_ContactPhoneNumber_MapsEditableFields()
    {
        var request = new AddPhoneRequest
        {
            PhoneNumber = "123-456-7890",
            Tag = PhoneTag.Home,
            IsPrimary = true
        };

        var entity = _mapper.Map<ContactPhoneNumber>(request);

        entity.PhoneNumber.Should().Be("123-456-7890");
        entity.Tag.Should().Be(PhoneTag.Home);
        entity.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void AddPhoneRequest_To_ContactPhoneNumber_IgnoredFieldsRemainAtDefault()
    {
        var request = new AddPhoneRequest { PhoneNumber = "000" };

        var entity = _mapper.Map<ContactPhoneNumber>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.ContactId.Should().Be(Guid.Empty);
        entity.NormalizedNumber.Should().BeNull();
        entity.Contact.Should().BeNull();
    }

    #endregion

    #region ContactSocialMedia -> ContactSocialMediaDto

    [Fact]
    public void ContactSocialMedia_To_ContactSocialMediaDto_MapsAllFields()
    {
        var sm = new ContactSocialMedia
        {
            Id = Guid.NewGuid(),
            ContactId = Guid.NewGuid(),
            Service = SocialMediaService.LinkedIn,
            Username = "john_doe",
            ProfileUrl = "https://linkedin.com/in/john_doe",
            CreatedAt = new DateTime(2025, 7, 1)
        };

        var dto = _mapper.Map<ContactSocialMediaDto>(sm);

        dto.Id.Should().Be(sm.Id);
        dto.ContactId.Should().Be(sm.ContactId);
        dto.Service.Should().Be(SocialMediaService.LinkedIn);
        dto.Username.Should().Be("john_doe");
        dto.ProfileUrl.Should().Be("https://linkedin.com/in/john_doe");
    }

    #endregion

    #region AddSocialMediaRequest -> ContactSocialMedia

    [Fact]
    public void AddSocialMediaRequest_To_ContactSocialMedia_MapsEditableFields()
    {
        var request = new AddSocialMediaRequest
        {
            Service = SocialMediaService.Twitter,
            Username = "jdoe",
            ProfileUrl = "https://twitter.com/jdoe"
        };

        var entity = _mapper.Map<ContactSocialMedia>(request);

        entity.Service.Should().Be(SocialMediaService.Twitter);
        entity.Username.Should().Be("jdoe");
        entity.ProfileUrl.Should().Be("https://twitter.com/jdoe");
    }

    [Fact]
    public void AddSocialMediaRequest_To_ContactSocialMedia_IgnoredFieldsRemainAtDefault()
    {
        var request = new AddSocialMediaRequest { Username = "test" };

        var entity = _mapper.Map<ContactSocialMedia>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.ContactId.Should().Be(Guid.Empty);
        entity.Contact.Should().BeNull();
    }

    #endregion

    #region ContactRelationship -> ContactRelationshipDto

    [Fact]
    public void ContactRelationship_To_ContactRelationshipDto_MapsWithPreferredName()
    {
        var rel = new ContactRelationship
        {
            Id = Guid.NewGuid(),
            SourceContactId = Guid.NewGuid(),
            TargetContactId = Guid.NewGuid(),
            RelationshipType = RelationshipType.Mother,
            CustomLabel = null,
            TargetContact = new Contact { PreferredName = "Mom", FirstName = "Mary", LastName = "Doe" }
        };

        var dto = _mapper.Map<ContactRelationshipDto>(rel);

        dto.TargetContactName.Should().Be("Mom");
        dto.RelationshipType.Should().Be(RelationshipType.Mother);
        dto.TargetIsUserLinked.Should().BeFalse();
    }

    [Fact]
    public void ContactRelationship_To_ContactRelationshipDto_FallsBackToFirstLastName()
    {
        var rel = new ContactRelationship
        {
            TargetContact = new Contact { PreferredName = null, FirstName = "Mary", LastName = "Doe" }
        };

        var dto = _mapper.Map<ContactRelationshipDto>(rel);

        dto.TargetContactName.Should().Be("Mary Doe");
    }

    [Fact]
    public void ContactRelationship_To_ContactRelationshipDto_FallsBackToFirstLastName_Trimmed()
    {
        var rel = new ContactRelationship
        {
            TargetContact = new Contact { PreferredName = "", FirstName = "Mary", LastName = "" }
        };

        var dto = _mapper.Map<ContactRelationshipDto>(rel);

        dto.TargetContactName.Should().Be("Mary");
    }

    [Fact]
    public void ContactRelationship_To_ContactRelationshipDto_EmptyStringWhenTargetContactNull()
    {
        var rel = new ContactRelationship { TargetContact = null };

        var dto = _mapper.Map<ContactRelationshipDto>(rel);

        dto.TargetContactName.Should().Be(string.Empty);
    }

    [Fact]
    public void ContactRelationship_To_ContactRelationshipDto_TargetIsUserLinked_True()
    {
        var rel = new ContactRelationship
        {
            TargetContact = new Contact { LinkedUserId = Guid.NewGuid(), FirstName = "X" }
        };

        var dto = _mapper.Map<ContactRelationshipDto>(rel);

        dto.TargetIsUserLinked.Should().BeTrue();
    }

    #endregion

    #region ContactTag -> ContactTagDto

    [Fact]
    public void ContactTag_To_ContactTagDto_MapsAllFields()
    {
        var tag = new ContactTag
        {
            Id = Guid.NewGuid(),
            Name = "VIP",
            Description = "Very important",
            Color = "#FF0000",
            Icon = "star",
            CreatedAt = new DateTime(2025, 1, 1),
            UpdatedAt = new DateTime(2025, 2, 1),
            Contacts = new List<ContactTagLink>
            {
                new(), new(), new()
            }
        };

        var dto = _mapper.Map<ContactTagDto>(tag);

        dto.Id.Should().Be(tag.Id);
        dto.Name.Should().Be("VIP");
        dto.Description.Should().Be("Very important");
        dto.Color.Should().Be("#FF0000");
        dto.Icon.Should().Be("star");
        dto.ContactCount.Should().Be(3);
        dto.CreatedAt.Should().Be(new DateTime(2025, 1, 1));
        dto.UpdatedAt.Should().Be(new DateTime(2025, 2, 1));
    }

    [Fact]
    public void ContactTag_To_ContactTagDto_ContactCount_ZeroWhenEmpty()
    {
        var tag = new ContactTag { Name = "Empty", Contacts = new List<ContactTagLink>() };

        var dto = _mapper.Map<ContactTagDto>(tag);

        dto.ContactCount.Should().Be(0);
    }

    #endregion

    #region CreateContactTagRequest -> ContactTag

    [Fact]
    public void CreateContactTagRequest_To_ContactTag_MapsEditableFields()
    {
        var request = new CreateContactTagRequest
        {
            Name = "Family",
            Description = "Family members",
            Color = "#00FF00",
            Icon = "people"
        };

        var entity = _mapper.Map<ContactTag>(request);

        entity.Name.Should().Be("Family");
        entity.Description.Should().Be("Family members");
        entity.Color.Should().Be("#00FF00");
        entity.Icon.Should().Be("people");
    }

    [Fact]
    public void CreateContactTagRequest_To_ContactTag_IgnoredFieldsRemainAtDefault()
    {
        var request = new CreateContactTagRequest { Name = "Test" };

        var entity = _mapper.Map<ContactTag>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.Contacts.Should().BeEmpty();
    }

    #endregion

    #region UpdateContactTagRequest -> ContactTag

    [Fact]
    public void UpdateContactTagRequest_To_ContactTag_MapsEditableFields()
    {
        var request = new UpdateContactTagRequest
        {
            Name = "Updated",
            Description = "Updated desc",
            Color = "#AABB00",
            Icon = "edit"
        };

        var entity = _mapper.Map<ContactTag>(request);

        entity.Name.Should().Be("Updated");
        entity.Description.Should().Be("Updated desc");
        entity.Color.Should().Be("#AABB00");
        entity.Icon.Should().Be("edit");
    }

    [Fact]
    public void UpdateContactTagRequest_To_ContactTag_IgnoredFieldsRemainAtDefault()
    {
        var request = new UpdateContactTagRequest { Name = "Test" };

        var entity = _mapper.Map<ContactTag>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.Contacts.Should().BeEmpty();
    }

    #endregion

    #region ContactUserShare -> ContactUserShareDto

    [Fact]
    public void ContactUserShare_To_ContactUserShareDto_MapsAllFields()
    {
        var share = new ContactUserShare
        {
            Id = Guid.NewGuid(),
            ContactId = Guid.NewGuid(),
            SharedWithUserId = Guid.NewGuid(),
            CanEdit = true,
            CreatedAt = new DateTime(2025, 6, 1),
            SharedWithUser = new User { FirstName = "Bob", LastName = "Builder" }
        };

        var dto = _mapper.Map<ContactUserShareDto>(share);

        dto.Id.Should().Be(share.Id);
        dto.ContactId.Should().Be(share.ContactId);
        dto.SharedWithUserId.Should().Be(share.SharedWithUserId);
        dto.CanEdit.Should().BeTrue();
        dto.SharedWithUserName.Should().Be("Bob Builder");
    }

    [Fact]
    public void ContactUserShare_To_ContactUserShareDto_EmptyNameWhenUserNull()
    {
        var share = new ContactUserShare { SharedWithUser = null! };

        var dto = _mapper.Map<ContactUserShareDto>(share);

        dto.SharedWithUserName.Should().Be(string.Empty);
    }

    #endregion

    #region ContactAuditLog -> ContactAuditLogDto

    [Fact]
    public void ContactAuditLog_To_ContactAuditLogDto_MapsAllFields()
    {
        var log = new ContactAuditLog
        {
            Id = Guid.NewGuid(),
            ContactId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Action = ContactAuditAction.Created,
            OldValues = null,
            NewValues = "{\"name\":\"test\"}",
            Description = "Contact created",
            IpAddress = "192.168.1.1",
            UserAgent = "TestAgent",
            CreatedAt = new DateTime(2025, 8, 1),
            User = new User { FirstName = "Admin", LastName = "User" }
        };

        var dto = _mapper.Map<ContactAuditLogDto>(log);

        dto.Id.Should().Be(log.Id);
        dto.ContactId.Should().Be(log.ContactId);
        dto.UserId.Should().Be(log.UserId);
        dto.Action.Should().Be(ContactAuditAction.Created);
        dto.OldValues.Should().BeNull();
        dto.NewValues.Should().Be("{\"name\":\"test\"}");
        dto.Description.Should().Be("Contact created");
        dto.IpAddress.Should().Be("192.168.1.1");
        dto.UserAgent.Should().Be("TestAgent");
        dto.UserName.Should().Be("Admin User");
    }

    [Fact]
    public void ContactAuditLog_To_ContactAuditLogDto_EmptyUserNameWhenUserNull()
    {
        var log = new ContactAuditLog { User = null! };

        var dto = _mapper.Map<ContactAuditLogDto>(log);

        dto.UserName.Should().Be(string.Empty);
    }

    #endregion
}
