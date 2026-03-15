using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for ContactDetailPage section visibility logic.
/// Mirrors the visibility rules from ContactDetailPage.xaml.cs BindContactData().
///
/// Note: These tests recreate the visibility logic to avoid MAUI project dependency.
/// </summary>
public class ContactDetailSectionVisibilityTests
{
    [Fact]
    public void PhoneSection_AlwaysVisible_CollectionHiddenWhenEmpty()
    {
        var contact = CreateContact(phoneCount: 0);
        var visibility = ComputeSectionVisibility(contact);

        visibility.PhonesSectionVisible.Should().BeTrue("section header with Add button should always show");
        visibility.PhonesCollectionVisible.Should().BeFalse("empty collection should be hidden to save space");
    }

    [Fact]
    public void PhoneSection_CollectionVisibleWhenHasPhones()
    {
        var contact = CreateContact(phoneCount: 2);
        var visibility = ComputeSectionVisibility(contact);

        visibility.PhonesSectionVisible.Should().BeTrue();
        visibility.PhonesCollectionVisible.Should().BeTrue();
    }

    [Fact]
    public void EmailSection_AlwaysVisible_CollectionHiddenWhenEmpty()
    {
        var contact = CreateContact(emailCount: 0);
        var visibility = ComputeSectionVisibility(contact);

        visibility.EmailsSectionVisible.Should().BeTrue("section header with Add button should always show");
        visibility.EmailsCollectionVisible.Should().BeFalse("empty collection should be hidden to save space");
    }

    [Fact]
    public void EmailSection_CollectionVisibleWhenHasEmails()
    {
        var contact = CreateContact(emailCount: 1);
        var visibility = ComputeSectionVisibility(contact);

        visibility.EmailsSectionVisible.Should().BeTrue();
        visibility.EmailsCollectionVisible.Should().BeTrue();
    }

    [Fact]
    public void AddressSection_HiddenWhenEmpty()
    {
        var contact = CreateContact(addressCount: 0);
        var visibility = ComputeSectionVisibility(contact);

        visibility.AddressesSectionVisible.Should().BeFalse();
    }

    [Fact]
    public void AddressSection_VisibleWhenHasAddresses()
    {
        var contact = CreateContact(addressCount: 1);
        var visibility = ComputeSectionVisibility(contact);

        visibility.AddressesSectionVisible.Should().BeTrue();
    }

    [Fact]
    public void SocialSection_HiddenWhenEmpty()
    {
        var contact = CreateContact(socialCount: 0);
        var visibility = ComputeSectionVisibility(contact);

        visibility.SocialSectionVisible.Should().BeFalse();
    }

    [Fact]
    public void RelationshipsSection_HiddenWhenEmpty()
    {
        var contact = CreateContact(relationshipCount: 0);
        var visibility = ComputeSectionVisibility(contact);

        visibility.RelationshipsSectionVisible.Should().BeFalse();
    }

    [Fact]
    public void SharingSection_HiddenWhenEmpty()
    {
        var contact = CreateContact(shareCount: 0);
        var visibility = ComputeSectionVisibility(contact);

        visibility.SharingSectionVisible.Should().BeFalse();
    }

    [Fact]
    public void AllSections_ContactWithAllData_AllVisible()
    {
        var contact = CreateContact(
            phoneCount: 2, emailCount: 1, addressCount: 1,
            socialCount: 1, relationshipCount: 1, shareCount: 1);
        var visibility = ComputeSectionVisibility(contact);

        visibility.PhonesSectionVisible.Should().BeTrue();
        visibility.PhonesCollectionVisible.Should().BeTrue();
        visibility.EmailsSectionVisible.Should().BeTrue();
        visibility.EmailsCollectionVisible.Should().BeTrue();
        visibility.AddressesSectionVisible.Should().BeTrue();
        visibility.SocialSectionVisible.Should().BeTrue();
        visibility.RelationshipsSectionVisible.Should().BeTrue();
        visibility.SharingSectionVisible.Should().BeTrue();
    }

    #region Test Helpers

    private static TestContact CreateContact(
        int phoneCount = 0, int emailCount = 0, int addressCount = 0,
        int socialCount = 0, int relationshipCount = 0, int shareCount = 0)
    {
        return new TestContact
        {
            PhoneCount = phoneCount,
            EmailCount = emailCount,
            AddressCount = addressCount,
            SocialCount = socialCount,
            RelationshipCount = relationshipCount,
            ShareCount = shareCount,
        };
    }

    /// <summary>
    /// Mirrors the visibility logic from ContactDetailPage.xaml.cs BindContactData()
    /// </summary>
    private static SectionVisibility ComputeSectionVisibility(TestContact contact)
    {
        return new SectionVisibility
        {
            PhonesSectionVisible = true, // always show for + Add button
            PhonesCollectionVisible = contact.PhoneCount > 0,
            EmailsSectionVisible = true, // always show for + Add button
            EmailsCollectionVisible = contact.EmailCount > 0,
            AddressesSectionVisible = contact.AddressCount > 0,
            SocialSectionVisible = contact.SocialCount > 0,
            RelationshipsSectionVisible = contact.RelationshipCount > 0,
            SharingSectionVisible = contact.ShareCount > 0,
        };
    }

    private class TestContact
    {
        public int PhoneCount { get; set; }
        public int EmailCount { get; set; }
        public int AddressCount { get; set; }
        public int SocialCount { get; set; }
        public int RelationshipCount { get; set; }
        public int ShareCount { get; set; }
    }

    private class SectionVisibility
    {
        public bool PhonesSectionVisible { get; set; }
        public bool PhonesCollectionVisible { get; set; }
        public bool EmailsSectionVisible { get; set; }
        public bool EmailsCollectionVisible { get; set; }
        public bool AddressesSectionVisible { get; set; }
        public bool SocialSectionVisible { get; set; }
        public bool RelationshipsSectionVisible { get; set; }
        public bool SharingSectionVisible { get; set; }
    }

    #endregion
}
