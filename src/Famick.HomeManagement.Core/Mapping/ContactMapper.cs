#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class ContactMapper
{
    // Contact -> ContactDto (computed fields: LinkedUserName, CreatedByUserName, IsGroup, ParentGroupName, Tags)
    public static ContactDto ToDto(Contact source)
    {
        var dto = MapContactToDto(source);
        dto.LinkedUserName = source.LinkedUser != null
            ? $"{source.LinkedUser.FirstName} {source.LinkedUser.LastName}"
            : null;
        dto.CreatedByUserName = $"{source.CreatedByUser.FirstName} {source.CreatedByUser.LastName}";
        dto.IsGroup = source.ParentContactId == null;
        dto.ParentGroupName = source.ParentContact != null ? source.ParentContact.CompanyName : null;
        dto.Tags = source.Tags.Select(t => ToContactTagDto(t.Tag)).ToList();
        return dto;
    }

    [MapperIgnoreTarget(nameof(ContactDto.LinkedUserName))]
    [MapperIgnoreTarget(nameof(ContactDto.CreatedByUserName))]
    [MapperIgnoreTarget(nameof(ContactDto.IsGroup))]
    [MapperIgnoreTarget(nameof(ContactDto.ParentGroupName))]
    [MapperIgnoreTarget(nameof(ContactDto.Tags))]
    [MapperIgnoreTarget(nameof(ContactDto.ProfileImageUrl))]
    [MapperIgnoreTarget(nameof(ContactDto.GravatarUrl))]
    [MapperIgnoreTarget(nameof(ContactDto.IsHouseholdMember))]
    [MapperIgnoreTarget(nameof(ContactDto.Members))]
    [MapProperty(nameof(Contact.RelationshipsAsSource), nameof(ContactDto.Relationships))]
    private static partial ContactDto MapContactToDto(Contact source);

    // Contact -> ContactSummaryDto (computed fields: PrimaryEmail, PrimaryPhone, PrimaryAddress, TagNames, TagColors, IsUserLinked, IsGroup, ParentGroupName)
    public static ContactSummaryDto ToSummaryDto(Contact source)
    {
        var dto = MapContactToSummaryDto(source);
        dto.IsUserLinked = source.LinkedUserId.HasValue;
        dto.IsGroup = source.ParentContactId == null;
        dto.ParentGroupName = source.ParentContact != null ? source.ParentContact.CompanyName : null;
        dto.PrimaryEmail = source.EmailAddresses
            .Where(e => e.IsPrimary)
            .Select(e => e.Email)
            .FirstOrDefault() ?? source.EmailAddresses
            .OrderBy(e => e.CreatedAt)
            .Select(e => e.Email)
            .FirstOrDefault();
        dto.PrimaryPhone = source.PhoneNumbers
            .Where(p => p.IsPrimary)
            .Select(p => p.PhoneNumber)
            .FirstOrDefault() ?? source.PhoneNumbers
            .OrderBy(p => p.CreatedAt)
            .Select(p => p.PhoneNumber)
            .FirstOrDefault();
        dto.PrimaryAddress = source.Addresses
            .Where(a => a.IsPrimary)
            .Select(a => a.Address.FormattedAddress ?? $"{a.Address.City}, {a.Address.StateProvince}")
            .FirstOrDefault() ?? source.Addresses
            .OrderBy(a => a.CreatedAt)
            .Select(a => a.Address.FormattedAddress ?? $"{a.Address.City}, {a.Address.StateProvince}")
            .FirstOrDefault();
        dto.TagNames = source.Tags.Select(t => t.Tag.Name).ToList();
        dto.TagColors = source.Tags.Select(t => t.Tag.Color).ToList();
        return dto;
    }

    [MapperIgnoreTarget(nameof(ContactSummaryDto.IsUserLinked))]
    [MapperIgnoreTarget(nameof(ContactSummaryDto.IsGroup))]
    [MapperIgnoreTarget(nameof(ContactSummaryDto.ParentGroupName))]
    [MapperIgnoreTarget(nameof(ContactSummaryDto.PrimaryEmail))]
    [MapperIgnoreTarget(nameof(ContactSummaryDto.PrimaryPhone))]
    [MapperIgnoreTarget(nameof(ContactSummaryDto.PrimaryAddress))]
    [MapperIgnoreTarget(nameof(ContactSummaryDto.TagNames))]
    [MapperIgnoreTarget(nameof(ContactSummaryDto.TagColors))]
    [MapperIgnoreTarget(nameof(ContactSummaryDto.ProfileImageUrl))]
    [MapperIgnoreTarget(nameof(ContactSummaryDto.GravatarUrl))]
    private static partial ContactSummaryDto MapContactToSummaryDto(Contact source);

    // CreateContactRequest -> Contact
    [MapperIgnoreTarget(nameof(Contact.Id))]
    [MapperIgnoreTarget(nameof(Contact.TenantId))]
    [MapperIgnoreTarget(nameof(Contact.CreatedAt))]
    [MapperIgnoreTarget(nameof(Contact.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Contact.HouseholdTenantId))]
    [MapperIgnoreTarget(nameof(Contact.LinkedUserId))]
    [MapperIgnoreTarget(nameof(Contact.UsesTenantAddress))]
    [MapperIgnoreTarget(nameof(Contact.CreatedByUserId))]
    [MapperIgnoreTarget(nameof(Contact.IsActive))]
    [MapperIgnoreTarget(nameof(Contact.ProfileImageFileName))]
    [MapperIgnoreTarget(nameof(Contact.ContactType))]
    [MapperIgnoreTarget(nameof(Contact.IsTenantHousehold))]
    [MapperIgnoreTarget(nameof(Contact.UsesGroupAddress))]
    [MapperIgnoreTarget(nameof(Contact.Website))]
    [MapperIgnoreTarget(nameof(Contact.BusinessCategory))]
    [MapperIgnoreTarget(nameof(Contact.DietaryNotes))]
    [MapperIgnoreTarget(nameof(Contact.LinkedUser))]
    [MapperIgnoreTarget(nameof(Contact.CreatedByUser))]
    [MapperIgnoreTarget(nameof(Contact.ParentContact))]
    [MapperIgnoreTarget(nameof(Contact.Members))]
    [MapperIgnoreTarget(nameof(Contact.Addresses))]
    [MapperIgnoreTarget(nameof(Contact.PhoneNumbers))]
    [MapperIgnoreTarget(nameof(Contact.EmailAddresses))]
    [MapperIgnoreTarget(nameof(Contact.SocialMedia))]
    [MapperIgnoreTarget(nameof(Contact.RelationshipsAsSource))]
    [MapperIgnoreTarget(nameof(Contact.RelationshipsAsTarget))]
    [MapperIgnoreTarget(nameof(Contact.Tags))]
    [MapperIgnoreTarget(nameof(Contact.SharedWithUsers))]
    [MapperIgnoreTarget(nameof(Contact.AuditLogs))]
    [MapperIgnoreTarget(nameof(Contact.Allergens))]
    [MapperIgnoreTarget(nameof(Contact.DietaryPreferences))]
    public static partial Contact FromCreateRequest(CreateContactRequest source);

    // UpdateContactRequest -> Contact
    [MapperIgnoreTarget(nameof(Contact.Id))]
    [MapperIgnoreTarget(nameof(Contact.TenantId))]
    [MapperIgnoreTarget(nameof(Contact.CreatedAt))]
    [MapperIgnoreTarget(nameof(Contact.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Contact.HouseholdTenantId))]
    [MapperIgnoreTarget(nameof(Contact.LinkedUserId))]
    [MapperIgnoreTarget(nameof(Contact.UsesTenantAddress))]
    [MapperIgnoreTarget(nameof(Contact.CreatedByUserId))]
    [MapperIgnoreTarget(nameof(Contact.ProfileImageFileName))]
    [MapperIgnoreTarget(nameof(Contact.ParentContactId))]
    [MapperIgnoreTarget(nameof(Contact.ContactType))]
    [MapperIgnoreTarget(nameof(Contact.IsTenantHousehold))]
    [MapperIgnoreTarget(nameof(Contact.UsesGroupAddress))]
    [MapperIgnoreTarget(nameof(Contact.Website))]
    [MapperIgnoreTarget(nameof(Contact.BusinessCategory))]
    [MapperIgnoreTarget(nameof(Contact.DietaryNotes))]
    [MapperIgnoreTarget(nameof(Contact.LinkedUser))]
    [MapperIgnoreTarget(nameof(Contact.CreatedByUser))]
    [MapperIgnoreTarget(nameof(Contact.ParentContact))]
    [MapperIgnoreTarget(nameof(Contact.Members))]
    [MapperIgnoreTarget(nameof(Contact.Addresses))]
    [MapperIgnoreTarget(nameof(Contact.PhoneNumbers))]
    [MapperIgnoreTarget(nameof(Contact.EmailAddresses))]
    [MapperIgnoreTarget(nameof(Contact.SocialMedia))]
    [MapperIgnoreTarget(nameof(Contact.RelationshipsAsSource))]
    [MapperIgnoreTarget(nameof(Contact.RelationshipsAsTarget))]
    [MapperIgnoreTarget(nameof(Contact.Tags))]
    [MapperIgnoreTarget(nameof(Contact.SharedWithUsers))]
    [MapperIgnoreTarget(nameof(Contact.AuditLogs))]
    [MapperIgnoreTarget(nameof(Contact.Allergens))]
    [MapperIgnoreTarget(nameof(Contact.DietaryPreferences))]
    public static partial Contact FromUpdateRequest(UpdateContactRequest source);

    // ContactAddress -> ContactAddressDto (computed: IsTenantAddress)
    [UserMapping(Default = true)]
    public static ContactAddressDto ToContactAddressDto(ContactAddress source)
    {
        var dto = MapContactAddressToDto(source);
        dto.IsTenantAddress = source.Contact != null && source.Contact.UsesTenantAddress && source.IsPrimary;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ContactAddressDto.IsTenantAddress))]
    private static partial ContactAddressDto MapContactAddressToDto(ContactAddress source);

    // ContactPhoneNumber -> ContactPhoneNumberDto
    public static partial ContactPhoneNumberDto ToContactPhoneNumberDto(ContactPhoneNumber source);

    // ContactEmailAddress -> ContactEmailAddressDto
    public static partial ContactEmailAddressDto ToContactEmailAddressDto(ContactEmailAddress source);

    // AddEmailRequest -> ContactEmailAddress
    [MapperIgnoreTarget(nameof(ContactEmailAddress.Id))]
    [MapperIgnoreTarget(nameof(ContactEmailAddress.TenantId))]
    [MapperIgnoreTarget(nameof(ContactEmailAddress.ContactId))]
    [MapperIgnoreTarget(nameof(ContactEmailAddress.NormalizedEmail))]
    [MapperIgnoreTarget(nameof(ContactEmailAddress.CreatedAt))]
    [MapperIgnoreTarget(nameof(ContactEmailAddress.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ContactEmailAddress.Contact))]
    public static partial ContactEmailAddress FromAddEmailRequest(AddEmailRequest source);

    // AddPhoneRequest -> ContactPhoneNumber
    [MapperIgnoreTarget(nameof(ContactPhoneNumber.Id))]
    [MapperIgnoreTarget(nameof(ContactPhoneNumber.TenantId))]
    [MapperIgnoreTarget(nameof(ContactPhoneNumber.ContactId))]
    [MapperIgnoreTarget(nameof(ContactPhoneNumber.NormalizedNumber))]
    [MapperIgnoreTarget(nameof(ContactPhoneNumber.CreatedAt))]
    [MapperIgnoreTarget(nameof(ContactPhoneNumber.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ContactPhoneNumber.Contact))]
    public static partial ContactPhoneNumber FromAddPhoneRequest(AddPhoneRequest source);

    // ContactSocialMedia -> ContactSocialMediaDto
    public static partial ContactSocialMediaDto ToContactSocialMediaDto(ContactSocialMedia source);

    // AddSocialMediaRequest -> ContactSocialMedia
    [MapperIgnoreTarget(nameof(ContactSocialMedia.Id))]
    [MapperIgnoreTarget(nameof(ContactSocialMedia.TenantId))]
    [MapperIgnoreTarget(nameof(ContactSocialMedia.ContactId))]
    [MapperIgnoreTarget(nameof(ContactSocialMedia.CreatedAt))]
    [MapperIgnoreTarget(nameof(ContactSocialMedia.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ContactSocialMedia.Contact))]
    public static partial ContactSocialMedia FromAddSocialMediaRequest(AddSocialMediaRequest source);

    // ContactRelationship -> ContactRelationshipDto (computed: TargetContactName, TargetIsUserLinked)
    [UserMapping(Default = true)]
    public static ContactRelationshipDto ToContactRelationshipDto(ContactRelationship source)
    {
        var dto = MapContactRelationshipToDto(source);
        dto.TargetContactName = source.TargetContact != null
            ? (!string.IsNullOrWhiteSpace(source.TargetContact.PreferredName)
                ? source.TargetContact.PreferredName
                : $"{source.TargetContact.FirstName} {source.TargetContact.LastName}".Trim())
            : string.Empty;
        dto.TargetIsUserLinked = source.TargetContact != null && source.TargetContact.LinkedUserId.HasValue;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ContactRelationshipDto.TargetContactName))]
    [MapperIgnoreTarget(nameof(ContactRelationshipDto.TargetIsUserLinked))]
    private static partial ContactRelationshipDto MapContactRelationshipToDto(ContactRelationship source);

    // ContactTag -> ContactTagDto (computed: ContactCount)
    public static ContactTagDto ToContactTagDto(ContactTag source)
    {
        var dto = MapContactTagToDto(source);
        dto.ContactCount = source.Contacts.Count;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ContactTagDto.ContactCount))]
    private static partial ContactTagDto MapContactTagToDto(ContactTag source);

    // CreateContactTagRequest -> ContactTag
    [MapperIgnoreTarget(nameof(ContactTag.Id))]
    [MapperIgnoreTarget(nameof(ContactTag.TenantId))]
    [MapperIgnoreTarget(nameof(ContactTag.CreatedAt))]
    [MapperIgnoreTarget(nameof(ContactTag.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ContactTag.Contacts))]
    public static partial ContactTag FromCreateContactTagRequest(CreateContactTagRequest source);

    // UpdateContactTagRequest -> ContactTag
    [MapperIgnoreTarget(nameof(ContactTag.Id))]
    [MapperIgnoreTarget(nameof(ContactTag.TenantId))]
    [MapperIgnoreTarget(nameof(ContactTag.CreatedAt))]
    [MapperIgnoreTarget(nameof(ContactTag.UpdatedAt))]
    [MapperIgnoreTarget(nameof(ContactTag.Contacts))]
    public static partial ContactTag FromUpdateContactTagRequest(UpdateContactTagRequest source);

    // ContactUserShare -> ContactUserShareDto (computed: SharedWithUserName)
    [UserMapping(Default = true)]
    public static ContactUserShareDto ToContactUserShareDto(ContactUserShare source)
    {
        var dto = MapContactUserShareToDto(source);
        dto.SharedWithUserName = source.SharedWithUser != null
            ? $"{source.SharedWithUser.FirstName} {source.SharedWithUser.LastName}"
            : string.Empty;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ContactUserShareDto.SharedWithUserName))]
    private static partial ContactUserShareDto MapContactUserShareToDto(ContactUserShare source);

    // ContactAuditLog -> ContactAuditLogDto (computed: UserName)
    public static ContactAuditLogDto ToContactAuditLogDto(ContactAuditLog source)
    {
        var dto = MapContactAuditLogToDto(source);
        dto.UserName = source.User != null
            ? $"{source.User.FirstName} {source.User.LastName}"
            : string.Empty;
        return dto;
    }

    [MapperIgnoreTarget(nameof(ContactAuditLogDto.UserName))]
    private static partial ContactAuditLogDto MapContactAuditLogToDto(ContactAuditLog source);
}
