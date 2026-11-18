using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Helpers;

/// <summary>
/// The one definition of which details a household owns about its own people. When households
/// are linked, a copy of each of these is kept in the other household and only the owner may
/// change them; everything else on the copy belongs to the household holding it.
/// </summary>
/// <remarks>
/// <para>
/// Owned: the scalar details in <see cref="OwnedContactDetails"/>, the photo, and every row in
/// <see cref="ChildTypes"/>. Which group a contact belongs to is owned too, since membership of
/// a household is the owner's to decide.
/// </para>
/// <para>
/// Local to the holder: notes, tags, relationships to its own contacts, visibility, shares with
/// its own members, and whether the contact is active.
/// </para>
/// </remarks>
public static class ContactOwnedFields
{
    /// <summary>The rows stored under a contact that belong to its owner.</summary>
    public static readonly IReadOnlySet<Type> ChildTypes = new HashSet<Type>
    {
        typeof(ContactPhoneNumber),
        typeof(ContactEmailAddress),
        typeof(ContactAddress),
        typeof(ContactSocialMedia),
        typeof(ContactAllergen),
        typeof(ContactDietaryPreference),
    };

    /// <summary>The owned scalar details of <paramref name="contact"/>, for comparing or copying.</summary>
    public static OwnedContactDetails Snapshot(Contact contact) => new(
        contact.FirstName,
        contact.MiddleName,
        contact.LastName,
        contact.PreferredName,
        contact.CompanyName,
        contact.Title,
        contact.Gender,
        contact.BirthYear,
        contact.BirthMonth,
        contact.BirthDay,
        contact.BirthDatePrecision,
        contact.DeathYear,
        contact.DeathMonth,
        contact.DeathDay,
        contact.DeathDatePrecision,
        contact.ContactType,
        contact.Website,
        contact.BusinessCategory,
        contact.UseGravatar,
        contact.DietaryNotes);

    /// <summary>Writes the owned scalar details onto <paramref name="contact"/>.</summary>
    public static void ApplyTo(this OwnedContactDetails details, Contact contact)
    {
        contact.FirstName = details.FirstName;
        contact.MiddleName = details.MiddleName;
        contact.LastName = details.LastName;
        contact.PreferredName = details.PreferredName;
        contact.CompanyName = details.CompanyName;
        contact.Title = details.Title;
        contact.Gender = details.Gender;
        contact.BirthYear = details.BirthYear;
        contact.BirthMonth = details.BirthMonth;
        contact.BirthDay = details.BirthDay;
        contact.BirthDatePrecision = details.BirthDatePrecision;
        contact.DeathYear = details.DeathYear;
        contact.DeathMonth = details.DeathMonth;
        contact.DeathDay = details.DeathDay;
        contact.DeathDatePrecision = details.DeathDatePrecision;
        contact.ContactType = details.ContactType;
        contact.Website = details.Website;
        contact.BusinessCategory = details.BusinessCategory;
        contact.UseGravatar = details.UseGravatar;
        contact.DietaryNotes = details.DietaryNotes;
    }
}

/// <summary>
/// The owned scalar details of a contact. A record, so two snapshots compare by value.
/// </summary>
public sealed record OwnedContactDetails(
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? PreferredName,
    string? CompanyName,
    string? Title,
    Gender Gender,
    int? BirthYear,
    int? BirthMonth,
    int? BirthDay,
    DatePrecision BirthDatePrecision,
    int? DeathYear,
    int? DeathMonth,
    int? DeathDay,
    DatePrecision DeathDatePrecision,
    ContactType? ContactType,
    string? Website,
    string? BusinessCategory,
    bool UseGravatar,
    string? DietaryNotes);
