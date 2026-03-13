using Famick.HomeManagement.Shared.Contacts;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class ContactHasherTests
{
    #region ComputeHash — Determinism & Stability

    [Fact]
    public void ComputeHash_SameData_ProducesSameHash()
    {
        var contact = CreateSampleContact();
        var hash1 = ContactHasher.ComputeHash(contact);
        var hash2 = ContactHasher.ComputeHash(contact);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentName_ProducesDifferentHash()
    {
        var contact1 = CreateSampleContact();
        var contact2 = CreateSampleContact();
        contact2.FirstName = "Jane";

        ContactHasher.ComputeHash(contact1).Should().NotBe(ContactHasher.ComputeHash(contact2));
    }

    [Fact]
    public void ComputeHash_DifferentPhone_ProducesDifferentHash()
    {
        var contact1 = CreateSampleContact();
        var contact2 = CreateSampleContact();
        contact2.PhoneNumbers[0].PhoneNumber = "555-9999";

        ContactHasher.ComputeHash(contact1).Should().NotBe(ContactHasher.ComputeHash(contact2));
    }

    [Fact]
    public void ComputeHash_DifferentEmail_ProducesDifferentHash()
    {
        var contact1 = CreateSampleContact();
        var contact2 = CreateSampleContact();
        contact2.EmailAddresses[0].Email = "different@example.com";

        ContactHasher.ComputeHash(contact1).Should().NotBe(ContactHasher.ComputeHash(contact2));
    }

    [Fact]
    public void ComputeHash_DifferentAddress_ProducesDifferentHash()
    {
        var contact1 = CreateSampleContact();
        var contact2 = CreateSampleContact();
        contact2.Addresses[0].City = "Los Angeles";

        ContactHasher.ComputeHash(contact1).Should().NotBe(ContactHasher.ComputeHash(contact2));
    }

    [Fact]
    public void ComputeHash_EmptyContact_DoesNotThrow()
    {
        var contact = new DeviceContactData();
        var hash = ContactHasher.ComputeHash(contact);

        hash.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ComputeHash — Collection Ordering

    [Fact]
    public void ComputeHash_PhoneOrder_DoesNotAffectHash()
    {
        var contact1 = CreateSampleContact();
        contact1.PhoneNumbers.Add(new DevicePhoneEntry { PhoneNumber = "555-2222", Tag = 1 });

        var contact2 = CreateSampleContact();
        contact2.PhoneNumbers.Insert(0, new DevicePhoneEntry { PhoneNumber = "555-2222", Tag = 1 });

        ContactHasher.ComputeHash(contact1).Should().Be(ContactHasher.ComputeHash(contact2));
    }

    [Fact]
    public void ComputeHash_EmailOrder_DoesNotAffectHash()
    {
        var contact1 = new DeviceContactData
        {
            EmailAddresses =
            {
                new DeviceEmailEntry { Email = "a@test.com", Tag = 0 },
                new DeviceEmailEntry { Email = "b@test.com", Tag = 1 }
            }
        };
        var contact2 = new DeviceContactData
        {
            EmailAddresses =
            {
                new DeviceEmailEntry { Email = "b@test.com", Tag = 1 },
                new DeviceEmailEntry { Email = "a@test.com", Tag = 0 }
            }
        };

        ContactHasher.ComputeHash(contact1).Should().Be(ContactHasher.ComputeHash(contact2));
    }

    [Fact]
    public void ComputeHash_AddressOrder_DoesNotAffectHash()
    {
        var contact1 = new DeviceContactData
        {
            Addresses =
            {
                new DeviceAddressEntry { AddressLine1 = "123 A St", City = "CityA", Tag = 0 },
                new DeviceAddressEntry { AddressLine1 = "456 B Ave", City = "CityB", Tag = 1 }
            }
        };
        var contact2 = new DeviceContactData
        {
            Addresses =
            {
                new DeviceAddressEntry { AddressLine1 = "456 B Ave", City = "CityB", Tag = 1 },
                new DeviceAddressEntry { AddressLine1 = "123 A St", City = "CityA", Tag = 0 }
            }
        };

        ContactHasher.ComputeHash(contact1).Should().Be(ContactHasher.ComputeHash(contact2));
    }

    [Fact]
    public void ComputeHash_SocialOrder_DoesNotAffectHash()
    {
        var contact1 = new DeviceContactData
        {
            SocialProfiles =
            {
                new DeviceSocialEntry { Service = 1, Username = "fbuser" },
                new DeviceSocialEntry { Service = 2, Username = "twuser" }
            }
        };
        var contact2 = new DeviceContactData
        {
            SocialProfiles =
            {
                new DeviceSocialEntry { Service = 2, Username = "twuser" },
                new DeviceSocialEntry { Service = 1, Username = "fbuser" }
            }
        };

        ContactHasher.ComputeHash(contact1).Should().Be(ContactHasher.ComputeHash(contact2));
    }

    #endregion

    #region ComputeHashWithPhotos

    [Fact]
    public void ComputeHashWithPhotos_DifferentFromComputeHash()
    {
        var contact = CreateSampleContact();
        var hashWithPhotos = ContactHasher.ComputeHashWithPhotos(contact, "photo.jpg", false, null);
        var hashWithout = ContactHasher.ComputeHash(contact);

        hashWithPhotos.Should().NotBe(hashWithout);
    }

    [Fact]
    public void ComputeHashWithPhotos_NoPhoto_MatchesComputeHash()
    {
        var contact = CreateSampleContact();
        var hashWithPhotos = ContactHasher.ComputeHashWithPhotos(contact, null, false, null);
        var hashWithout = ContactHasher.ComputeHash(contact);

        // With null photo and no gravatar, IMG: and GRAV: are both empty — matches ComputeHash
        hashWithPhotos.Should().Be(hashWithout);
    }

    [Fact]
    public void ComputeHashWithPhotos_DifferentPhoto_ProducesDifferentHash()
    {
        var contact = CreateSampleContact();
        var hash1 = ContactHasher.ComputeHashWithPhotos(contact, "photo1.jpg", false, null);
        var hash2 = ContactHasher.ComputeHashWithPhotos(contact, "photo2.jpg", false, null);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHashWithPhotos_GravatarEnabled_IncludesUrl()
    {
        var contact = CreateSampleContact();
        var hashNoGravatar = ContactHasher.ComputeHashWithPhotos(contact, null, false, null);
        var hashWithGravatar = ContactHasher.ComputeHashWithPhotos(
            contact, null, true, "https://gravatar.com/avatar/abc123");

        hashNoGravatar.Should().NotBe(hashWithGravatar);
    }

    [Fact]
    public void ComputeHashWithPhotos_GravatarDisabled_IgnoresUrl()
    {
        var contact = CreateSampleContact();
        var hashNoGravatar = ContactHasher.ComputeHashWithPhotos(contact, null, false, null);
        var hashDisabledGravatar = ContactHasher.ComputeHashWithPhotos(
            contact, null, false, "https://gravatar.com/avatar/abc123");

        // useGravatar=false means URL is ignored
        hashNoGravatar.Should().Be(hashDisabledGravatar);
    }

    #endregion

    #region Group Contacts

    [Fact]
    public void ComputeHash_GroupContact_UsesDisplayName()
    {
        var group1 = new DeviceContactData
        {
            IsGroup = true,
            DisplayName = "Smith Family",
            OrganizationName = "Smith Family"
        };
        var group2 = new DeviceContactData
        {
            IsGroup = true,
            DisplayName = "Jones Family",
            OrganizationName = "Jones Family"
        };

        ContactHasher.ComputeHash(group1).Should().NotBe(ContactHasher.ComputeHash(group2));
    }

    [Fact]
    public void ComputeHash_GroupContact_FallsBackToOrganizationName()
    {
        var group1 = new DeviceContactData
        {
            IsGroup = true,
            DisplayName = null,
            OrganizationName = "Smith Family"
        };
        var group2 = new DeviceContactData
        {
            IsGroup = true,
            DisplayName = "Smith Family",
            OrganizationName = "Smith Family"
        };

        // Both should produce the same hash because DisplayName ?? OrganizationName == "Smith Family"
        ContactHasher.ComputeHash(group1).Should().Be(ContactHasher.ComputeHash(group2));
    }

    [Fact]
    public void ComputeHash_NonGroup_IgnoresDisplayName()
    {
        var contact1 = CreateSampleContact();
        contact1.DisplayName = "Custom Display Name";

        var contact2 = CreateSampleContact();
        contact2.DisplayName = "Different Display Name";

        // For non-groups, DisplayName is not included in the hash
        ContactHasher.ComputeHash(contact1).Should().Be(ContactHasher.ComputeHash(contact2));
    }

    #endregion

    #region Tag Normalization

    [Theory]
    [InlineData(0, 0)]  // Mobile
    [InlineData(1, 1)]  // Home
    [InlineData(2, 2)]  // Work
    [InlineData(3, 3)]  // Fax
    [InlineData(4, 99)] // Unknown → Other
    [InlineData(99, 99)]
    [InlineData(-1, 99)]
    public void NormalizePhoneTag_ReturnsExpected(int input, int expected)
    {
        ContactHasher.NormalizePhoneTag(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0)]  // Personal/Home
    [InlineData(1, 1)]  // Work
    [InlineData(2, 99)] // School → Other
    [InlineData(3, 99)]
    [InlineData(99, 99)]
    public void NormalizeEmailTag_ReturnsExpected(int input, int expected)
    {
        ContactHasher.NormalizeEmailTag(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0)]  // Home
    [InlineData(1, 1)]  // Work
    [InlineData(2, 99)] // School → Other
    [InlineData(3, 99)] // Previous → Other
    [InlineData(4, 99)] // Vacation → Other
    [InlineData(99, 99)]
    public void NormalizeAddressTag_ReturnsExpected(int input, int expected)
    {
        ContactHasher.NormalizeAddressTag(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0)]  // Unknown stays 0
    [InlineData(1, 1)]  // Facebook
    [InlineData(2, 2)]  // Twitter
    [InlineData(3, 3)]  // Instagram
    [InlineData(4, 4)]  // LinkedIn
    [InlineData(5, 0)]  // TikTok → 0
    [InlineData(99, 0)]
    public void NormalizeSocialService_ReturnsExpected(int input, int expected)
    {
        ContactHasher.NormalizeSocialService(input).Should().Be(expected);
    }

    #endregion

    #region Notes Exclusion

    [Fact]
    public void ComputeHash_NotesField_ExcludedFromHash()
    {
        var contact1 = CreateSampleContact();
        contact1.Notes = "Some important notes";

        var contact2 = CreateSampleContact();
        contact2.Notes = null;

        // Notes are intentionally excluded (iOS can't read them)
        ContactHasher.ComputeHash(contact1).Should().Be(ContactHasher.ComputeHash(contact2));
    }

    #endregion

    #region Birth Date

    [Fact]
    public void ComputeHash_DifferentBirthDate_ProducesDifferentHash()
    {
        var contact1 = CreateSampleContact();
        contact1.BirthYear = 1990;
        contact1.BirthMonth = 6;
        contact1.BirthDay = 15;

        var contact2 = CreateSampleContact();
        contact2.BirthYear = 1990;
        contact2.BirthMonth = 6;
        contact2.BirthDay = 16;

        ContactHasher.ComputeHash(contact1).Should().NotBe(ContactHasher.ComputeHash(contact2));
    }

    #endregion

    #region All Fields Covered

    [Fact]
    public void ComputeHash_AllFieldsPopulated_ProducesStableHash()
    {
        var contact = new DeviceContactData
        {
            IsGroup = false,
            FirstName = "John",
            MiddleName = "Michael",
            LastName = "Doe",
            Nickname = "Johnny",
            OrganizationName = "Acme Corp",
            JobTitle = "Engineer",
            Website = "https://example.com",
            Notes = "Should be excluded",
            BirthYear = 1990,
            BirthMonth = 3,
            BirthDay = 15,
            PhoneNumbers =
            {
                new DevicePhoneEntry { PhoneNumber = "555-1234", Tag = 0 },
                new DevicePhoneEntry { PhoneNumber = "555-5678", Tag = 2 }
            },
            EmailAddresses =
            {
                new DeviceEmailEntry { Email = "john@example.com", Tag = 0 },
                new DeviceEmailEntry { Email = "john@work.com", Tag = 1 }
            },
            Addresses =
            {
                new DeviceAddressEntry
                {
                    AddressLine1 = "123 Main St",
                    City = "Springfield",
                    StateProvince = "IL",
                    PostalCode = "62701",
                    Country = "US",
                    Tag = 0
                }
            },
            SocialProfiles =
            {
                new DeviceSocialEntry { Service = 1, Username = "johndoe" },
                new DeviceSocialEntry { Service = 3, Username = "john.doe" }
            }
        };

        // Compute multiple times to verify stability
        var hashes = Enumerable.Range(0, 5).Select(_ => ContactHasher.ComputeHash(contact)).ToList();
        hashes.Should().AllBe(hashes[0]);
    }

    [Fact]
    public void ComputeHash_EachFieldChange_ProducesDifferentHash()
    {
        var baseline = ContactHasher.ComputeHash(CreateSampleContact());

        // Test each field individually
        var modifications = new (string Field, Action<DeviceContactData> Modify)[]
        {
            ("FirstName", c => c.FirstName = "Changed"),
            ("MiddleName", c => c.MiddleName = "Changed"),
            ("LastName", c => c.LastName = "Changed"),
            ("Nickname", c => c.Nickname = "Changed"),
            ("OrganizationName", c => c.OrganizationName = "Changed"),
            ("JobTitle", c => c.JobTitle = "Changed"),
            ("Website", c => c.Website = "https://changed.com"),
            ("BirthYear", c => c.BirthYear = 2000),
            ("BirthMonth", c => c.BirthMonth = 12),
            ("BirthDay", c => c.BirthDay = 25),
        };

        foreach (var (field, modify) in modifications)
        {
            var contact = CreateSampleContact();
            modify(contact);
            ContactHasher.ComputeHash(contact).Should().NotBe(baseline,
                because: $"changing {field} should produce a different hash");
        }
    }

    #endregion

    #region Helpers

    private static DeviceContactData CreateSampleContact()
    {
        return new DeviceContactData
        {
            IsGroup = false,
            FirstName = "John",
            LastName = "Doe",
            OrganizationName = "Acme Corp",
            PhoneNumbers =
            {
                new DevicePhoneEntry { PhoneNumber = "555-1234", Tag = 0 }
            },
            EmailAddresses =
            {
                new DeviceEmailEntry { Email = "john@example.com", Tag = 0 }
            },
            Addresses =
            {
                new DeviceAddressEntry
                {
                    AddressLine1 = "123 Main St",
                    City = "Springfield",
                    StateProvince = "IL",
                    PostalCode = "62701",
                    Country = "US",
                    Tag = 0
                }
            }
        };
    }

    #endregion
}
