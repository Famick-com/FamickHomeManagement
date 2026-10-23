using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// Writes one member's own data as a zip: what leaves with them, and their profile.
/// </summary>
/// <remarks>
/// <para>
/// Small next to the household archive, so it is built in memory from tracked-free reads rather
/// than streamed from a snapshot. The household writer dumps tables for a restore to put back;
/// this one is for a person, so it is shaped for reading and for other apps:
/// </para>
/// <list type="bullet">
/// <item><c>contacts.vcf</c> — the contacts, for a phone or any address book.</item>
/// <item><c>contacts.json</c> — the same contacts in full, with what vCard has no field for.</item>
/// <item><c>profile.json</c> — the account, its own contact card and dietary profile, and
/// notification preferences.</item>
/// <item><c>photos/</c> — the contacts' profile images as stored.</item>
/// </list>
/// <para>
/// Which contacts is <see cref="HouseholdMemberDeparture.LeavesWith"/>, the same rule the delete
/// applies, so the export carries exactly what the removal takes away.
/// </para>
/// </remarks>
internal sealed class MemberArchiveWriter(
    HomeManagementDbContext context,
    IFileStorageService storage,
    ILogger logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<MemberArchiveSummary> WriteAsync(User member, string householdName, Stream output, CancellationToken ct)
    {
        var tenantId = member.TenantId;

        var contacts = await LoadContactsAsync(tenantId, HouseholdMemberDeparture.LeavesWith(member.Id), ct);

        var card = member.ContactId is { } cardId
            ? (await LoadContactsAsync(tenantId, c => c.Id == cardId, ct)).FirstOrDefault()
            : null;

        var preferences = await context.NotificationPreferences
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == member.Id)
            .OrderBy(p => p.MessageType)
            .Select(p => new NotificationPreferenceExport(p.MessageType.ToString(), p.EmailEnabled, p.PushEnabled, p.InAppEnabled, p.SmsEnabled))
            .ToListAsync(ct);

        var vcf = await new ContactVcfWriter(context, storage, logger)
            .WriteAsync(tenantId, HouseholdMemberDeparture.LeavesWith(member.Id), ct);

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        var photos = new Dictionary<Guid, string>();
        var missingPhotos = 0;

        foreach (var contact in contacts.Where(c => !string.IsNullOrEmpty(c.ProfileImageFileName)))
        {
            var entryName = $"photos/{contact.Id:N}{Path.GetExtension(contact.ProfileImageFileName)}";

            try
            {
                await using var image = await storage.GetContactProfileImageStreamAsync(contact.Id, contact.ProfileImageFileName!, ct);
                if (image == null)
                {
                    missingPhotos++;
                    continue;
                }

                // Images are already compressed; deflating them again costs time and saves nothing.
                await using var entry = zip.CreateEntry(entryName, CompressionLevel.NoCompression).Open();
                await image.CopyToAsync(entry, ct);
                photos[contact.Id] = entryName;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One unreadable image is not a reason to withhold everything else from the person.
                logger.LogWarning(ex, "Profile image of contact {ContactId} could not be read for a member export", contact.Id);
                missingPhotos++;
            }
        }

        await WriteTextAsync(zip, "contacts.vcf", vcf.Content, ct);
        await WriteJsonAsync(zip, "contacts.json", contacts.Select(c => ContactExport.From(c, photos.GetValueOrDefault(c.Id))), ct);
        await WriteJsonAsync(zip, "profile.json", new ProfileExport(
            DateTime.UtcNow,
            householdName,
            new AccountExport(member.FirstName, member.LastName, member.Email, member.Username,
                member.PhoneNumber, member.PreferredLanguage, member.CreatedAt, member.LastLoginAt),
            card == null ? null : ContactExport.From(card, photo: null),
            preferences), ct);

        return new MemberArchiveSummary(contacts.Count, missingPhotos);
    }

    private Task<List<Contact>> LoadContactsAsync(
        Guid tenantId, System.Linq.Expressions.Expression<Func<Contact, bool>> scope, CancellationToken ct) =>
        context.Contacts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .Where(scope)
            .Include(c => c.Addresses).ThenInclude(a => a.Address)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.EmailAddresses)
            .Include(c => c.SocialMedia)
            .Include(c => c.Tags).ThenInclude(t => t.Tag)
            .Include(c => c.Allergens)
            .Include(c => c.DietaryPreferences)
            .AsSplitQuery()
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.CompanyName)
            .ToListAsync(ct);

    private static async Task WriteTextAsync(ZipArchive zip, string name, string content, CancellationToken ct)
    {
        await using var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        await entry.WriteAsync(Encoding.UTF8.GetBytes(content), ct);
    }

    private static async Task WriteJsonAsync<T>(ZipArchive zip, string name, T value, CancellationToken ct)
    {
        await using var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        await JsonSerializer.SerializeAsync(entry, value, Json, ct);
    }

    #region Archive shapes

    // Records rather than the entities, for two reasons: the entities carry navigation cycles and
    // internal columns (hashes, normalised forms, tenant ids) that mean nothing to the person, and
    // a written-down shape is one a change to the model cannot silently alter.

    private sealed record ProfileExport(
        DateTime ExportedAt,
        string Household,
        AccountExport Account,
        ContactExport? ContactCard,
        List<NotificationPreferenceExport> NotificationPreferences);

    private sealed record AccountExport(
        string FirstName, string LastName, string Email, string Username,
        string? PhoneNumber, string? PreferredLanguage, DateTime CreatedAt, DateTime? LastLoginAt);

    private sealed record NotificationPreferenceExport(string MessageType, bool Email, bool Push, bool InApp, bool Sms);

    private sealed record ContactExport(
        Guid Id,
        string DisplayName,
        string? FirstName, string? MiddleName, string? LastName, string? PreferredName,
        string? CompanyName, string? Title,
        string Gender,
        PartialDate? Birth, PartialDate? Death,
        string? Notes,
        bool IsGroup, string? ContactType, Guid? ParentContactId,
        string? Website, string? BusinessCategory,
        string Visibility, bool IsActive,
        string? DietaryNotes,
        string? ProfileImage,
        List<AddressExport> Addresses,
        List<LabelledValue> PhoneNumbers,
        List<LabelledValue> EmailAddresses,
        List<SocialMediaExport> SocialMedia,
        List<string> Tags,
        List<AllergenExport> Allergens,
        List<string> DietaryPreferences)
    {
        public static ContactExport From(Contact c, string? photo) => new(
            c.Id,
            c.DisplayName,
            c.FirstName, c.MiddleName, c.LastName, c.PreferredName,
            c.CompanyName, c.Title,
            c.Gender.ToString(),
            PartialDate.From(c.BirthYear, c.BirthMonth, c.BirthDay),
            PartialDate.From(c.DeathYear, c.DeathMonth, c.DeathDay),
            c.Notes,
            c.IsGroup, c.ContactType?.ToString(), c.ParentContactId,
            c.Website, c.BusinessCategory,
            c.Visibility.ToString(), c.IsActive,
            c.DietaryNotes,
            photo,
            c.Addresses.Select(a => new AddressExport(
                a.Tag.ToString(), a.Label, a.IsPrimary,
                a.Address?.AddressLine1, a.Address?.City, a.Address?.StateProvince,
                a.Address?.PostalCode, a.Address?.Country, a.Address?.FormattedAddress)).ToList(),
            c.PhoneNumbers.Select(p => new LabelledValue(p.PhoneNumber, p.Tag.ToString(), p.Label, p.IsPrimary)).ToList(),
            c.EmailAddresses.Select(e => new LabelledValue(e.Email, e.Tag.ToString(), e.Label, e.IsPrimary)).ToList(),
            c.SocialMedia.Select(s => new SocialMediaExport(s.Service.ToString(), s.Username, s.ProfileUrl)).ToList(),
            c.Tags.Where(t => t.Tag != null).Select(t => t.Tag!.Name).OrderBy(n => n).ToList(),
            c.Allergens.Select(a => new AllergenExport(a.AllergenType.ToString(), a.Severity.ToString())).ToList(),
            c.DietaryPreferences.Select(d => d.DietaryPreference.ToString()).ToList());
    }

    private sealed record PartialDate(int? Year, int? Month, int? Day)
    {
        public static PartialDate? From(int? year, int? month, int? day) =>
            year == null && month == null && day == null ? null : new PartialDate(year, month, day);
    }

    private sealed record AddressExport(
        string Tag, string? Label, bool IsPrimary,
        string? Line1, string? City, string? StateProvince, string? PostalCode, string? Country, string? Formatted);

    private sealed record LabelledValue(string Value, string Tag, string? Label, bool IsPrimary);

    private sealed record SocialMediaExport(string Service, string Username, string? ProfileUrl);

    private sealed record AllergenExport(string Allergen, string Severity);

    #endregion
}

/// <summary>What went into a member's archive.</summary>
internal sealed record MemberArchiveSummary(int ContactCount, int MissingPhotoCount);
