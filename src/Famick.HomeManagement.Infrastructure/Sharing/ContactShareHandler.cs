using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.Sharing;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Sharing;

/// <summary>
/// Shares a contact's card: names, photo, phones, emails, addresses, social profiles and birthday.
/// Only a contact the sharing member can see is shared, and none of the sharer's notes, tags,
/// relationships or shares go with it. The copy is made through the contact service, as one entered by
/// hand is, so it is checked, normalised and audited the same way.
/// </summary>
public class ContactShareHandler : IShareableItemHandler
{
    private readonly HomeManagementDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IContactService _contacts;
    private readonly IFileStorageService _storage;

    public ContactShareHandler(
        HomeManagementDbContext context,
        ITenantProvider tenantProvider,
        IContactService contacts,
        IFileStorageService storage)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _contacts = contacts;
        _storage = storage;
    }

    public ShareableKind Kind => ShareableKind.Contact;

    /// <summary>A contact's preview is who it is and their photo: none of their details.</summary>
    public SharedItemPreview Preview(SharedItemSnapshot snapshot, string sharedByHouseholdName, bool hasImage) => new()
    {
        Kind = Kind,
        Name = snapshot.Name,
        SharedByHouseholdName = sharedByHouseholdName,
        HasImage = hasImage
    };

    public async Task<SharedItemSnapshot?> SnapshotAsync(Guid itemId, ShareSnapshotContext context, CancellationToken ct)
    {
        // The household's own card is shared by linking households, not as a contact.
        var contact = await _context.Contacts
            .AsNoTracking()
            .AsSplitQuery()
            .Where(ContactVisibility.VisibleTo(_tenantProvider.UserId))
            .Include(c => c.EmailAddresses)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.Addresses)
            .Include(c => c.SocialMedia)
            .FirstOrDefaultAsync(c => c.Id == itemId && c.IsActive && !c.IsTenantHousehold, ct);
        if (contact == null)
            return null;

        context.CountEntities(1 + contact.EmailAddresses.Count + contact.PhoneNumbers.Count
                             + contact.Addresses.Count + contact.SocialMedia.Count);

        var photoKey = string.IsNullOrEmpty(contact.ProfileImageFileName)
            ? null
            : await context.AddImageAsync(c => _storage.GetContactProfileImageStreamAsync(contact.Id, contact.ProfileImageFileName, c), ct);

        return new ContactSnapshot
        {
            SourceId = contact.Id,
            Name = contact.DisplayName,
            FirstName = contact.FirstName,
            MiddleName = contact.MiddleName,
            LastName = contact.LastName,
            PreferredName = contact.PreferredName,
            CompanyName = contact.CompanyName,
            Title = contact.Title,
            ContactType = contact.ContactType,
            BusinessCategory = contact.BusinessCategory,
            Gender = contact.Gender,
            BirthYear = contact.BirthYear,
            BirthMonth = contact.BirthMonth,
            BirthDay = contact.BirthDay,
            BirthDatePrecision = contact.BirthDatePrecision,
            Website = contact.Website,
            UseGravatar = contact.UseGravatar,
            PhotoKey = photoKey,
            Emails = contact.EmailAddresses.Select(e => new SharedEmail(e.Email, e.Tag, e.IsPrimary, e.Label)).ToList(),
            Phones = contact.PhoneNumbers.Select(p => new SharedPhone(p.PhoneNumber, p.Tag, p.IsPrimary)).ToList(),
            Addresses = contact.Addresses
                .Select(a => new SharedAddress(a.AddressId, a.Tag, a.IsPrimary, a.AddressLine2, a.AddressLine3, a.AddressLine4))
                .ToList(),
            SocialMedia = contact.SocialMedia.Select(s => new SharedSocialMedia(s.Service, s.Username, s.ProfileUrl)).ToList()
        };
    }

    public async Task<SharedCopy> ImportAsync(SharedItemSnapshot snapshot, ShareImportContext context, CancellationToken ct)
    {
        var shared = (ContactSnapshot)snapshot;
        var originKey = context.Origin.KeyFor(ShareableKind.Contact, shared.SourceId);
        var existing = await _context.Contacts
            .Where(c => c.IsActive && c.OriginKey == originKey)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct);
        if (existing != null)
            return new SharedCopy(existing.Id, shared.Name, AlreadyHad: true);

        var created = await _contacts.CreateAsync(new CreateContactRequest
        {
            FirstName = ShareText.Clean(shared.FirstName, 100),
            MiddleName = ShareText.Clean(shared.MiddleName, 100),
            LastName = ShareText.Clean(shared.LastName, 100),
            PreferredName = ShareText.Clean(shared.PreferredName, 100),
            CompanyName = ShareText.Clean(shared.CompanyName, 200),
            Title = ShareText.Clean(shared.Title, 100),
            Gender = shared.Gender,
            BirthYear = shared.BirthYear,
            BirthMonth = shared.BirthMonth,
            BirthDay = shared.BirthDay,
            BirthDatePrecision = shared.BirthDatePrecision,
            Visibility = ContactVisibilityLevel.TenantShared,
            UseGravatar = shared.UseGravatar
        }, ct);

        var contact = await _context.Contacts.FirstAsync(c => c.Id == created.Id, ct);
        contact.ContactType = shared.ContactType;
        contact.BusinessCategory = ShareText.Clean(shared.BusinessCategory, 100);
        contact.Website = ShareText.WebUrl(shared.Website, 500);
        contact.OriginKey = originKey;
        contact.SharedByHouseholdName = context.Origin.SharedByHouseholdName;
        await _context.SaveChangesAsync(ct);

        foreach (var email in shared.Emails)
            await _contacts.AddEmailAsync(contact.Id, new AddEmailRequest
            {
                Email = ShareText.Clean(email.Email, 255) ?? string.Empty,
                Tag = email.Tag,
                IsPrimary = email.IsPrimary,
                Label = ShareText.Clean(email.Label, 100)
            }, ct);

        foreach (var phone in shared.Phones)
            await _contacts.AddPhoneAsync(contact.Id, new AddPhoneRequest
            {
                PhoneNumber = ShareText.Clean(phone.PhoneNumber, 50) ?? string.Empty,
                Tag = phone.Tag,
                IsPrimary = phone.IsPrimary
            }, ct);

        foreach (var address in shared.Addresses)
            await _contacts.AddAddressAsync(contact.Id, new AddContactAddressRequest
            {
                AddressId = address.AddressId,
                Tag = address.Tag,
                IsPrimary = address.IsPrimary,
                AddressLine2 = ShareText.Clean(address.AddressLine2, 200),
                AddressLine3 = ShareText.Clean(address.AddressLine3, 200),
                AddressLine4 = ShareText.Clean(address.AddressLine4, 200)
            }, ct);

        foreach (var social in shared.SocialMedia)
            await _contacts.AddSocialMediaAsync(contact.Id, new AddSocialMediaRequest
            {
                Service = social.Service,
                Username = ShareText.Clean(social.Username, 200) ?? string.Empty,
                ProfileUrl = ShareText.WebUrl(social.ProfileUrl, 500)
            }, ct);

        if (context.Image(shared.PhotoKey) is { } photo)
        {
            using var stream = new MemoryStream(photo.Bytes, writable: false);
            await _contacts.UploadProfileImageAsync(contact.Id, stream, $"photo{photo.Extension}", ct);
            var stored = contact.ProfileImageFileName;
            if (stored != null)
                context.SavedFile(() => _storage.DeleteContactProfileImageAsync(contact.Id, stored));
        }

        return new SharedCopy(contact.Id, created.DisplayName, AlreadyHad: false);
    }
}
