using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ContactFeedService : IContactFeedService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<ContactFeedService> _logger;

    public ContactFeedService(
        HomeManagementDbContext context,
        ILogger<ContactFeedService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<UserContactVcfTokenDto>> GetTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _context.UserContactVcfTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return tokens.Select(MapToDto).ToList();
    }

    public async Task<UserContactVcfTokenDto> CreateTokenAsync(
        CreateVcfTokenRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var token = new UserContactVcfToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = GenerateSecureToken(),
            IsRevoked = false,
            Label = request.Label
        };

        _context.UserContactVcfTokens.Add(token);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created VCF token {TokenId} for user {UserId}", token.Id, userId);

        return MapToDto(token);
    }

    public async Task RevokeTokenAsync(
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        var token = await _context.UserContactVcfTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);

        if (token == null)
            throw new EntityNotFoundException(nameof(UserContactVcfToken), tokenId);

        token.IsRevoked = true;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Revoked VCF token {TokenId}", tokenId);
    }

    public async Task DeleteTokenAsync(
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        var token = await _context.UserContactVcfTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);

        if (token == null)
            throw new EntityNotFoundException(nameof(UserContactVcfToken), tokenId);

        _context.UserContactVcfTokens.Remove(token);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted VCF token {TokenId}", tokenId);
    }

    public async Task<string?> GenerateVcfFeedAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        // Look up token WITHOUT tenant query filter (unauthenticated request)
        var tokenEntity = await _context.UserContactVcfTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsRevoked, cancellationToken);

        if (tokenEntity == null)
        {
            _logger.LogWarning("VCF feed requested with invalid or revoked token");
            return null;
        }

        var tenantId = tokenEntity.TenantId;

        // Query all active contacts for this tenant (non-group contacts)
        var contacts = await _context.Contacts
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.IsActive && c.ParentContactId != null)
            .Include(c => c.Addresses).ThenInclude(a => a.Address)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.EmailAddresses)
            .Include(c => c.SocialMedia)
            .Include(c => c.Tags).ThenInclude(tl => tl.Tag)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // Also query group contacts for X-ADDRESSBOOKSERVER export
        var groups = await _context.Contacts
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.IsActive && c.ParentContactId == null)
            .Include(c => c.Members)
            .Include(c => c.Addresses).ThenInclude(a => a.Address)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.EmailAddresses)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // Build lookup of group addresses for members that use their group's address
        var groupAddressLookup = groups.ToDictionary(
            g => g.Id,
            g => g.Addresses.ToList());

        // Fetch the tenant's address for contacts with UsesTenantAddress
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Include(t => t.Address)
            .FirstOrDefaultAsync(cancellationToken);

        var sb = new StringBuilder();

        // Export individual contacts
        foreach (var contact in contacts)
        {
            // Resolve addresses: if contact uses group address, inherit from parent group
            var resolvedAddresses = ResolveAddresses(contact, groupAddressLookup, tenant);
            AppendVCard(sb, contact, resolvedAddresses);
        }

        // Export groups with member references
        foreach (var group in groups)
        {
            AppendGroupVCard(sb, group);
        }

        _logger.LogDebug("Generated VCF feed with {ContactCount} contacts and {GroupCount} groups for tenant {TenantId}",
            contacts.Count, groups.Count, tenantId);

        return sb.ToString();
    }

    #region Private Methods

    /// <summary>
    /// Resolves the effective addresses for a contact, considering UsesGroupAddress and UsesTenantAddress flags.
    /// </summary>
    private static List<ContactAddress> ResolveAddresses(
        Contact contact,
        Dictionary<Guid, List<ContactAddress>> groupAddressLookup,
        Tenant? tenant)
    {
        // If the contact has its own addresses and doesn't use group address, use those
        if (!contact.UsesGroupAddress && !contact.UsesTenantAddress)
            return contact.Addresses.ToList();

        // If UsesGroupAddress, inherit from parent group
        if (contact.UsesGroupAddress && contact.ParentContactId.HasValue)
        {
            if (groupAddressLookup.TryGetValue(contact.ParentContactId.Value, out var groupAddresses) && groupAddresses.Count > 0)
                return groupAddresses;
        }

        // Fall back to tenant address (covers UsesTenantAddress flag, and also
        // UsesGroupAddress when parent group has no own addresses — the household
        // address is stored on the Tenant entity, not as ContactAddress records)
        if (tenant?.Address != null)
        {
            return new List<ContactAddress>
            {
                new()
                {
                    Tag = AddressTag.Home,
                    IsPrimary = true,
                    Address = tenant.Address
                }
            };
        }

        // Fallback to contact's own addresses (if any)
        return contact.Addresses.ToList();
    }

    private static void AppendVCard(StringBuilder sb, Contact contact, List<ContactAddress> resolvedAddresses)
    {
        sb.AppendLine("BEGIN:VCARD");
        sb.AppendLine("VERSION:3.0");

        // UID
        sb.AppendLine($"UID:{contact.Id}");

        // N (structured name)
        var lastName = EscapeVCard(contact.LastName ?? "");
        var firstName = EscapeVCard(contact.FirstName ?? "");
        var middleName = EscapeVCard(contact.MiddleName ?? "");
        sb.AppendLine($"N:{lastName};{firstName};{middleName};;");

        // FN (formatted name)
        sb.AppendLine($"FN:{EscapeVCard(contact.DisplayName)}");

        // NICKNAME
        if (!string.IsNullOrWhiteSpace(contact.PreferredName) && contact.PreferredName != contact.FirstName)
        {
            sb.AppendLine($"NICKNAME:{EscapeVCard(contact.PreferredName)}");
        }

        // ORG
        if (!string.IsNullOrWhiteSpace(contact.CompanyName))
        {
            sb.AppendLine($"ORG:{EscapeVCard(contact.CompanyName)}");
        }

        // TITLE
        if (!string.IsNullOrWhiteSpace(contact.Title))
        {
            sb.AppendLine($"TITLE:{EscapeVCard(contact.Title)}");
        }

        // TEL
        foreach (var phone in contact.PhoneNumbers)
        {
            var typeParam = phone.Tag switch
            {
                PhoneTag.Mobile => "CELL",
                PhoneTag.Home => "HOME",
                PhoneTag.Work => "WORK",
                PhoneTag.Fax => "FAX",
                _ => "OTHER"
            };
            sb.AppendLine($"TEL;TYPE={typeParam}:{phone.PhoneNumber}");
        }

        // EMAIL
        foreach (var email in contact.EmailAddresses)
        {
            var typeParam = email.Tag switch
            {
                EmailTag.Personal => "HOME",
                EmailTag.Work => "WORK",
                _ => "OTHER"
            };
            sb.AppendLine($"EMAIL;TYPE={typeParam}:{email.Email}");
        }

        // ADR (uses resolved addresses - may come from parent group or tenant home)
        AppendAddresses(sb, resolvedAddresses);

        // URL (Website)
        if (!string.IsNullOrWhiteSpace(contact.Website))
        {
            sb.AppendLine($"URL:{contact.Website}");
        }

        // BDAY
        AppendBirthday(sb, contact);

        // NOTE
        if (!string.IsNullOrWhiteSpace(contact.Notes))
        {
            sb.AppendLine($"NOTE:{EscapeVCard(contact.Notes)}");
        }

        // CATEGORIES (tags)
        var tagNames = contact.Tags
            .Where(t => t.Tag != null)
            .Select(t => EscapeVCard(t.Tag.Name))
            .ToList();
        if (tagNames.Count > 0)
        {
            sb.AppendLine($"CATEGORIES:{string.Join(",", tagNames)}");
        }

        // X-SOCIALPROFILE
        foreach (var social in contact.SocialMedia)
        {
            var serviceType = social.Service.ToString().ToUpperInvariant();
            var value = !string.IsNullOrWhiteSpace(social.ProfileUrl)
                ? social.ProfileUrl
                : social.Username;
            sb.AppendLine($"X-SOCIALPROFILE;TYPE={serviceType}:{value}");
        }

        // REV
        var rev = (contact.UpdatedAt ?? contact.CreatedAt).ToString("yyyy-MM-ddTHH:mm:ssZ");
        sb.AppendLine($"REV:{rev}");

        sb.AppendLine("END:VCARD");
    }

    private static void AppendGroupVCard(StringBuilder sb, Contact group)
    {
        sb.AppendLine("BEGIN:VCARD");
        sb.AppendLine("VERSION:3.0");

        sb.AppendLine($"UID:{group.Id}");

        // Group name as FN and N
        var groupName = EscapeVCard(group.DisplayName);
        sb.AppendLine($"FN:{groupName}");
        sb.AppendLine($"N:{groupName};;;;");

        // Mark as group (Apple convention)
        sb.AppendLine("X-ADDRESSBOOKSERVER-KIND:group");

        // Member references
        foreach (var member in group.Members.Where(m => m.IsActive))
        {
            sb.AppendLine($"X-ADDRESSBOOKSERVER-MEMBER:urn:uuid:{member.Id}");
        }

        // Group phone numbers
        foreach (var phone in group.PhoneNumbers)
        {
            var typeParam = phone.Tag switch
            {
                PhoneTag.Mobile => "CELL",
                PhoneTag.Home => "HOME",
                PhoneTag.Work => "WORK",
                PhoneTag.Fax => "FAX",
                _ => "OTHER"
            };
            sb.AppendLine($"TEL;TYPE={typeParam}:{phone.PhoneNumber}");
        }

        // Group email addresses
        foreach (var email in group.EmailAddresses)
        {
            var typeParam = email.Tag switch
            {
                EmailTag.Personal => "HOME",
                EmailTag.Work => "WORK",
                _ => "OTHER"
            };
            sb.AppendLine($"EMAIL;TYPE={typeParam}:{email.Email}");
        }

        // Group addresses
        AppendAddresses(sb, group.Addresses.ToList());

        // ORG for business groups
        if (group.ContactType == ContactType.Business && !string.IsNullOrWhiteSpace(group.CompanyName))
        {
            sb.AppendLine($"ORG:{EscapeVCard(group.CompanyName)}");
        }

        // Website
        if (!string.IsNullOrWhiteSpace(group.Website))
        {
            sb.AppendLine($"URL:{group.Website}");
        }

        var rev = (group.UpdatedAt ?? group.CreatedAt).ToString("yyyy-MM-ddTHH:mm:ssZ");
        sb.AppendLine($"REV:{rev}");

        sb.AppendLine("END:VCARD");
    }

    private static void AppendAddresses(StringBuilder sb, List<ContactAddress> addresses)
    {
        foreach (var contactAddr in addresses)
        {
            var addr = contactAddr.Address;
            if (addr == null) continue;

            var typeParam = contactAddr.Tag switch
            {
                AddressTag.Home => "HOME",
                AddressTag.Work => "WORK",
                _ => "OTHER"
            };

            // ADR: PO Box; Extended; Street; City; State; Postal; Country
            var street = EscapeVCard(addr.AddressLine1 ?? "");
            if (!string.IsNullOrWhiteSpace(addr.AddressLine2))
                street += "\\n" + EscapeVCard(addr.AddressLine2);

            sb.AppendLine($"ADR;TYPE={typeParam}:;;{street};{EscapeVCard(addr.City ?? "")};{EscapeVCard(addr.StateProvince ?? "")};{EscapeVCard(addr.PostalCode ?? "")};{EscapeVCard(addr.Country ?? "")}");
        }
    }

    private static void AppendBirthday(StringBuilder sb, Contact contact)
    {
        switch (contact.BirthDatePrecision)
        {
            case DatePrecision.Full when contact.BirthYear.HasValue && contact.BirthMonth.HasValue && contact.BirthDay.HasValue:
                sb.AppendLine($"BDAY:{contact.BirthYear:D4}-{contact.BirthMonth:D2}-{contact.BirthDay:D2}");
                break;
            case DatePrecision.YearMonth when contact.BirthYear.HasValue && contact.BirthMonth.HasValue:
                sb.AppendLine($"BDAY:{contact.BirthYear:D4}-{contact.BirthMonth:D2}");
                break;
            case DatePrecision.Year when contact.BirthYear.HasValue:
                sb.AppendLine($"BDAY:{contact.BirthYear:D4}");
                break;
        }
    }

    private static string EscapeVCard(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace(";", "\\;")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n");
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static UserContactVcfTokenDto MapToDto(UserContactVcfToken token)
    {
        return new UserContactVcfTokenDto
        {
            Id = token.Id,
            Token = token.Token,
            Label = token.Label,
            IsRevoked = token.IsRevoked,
            CreatedAt = token.CreatedAt
        };
    }

    #endregion
}
