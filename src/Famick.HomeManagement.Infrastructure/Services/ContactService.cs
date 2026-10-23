using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Famick.HomeManagement.Core.Validators.Common;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for managing contacts, relationships, and related data
/// </summary>
public partial class ContactService : IContactService
{
    private readonly HomeManagementDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFileUrlService _fileUrlService;
    private readonly IAddressHasher _addressHasher;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        HomeManagementDbContext context,
        ITenantProvider tenantProvider,
        IFileStorageService fileStorageService,
        IFileUrlService fileUrlService,
        IAddressHasher addressHasher,
        ILogger<ContactService> logger)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _fileStorageService = fileStorageService;
        _fileUrlService = fileUrlService;
        _addressHasher = addressHasher;
        _logger = logger;
    }

    #region Contact CRUD

    /// <inheritdoc />
    public async Task<ContactDto> CreateAsync(CreateContactRequest request, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Creating contact: {FirstName} {LastName}", request.FirstName, request.LastName);

        var contact = ContactMapper.FromCreateRequest(request);
        contact.Id = Guid.NewGuid();
        contact.CreatedByUserId = userId;
        contact.IsActive = true;

        // Default ParentContactId to tenant household if not specified
        if (request.ParentContactId.HasValue)
        {
            var parentGroup = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == request.ParentContactId.Value && c.ParentContactId == null, ct)
                ?? throw new EntityNotFoundException("Contact group", request.ParentContactId.Value);
            contact.ParentContactId = request.ParentContactId.Value;
        }
        else
        {
            var tenantHousehold = await _context.Contacts
                .FirstOrDefaultAsync(c => c.IsTenantHousehold, ct);
            if (tenantHousehold != null)
            {
                contact.ParentContactId = tenantHousehold.Id;
            }
        }

        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync(ct);

        // Add initial tags if provided
        if (request.TagIds?.Count > 0)
        {
            await SetContactTagsAsync(contact.Id, request.TagIds, ct);
        }

        // Log creation
        await LogAuditAsync(contact.Id, ContactAuditAction.Created, null, contact, "Contact created", ct);

        _logger.LogInformation("Created contact: {Id}", contact.Id);

        return await GetByIdAsync(contact.Id, ct) ?? throw new InvalidOperationException("Failed to retrieve created contact");
    }

    /// <inheritdoc />
    public async Task<ContactDto> CreateContactForUserAsync(User user, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating contact for user: {UserId}", user.Id);

        // Get tenant's address
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);

        // Default to tenant household group
        var tenantHousehold = await _context.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == user.TenantId && c.IsTenantHousehold, ct);

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            LinkedUserId = user.Id,
            UsesTenantAddress = true,
            CreatedByUserId = user.Id,
            Visibility = ContactVisibilityLevel.TenantShared,
            IsActive = true,
            ParentContactId = tenantHousehold?.Id
        };

        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync(ct);

        // Add user's email as primary email address
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var emailAddress = new ContactEmailAddress
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                ContactId = contact.Id,
                Email = user.Email,
                NormalizedEmail = user.Email.Trim().ToLowerInvariant(),
                Tag = EmailTag.Personal,
                IsPrimary = true
            };
            _context.ContactEmailAddresses.Add(emailAddress);
            await _context.SaveChangesAsync(ct);
        }

        // Add tenant's address as home address
        if (tenant?.AddressId != null)
        {
            var contactAddress = new ContactAddress
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                ContactId = contact.Id,
                AddressId = tenant.AddressId.Value,
                AddressLine2 = tenant.AddressLine2,
                AddressLine3 = tenant.AddressLine3,
                AddressLine4 = tenant.AddressLine4,
                Tag = AddressTag.Home,
                IsPrimary = true
            };
            _context.ContactAddresses.Add(contactAddress);
        }

        // Update user with contact reference
        user.ContactId = contact.Id;
        await _context.SaveChangesAsync(ct);

        // Log creation
        await LogAuditAsync(contact.Id, ContactAuditAction.Created, null, contact, "Contact created for user registration", ct);

        _logger.LogInformation("Created contact {ContactId} for user {UserId}", contact.Id, user.Id);

        return await GetByIdAsync(contact.Id, ct) ?? throw new InvalidOperationException("Failed to retrieve created contact");
    }

    /// <inheritdoc />
    public async Task<ContactDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var contact = await VisibleContacts()
            .Include(c => c.ParentContact)
            .Include(c => c.Members)
            .Include(c => c.LinkedUser)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Addresses)
                .ThenInclude(a => a.Address)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.EmailAddresses)
            .Include(c => c.SocialMedia)
            .Include(c => c.RelationshipsAsSource)
                .ThenInclude(r => r.TargetContact)
            .Include(c => c.Tags)
                .ThenInclude(t => t.Tag)
            .Include(c => c.SharedWithUsers)
                .ThenInclude(s => s.SharedWithUser)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (contact == null) return null;

        // Members and relationship targets are contacts in their own right, each with its own
        // visibility. Filtered on the DTO, not the tracked collections: removing an entity from
        // a tracked navigation reads to EF as severing the relationship.
        var linkedIds = contact.Members.Select(m => m.Id)
            .Concat(contact.RelationshipsAsSource.Select(r => r.TargetContactId))
            .Distinct()
            .ToList();
        var visibleLinkedIds = linkedIds.Count == 0
            ? new HashSet<Guid>()
            : (await VisibleContacts().Where(c => linkedIds.Contains(c.Id)).Select(c => c.Id).ToListAsync(ct)).ToHashSet();

        var dto = ContactMapper.ToDto(contact);
        dto.Relationships = dto.Relationships.Where(r => visibleLinkedIds.Contains(r.TargetContactId)).ToList();
        await ApplyCallerRightsAsync(dto, ct);

        // Set IsHouseholdMember — true if HouseholdTenantId is set (wizard-created)
        // OR if the parent group is a Household-type contact group
        dto.IsHouseholdMember = contact.HouseholdTenantId.HasValue
            || (contact.ParentContact != null && contact.ParentContact.ContactType == Domain.Enums.ContactType.Household);

        // Resolve inherited addresses for contacts that use group or tenant address
        if (!contact.IsGroup && (contact.UsesGroupAddress || contact.UsesTenantAddress))
        {
            var resolved = false;

            // Try parent group's addresses first
            if (contact.UsesGroupAddress && contact.ParentContactId.HasValue)
            {
                var parentAddresses = await _context.ContactAddresses
                    .Where(ca => ca.ContactId == contact.ParentContactId.Value)
                    .Include(ca => ca.Address)
                    .ToListAsync(ct);

                if (parentAddresses.Count > 0)
                {
                    dto.Addresses = parentAddresses.Select(ContactMapper.ToContactAddressDto).ToList();
                    resolved = true;
                }
            }

            // Fall back to tenant address (covers UsesTenantAddress flag, and also
            // UsesGroupAddress when parent group has no own addresses — the household
            // address is stored on the Tenant entity, not as ContactAddress records)
            if (!resolved)
            {
                var tenant = await _context.Tenants
                    .Where(t => t.Id == contact.TenantId)
                    .Include(t => t.Address)
                    .FirstOrDefaultAsync(ct);

                if (tenant?.Address != null)
                {
                    dto.Addresses = new List<ContactAddressDto>
                    {
                        new()
                        {
                            Id = Guid.Empty,
                            ContactId = contact.Id,
                            AddressId = tenant.Address.Id,
                            Address = TenantMapper.ToHouseholdAddressDto(tenant),
                            Tag = AddressTag.Home,
                            IsPrimary = true,
                            IsTenantAddress = true
                        }
                    };
                }
            }

        }

        // Populate members list for group contacts
        if (contact.IsGroup && contact.Members.Count > 0)
        {
            dto.Members = contact.Members
                .Where(m => visibleLinkedIds.Contains(m.Id))
                .Select(ContactMapper.ToSummaryDto)
                .ToList();

            // Set profile image URLs for each member (mapper ignores these fields)
            foreach (var member in dto.Members)
            {
                var memberEntity = contact.Members.First(m => m.Id == member.Id);
                if (!string.IsNullOrEmpty(memberEntity.ProfileImageFileName))
                {
                    member.ProfileImageUrl = _fileUrlService.GetContactProfileImageUrl(memberEntity.Id, memberEntity.TenantId, memberEntity.ProfileImageFileName);
                }
                if (memberEntity.UseGravatar && !string.IsNullOrEmpty(member.PrimaryEmail))
                {
                    member.GravatarUrl = GravatarHelper.GetGravatarUrl(member.PrimaryEmail);
                }
            }
        }

        // Set profile image URL if exists (with signed token for browser access)
        if (!string.IsNullOrEmpty(contact.ProfileImageFileName))
        {
            dto.ProfileImageUrl = _fileUrlService.GetContactProfileImageUrl(contact.Id, contact.TenantId, contact.ProfileImageFileName);
        }

        // Set Gravatar URL if UseGravatar is enabled and primary email exists
        if (contact.UseGravatar && !string.IsNullOrEmpty(dto.PrimaryEmail))
        {
            dto.GravatarUrl = GravatarHelper.GetGravatarUrl(dto.PrimaryEmail);
        }

        return dto;
    }

    /// <inheritdoc />
    public async Task<ContactDto?> GetByLinkedUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var contact = await VisibleContacts()
            .Include(c => c.LinkedUser)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Addresses)
                .ThenInclude(a => a.Address)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.EmailAddresses)
            .Include(c => c.SocialMedia)
            .Include(c => c.Tags)
                .ThenInclude(t => t.Tag)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.LinkedUserId == userId, ct);

        if (contact == null) return null;

        var dto = ContactMapper.ToDto(contact);
        await ApplyCallerRightsAsync(dto, ct);

        // Set profile image URL if exists (with signed token for browser access)
        if (!string.IsNullOrEmpty(contact.ProfileImageFileName))
        {
            dto.ProfileImageUrl = _fileUrlService.GetContactProfileImageUrl(contact.Id, contact.TenantId, contact.ProfileImageFileName);
        }

        // Set Gravatar URL if UseGravatar is enabled and primary email exists
        if (contact.UseGravatar && !string.IsNullOrEmpty(dto.PrimaryEmail))
        {
            dto.GravatarUrl = GravatarHelper.GetGravatarUrl(dto.PrimaryEmail);
        }

        return dto;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ContactSummaryDto>> ListAsync(ContactFilterRequest filter, CancellationToken ct = default)
    {
        filter.Page = QueryLimits.Page(filter.Page);
        filter.PageSize = QueryLimits.PageSize(filter.PageSize, 25);

        var query = VisibleContacts()
            .Include(c => c.ParentContact)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.EmailAddresses)
            .Include(c => c.Addresses)
                .ThenInclude(a => a.Address)
            .Include(c => c.Tags)
                .ThenInclude(t => t.Tag)
            .AsQueryable();

        // Group filters
        if (filter.IsGroup.HasValue)
        {
            query = filter.IsGroup.Value
                ? query.Where(c => c.ParentContactId == null)
                : query.Where(c => c.ParentContactId != null);
        }

        if (filter.ContactType.HasValue)
        {
            query = query.Where(c => c.ContactType == filter.ContactType.Value);
        }

        if (filter.ParentContactId.HasValue)
        {
            query = query.Where(c => c.ParentContactId == filter.ParentContactId.Value);
        }

        // Apply filters
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(c =>
                (c.FirstName != null && c.FirstName.ToLower().Contains(searchTerm)) ||
                (c.LastName != null && c.LastName.ToLower().Contains(searchTerm)) ||
                (c.PreferredName != null && c.PreferredName.ToLower().Contains(searchTerm)) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(searchTerm)) ||
                c.EmailAddresses.Any(e => e.Email.ToLower().Contains(searchTerm)) ||
                c.PhoneNumbers.Any(p => p.PhoneNumber.Contains(searchTerm) ||
                    (p.NormalizedNumber != null && p.NormalizedNumber.Contains(searchTerm))));
        }

        if (filter.Visibility.HasValue)
        {
            query = query.Where(c => c.Visibility == filter.Visibility.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == filter.IsActive.Value);
        }

        if (filter.IsUserLinked.HasValue)
        {
            query = filter.IsUserLinked.Value
                ? query.Where(c => c.LinkedUserId.HasValue)
                : query.Where(c => !c.LinkedUserId.HasValue);
        }

        if (filter.TagIds?.Count > 0)
        {
            query = query.Where(c => c.Tags.Any(t => filter.TagIds.Contains(t.TagId)));
        }

        // Apply sorting
        query = filter.SortBy?.ToLower() switch
        {
            "firstname" => filter.SortDescending
                ? query.OrderByDescending(c => c.FirstName)
                : query.OrderBy(c => c.FirstName),
            "email" => filter.SortDescending
                ? query.OrderByDescending(c => c.EmailAddresses.Where(e => e.IsPrimary).Select(e => e.Email).FirstOrDefault())
                : query.OrderBy(c => c.EmailAddresses.Where(e => e.IsPrimary).Select(e => e.Email).FirstOrDefault()),
            "createdat" => filter.SortDescending
                ? query.OrderByDescending(c => c.CreatedAt)
                : query.OrderBy(c => c.CreatedAt),
            _ => filter.SortDescending
                ? query.OrderByDescending(c => c.LastName).ThenByDescending(c => c.FirstName)
                : query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
        };

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip(QueryLimits.Skip(filter.Page, filter.PageSize))
            .Take(filter.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(ContactMapper.ToSummaryDto).ToList();

        // Set profile image URLs and Gravatar URLs for contacts
        for (int i = 0; i < items.Count; i++)
        {
            if (!string.IsNullOrEmpty(items[i].ProfileImageFileName))
            {
                dtos[i].ProfileImageUrl = _fileUrlService.GetContactProfileImageUrl(items[i].Id, items[i].TenantId, items[i].ProfileImageFileName);
            }

            // Set Gravatar URL if UseGravatar is enabled and primary email exists
            if (items[i].UseGravatar && !string.IsNullOrEmpty(dtos[i].PrimaryEmail))
            {
                dtos[i].GravatarUrl = GravatarHelper.GetGravatarUrl(dtos[i].PrimaryEmail);
            }
        }

        return new PagedResult<ContactSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<ContactDto> UpdateAsync(Guid id, UpdateContactRequest request, CancellationToken ct = default)
    {
        var contact = await FindEditableContactAsync(id, ct);

        // Somebody it was shared with may edit the details, not decide who else sees them.
        if (request.Visibility is { } visibility && visibility != contact.Visibility
            && contact.CreatedByUserId != _tenantProvider.UserId)
            throw new BusinessRuleViolationException("ContactOwnerOnly",
                "Only the person who created this contact can change who sees it.");

        var oldValues = JsonSerializer.Serialize(new
        {
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
            contact.Notes,
            contact.Visibility,
            contact.IsActive
        });

        contact.FirstName = request.FirstName;
        contact.MiddleName = request.MiddleName;
        contact.LastName = request.LastName;
        contact.PreferredName = request.PreferredName;
        contact.CompanyName = request.CompanyName;
        contact.Title = request.Title;
        contact.Gender = request.Gender;
        contact.BirthYear = request.BirthYear;
        contact.BirthMonth = request.BirthMonth;
        contact.BirthDay = request.BirthDay;
        contact.BirthDatePrecision = request.BirthDatePrecision;
        contact.DeathYear = request.DeathYear;
        contact.DeathMonth = request.DeathMonth;
        contact.DeathDay = request.DeathDay;
        contact.DeathDatePrecision = request.DeathDatePrecision;
        contact.Notes = request.Notes;
        contact.Visibility = request.Visibility ?? contact.Visibility;
        contact.IsActive = request.IsActive ?? contact.IsActive;
        contact.UseGravatar = request.UseGravatar;

        // Sync name back to linked user record if this contact belongs to a user
        var linkedUser = await _context.Users
            .FirstOrDefaultAsync(u => u.ContactId == id, ct);
        if (linkedUser != null)
        {
            linkedUser.FirstName = contact.FirstName ?? string.Empty;
            linkedUser.LastName = contact.LastName ?? string.Empty;
            linkedUser.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new
        {
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
            contact.Notes,
            contact.Visibility,
            contact.IsActive
        });

        await LogAuditAsync(id, ContactAuditAction.Updated, oldValues, newValues, "Contact updated", ct);

        _logger.LogInformation("Updated contact: {Id}", id);

        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Failed to retrieve updated contact");
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await FindEditableContactAsync(id, ct);

        var contact = await _context.Contacts
            .Include(c => c.LinkedUser)
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), id);

        // Cannot delete tenant household via this method
        if (contact.IsTenantHousehold)
        {
            throw new InvalidOperationException("Cannot delete the tenant household group");
        }

        // If this is a group, move members to tenant household first
        if (contact.IsGroup && contact.Members.Count > 0)
        {
            var tenantHousehold = await _context.Contacts
                .FirstOrDefaultAsync(c => c.IsTenantHousehold && c.TenantId == contact.TenantId, ct);

            if (tenantHousehold != null)
            {
                foreach (var member in contact.Members)
                {
                    member.ParentContactId = tenantHousehold.Id;
                }
                await _context.SaveChangesAsync(ct);
            }
        }

        // Remove user link if exists
        if (contact.LinkedUser != null)
        {
            contact.LinkedUser.ContactId = null;
        }

        _context.Contacts.Remove(contact);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted contact: {Id}", id);
    }

    /// <inheritdoc />
    public async Task<List<ContactSummaryDto>> SearchAsync(string searchTerm, int limit = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<ContactSummaryDto>();

        var term = searchTerm.ToLower();
        var contacts = await VisibleContacts()
            .Include(c => c.PhoneNumbers)
            .Include(c => c.EmailAddresses)
            .Include(c => c.Tags)
                .ThenInclude(t => t.Tag)
            .Where(c => c.IsActive &&
                ((c.FirstName != null && c.FirstName.ToLower().Contains(term)) ||
                 (c.LastName != null && c.LastName.ToLower().Contains(term)) ||
                 (c.PreferredName != null && c.PreferredName.ToLower().Contains(term)) ||
                 (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)) ||
                 c.EmailAddresses.Any(e => e.Email.ToLower().Contains(term))))
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Take(limit)
            .ToListAsync(ct);

        var dtos = contacts.Select(ContactMapper.ToSummaryDto).ToList();

        // Set profile image URLs and Gravatar URLs for contacts
        for (int i = 0; i < contacts.Count; i++)
        {
            if (!string.IsNullOrEmpty(contacts[i].ProfileImageFileName))
            {
                dtos[i].ProfileImageUrl = _fileUrlService.GetContactProfileImageUrl(contacts[i].Id, contacts[i].TenantId, contacts[i].ProfileImageFileName);
            }

            // Set Gravatar URL if UseGravatar is enabled and primary email exists
            if (contacts[i].UseGravatar && !string.IsNullOrEmpty(dtos[i].PrimaryEmail))
            {
                dtos[i].GravatarUrl = GravatarHelper.GetGravatarUrl(dtos[i].PrimaryEmail);
            }
        }

        return dtos;
    }

    #endregion

    #region Address Management

    /// <inheritdoc />
    public async Task<ContactAddressDto> AddAddressAsync(Guid contactId, AddContactAddressRequest request, CancellationToken ct = default)
    {
        var contact = await FindEditableContactAsync(contactId, ct);

        Guid addressId;

        if (request.AddressId.HasValue)
        {
            // Link an existing row. Rows are shared and never edited, so any row may be
            // linked; the fields are ignored (issue #119).
            addressId = request.AddressId.Value;
            if (!await _context.Addresses.AnyAsync(a => a.Id == addressId, ct))
                throw new EntityNotFoundException(nameof(Address), addressId);
        }
        else
        {
            addressId = (await FindOrCreateAddressAsync(request, ct)).Id;
        }

        // If setting as primary, clear other primaries
        if (request.IsPrimary)
        {
            var existingPrimaries = await _context.ContactAddresses
                .Where(ca => ca.ContactId == contactId && ca.IsPrimary)
                .ToListAsync(ct);
            foreach (var existing in existingPrimaries)
            {
                existing.IsPrimary = false;
            }
        }

        // The (ContactId, AddressId) pair has a unique index. Before
        // libpostal canonicalization, two textually-different addresses
        // produced two Address rows so the constraint never fired in
        // practice. Now that libpostal collapses "123 Main St" and
        // "123 main street" into a single Address row, a user adding both
        // to the same contact would re-target the same AddressId and
        // trip the constraint. Treat that as an UPDATE of the existing
        // link (refresh Tag, IsPrimary, AddressLine2) rather than an
        // insert that crashes.
        var existingLink = await _context.ContactAddresses
            .FirstOrDefaultAsync(ca => ca.ContactId == contactId && ca.AddressId == addressId, ct);

        ContactAddress contactAddress;
        if (existingLink != null)
        {
            existingLink.Tag = request.Tag;
            existingLink.IsPrimary = request.IsPrimary;
            SetUnitLines(existingLink, request);
            contactAddress = existingLink;
        }
        else
        {
            contactAddress = new ContactAddress
            {
                Id = Guid.NewGuid(),
                TenantId = contact.TenantId,
                ContactId = contactId,
                AddressId = addressId,
                Tag = request.Tag,
                IsPrimary = request.IsPrimary
            };
            SetUnitLines(contactAddress, request);
            _context.ContactAddresses.Add(contactAddress);
        }
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.AddressAdded, null,
            JsonSerializer.Serialize(new { AddressId = addressId, Tag = request.Tag.ToString() }),
            "Address added", ct);

        // Reload with address included
        var result = await _context.ContactAddresses
            .Include(ca => ca.Address)
            .FirstAsync(ca => ca.Id == contactAddress.Id, ct);

        return ContactMapper.ToContactAddressDto(result);
    }

    /// <inheritdoc />
    public async Task RemoveAddressAsync(Guid contactId, Guid contactAddressId, CancellationToken ct = default)
    {
        await FindEditableContactAsync(contactId, ct);

        var contactAddress = await _context.ContactAddresses
            .FirstOrDefaultAsync(ca => ca.Id == contactAddressId && ca.ContactId == contactId, ct)
            ?? throw new EntityNotFoundException(nameof(ContactAddress), contactAddressId);

        _context.ContactAddresses.Remove(contactAddress);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.AddressRemoved, null,
            JsonSerializer.Serialize(new { AddressId = contactAddress.AddressId }),
            "Address removed", ct);
    }

    /// <inheritdoc />
    public async Task SetPrimaryAddressAsync(Guid contactId, Guid contactAddressId, CancellationToken ct = default)
    {
        await FindEditableContactAsync(contactId, ct);

        var addresses = await _context.ContactAddresses
            .Where(ca => ca.ContactId == contactId)
            .ToListAsync(ct);

        var targetAddress = addresses.FirstOrDefault(a => a.Id == contactAddressId)
            ?? throw new EntityNotFoundException(nameof(ContactAddress), contactAddressId);

        foreach (var address in addresses)
        {
            address.IsPrimary = address.Id == contactAddressId;
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<ContactAddressDto> UpdateAddressAsync(Guid contactId, Guid contactAddressId, AddContactAddressRequest request, CancellationToken ct = default)
    {
        await FindEditableContactAsync(contactId, ct);

        var contactAddress = await _context.ContactAddresses
            .FirstOrDefaultAsync(ca => ca.Id == contactAddressId && ca.ContactId == contactId, ct)
            ?? throw new EntityNotFoundException(nameof(ContactAddress), contactAddressId);

        // Never edit the linked Address row: every household that entered the address shares
        // it. Re-point this link at the row for the new address instead (issue #119).
        // Request.AddressId is ignored here, as it always has been; the fields are what the
        // edit form submits.
        var addressId = (await FindOrCreateAddressAsync(request, ct)).Id;
        if (addressId != contactAddress.AddressId)
        {
            // (ContactId, AddressId) is unique, so the contact cannot hold the same address twice.
            if (await _context.ContactAddresses.AnyAsync(
                    ca => ca.ContactId == contactId && ca.AddressId == addressId && ca.Id != contactAddressId, ct))
            {
                throw new DuplicateEntityException(nameof(ContactAddress), nameof(ContactAddress.AddressId), addressId.ToString());
            }

            contactAddress.AddressId = addressId;
        }

        // Update the contact address entity
        contactAddress.Tag = request.Tag;
        SetUnitLines(contactAddress, request);

        // Handle primary flag
        if (request.IsPrimary && !contactAddress.IsPrimary)
        {
            var existingPrimaries = await _context.ContactAddresses
                .Where(ca => ca.ContactId == contactId && ca.IsPrimary && ca.Id != contactAddressId)
                .ToListAsync(ct);
            foreach (var existing in existingPrimaries)
            {
                existing.IsPrimary = false;
            }
            contactAddress.IsPrimary = true;
        }
        else if (!request.IsPrimary && contactAddress.IsPrimary)
        {
            // Unset primary if requested, but only if there are other addresses
            var otherAddresses = await _context.ContactAddresses
                .Where(ca => ca.ContactId == contactId && ca.Id != contactAddressId)
                .ToListAsync(ct);
            if (otherAddresses.Count > 0)
            {
                contactAddress.IsPrimary = false;
                // Set the first other address as primary
                otherAddresses.First().IsPrimary = true;
            }
        }

        await _context.SaveChangesAsync(ct);

        // Reload to get updated data
        contactAddress = await _context.ContactAddresses
            .Include(ca => ca.Address)
            .FirstAsync(ca => ca.Id == contactAddressId, ct);

        return ContactMapper.ToContactAddressDto(contactAddress);
    }

    /// <inheritdoc />
    public async Task<List<ContactAddressDto>> GetAddressesAsync(Guid contactId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(contactId, ct);

        var addresses = await _context.ContactAddresses
            .Include(ca => ca.Address)
            .Where(ca => ca.ContactId == contactId)
            .OrderByDescending(ca => ca.IsPrimary)
            .ThenBy(ca => ca.CreatedAt)
            .ToListAsync(ct);

        return addresses.Select(ContactMapper.ToContactAddressDto).ToList();
    }

    /// <summary>
    /// The shared Address row for a contact address entered as fields. Only the components
    /// that identify the address are used: coordinates, place ids and formatted text from a
    /// client never reach the shared row (issue #119). A provider result the client picked
    /// was recorded when the normalize call returned it, so its row is found by its fields.
    /// </summary>
    private Task<Address> FindOrCreateAddressAsync(AddContactAddressRequest request, CancellationToken ct) =>
        _context.FindOrCreateAsync(_addressHasher, new AddressIdentity(
            request.AddressLine1, request.City, request.StateProvince, request.PostalCode, request.Country), ct);

    /// <summary>Lines that belong to this contact rather than to the shared row.</summary>
    private static void SetUnitLines(ContactAddress link, AddContactAddressRequest request)
    {
        link.AddressLine2 = NullIfBlank(request.AddressLine2);
        link.AddressLine3 = NullIfBlank(request.AddressLine3);
        link.AddressLine4 = NullIfBlank(request.AddressLine4);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    #endregion

    #region Phone Management

    /// <inheritdoc />
    public async Task<ContactPhoneNumberDto> AddPhoneAsync(Guid contactId, AddPhoneRequest request, CancellationToken ct = default)
    {
        var contact = await FindEditableContactAsync(contactId, ct);

        // If setting as primary, clear other primaries
        if (request.IsPrimary)
        {
            var existingPrimaries = await _context.ContactPhoneNumbers
                .Where(p => p.ContactId == contactId && p.IsPrimary)
                .ToListAsync(ct);
            foreach (var existing in existingPrimaries)
            {
                existing.IsPrimary = false;
            }
        }

        var phone = new ContactPhoneNumber
        {
            Id = Guid.NewGuid(),
            TenantId = contact.TenantId,
            ContactId = contactId,
            PhoneNumber = request.PhoneNumber,
            NormalizedNumber = NormalizePhoneNumber(request.PhoneNumber),
            Tag = request.Tag,
            IsPrimary = request.IsPrimary
        };

        _context.ContactPhoneNumbers.Add(phone);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.PhoneAdded, null,
            JsonSerializer.Serialize(new { PhoneNumber = request.PhoneNumber, Tag = request.Tag.ToString() }),
            "Phone number added", ct);

        return ContactMapper.ToContactPhoneNumberDto(phone);
    }

    /// <inheritdoc />
    public async Task<ContactPhoneNumberDto> UpdatePhoneAsync(Guid phoneId, AddPhoneRequest request, CancellationToken ct = default)
    {
        var phone = await _context.ContactPhoneNumbers.FindAsync(new object[] { phoneId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactPhoneNumber), phoneId);
        await FindEditableContactAsync(phone.ContactId, ct, (nameof(ContactPhoneNumber), phoneId));

        phone.PhoneNumber = request.PhoneNumber;
        phone.NormalizedNumber = NormalizePhoneNumber(request.PhoneNumber);
        phone.Tag = request.Tag;

        // Handle primary change
        if (request.IsPrimary && !phone.IsPrimary)
        {
            var existingPrimaries = await _context.ContactPhoneNumbers
                .Where(p => p.ContactId == phone.ContactId && p.IsPrimary && p.Id != phoneId)
                .ToListAsync(ct);
            foreach (var existing in existingPrimaries)
            {
                existing.IsPrimary = false;
            }
        }
        phone.IsPrimary = request.IsPrimary;

        await _context.SaveChangesAsync(ct);

        return ContactMapper.ToContactPhoneNumberDto(phone);
    }

    /// <inheritdoc />
    public async Task RemovePhoneAsync(Guid phoneId, CancellationToken ct = default)
    {
        var phone = await _context.ContactPhoneNumbers.FindAsync(new object[] { phoneId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactPhoneNumber), phoneId);
        await FindEditableContactAsync(phone.ContactId, ct, (nameof(ContactPhoneNumber), phoneId));

        var contactId = phone.ContactId;
        _context.ContactPhoneNumbers.Remove(phone);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.PhoneRemoved, null, null, "Phone number removed", ct);
    }

    /// <inheritdoc />
    public async Task SetPrimaryPhoneAsync(Guid contactId, Guid phoneId, CancellationToken ct = default)
    {
        await FindEditableContactAsync(contactId, ct);

        var phones = await _context.ContactPhoneNumbers
            .Where(p => p.ContactId == contactId)
            .ToListAsync(ct);

        var targetPhone = phones.FirstOrDefault(p => p.Id == phoneId)
            ?? throw new EntityNotFoundException(nameof(ContactPhoneNumber), phoneId);

        foreach (var phone in phones)
        {
            phone.IsPrimary = phone.Id == phoneId;
        }

        await _context.SaveChangesAsync(ct);
    }

    #endregion

    #region Social Media Management

    /// <inheritdoc />
    public async Task<ContactSocialMediaDto> AddSocialMediaAsync(Guid contactId, AddSocialMediaRequest request, CancellationToken ct = default)
    {
        WebLink.Ensure(request.ProfileUrl, nameof(request.ProfileUrl));
        var contact = await FindEditableContactAsync(contactId, ct);

        // Check for duplicate service/username
        var exists = await _context.ContactSocialMedia
            .AnyAsync(s => s.ContactId == contactId && s.Service == request.Service, ct);
        if (exists)
        {
            throw new DuplicateEntityException(nameof(ContactSocialMedia), "Service", request.Service.ToString());
        }

        var social = new ContactSocialMedia
        {
            Id = Guid.NewGuid(),
            TenantId = contact.TenantId,
            ContactId = contactId,
            Service = request.Service,
            Username = request.Username,
            ProfileUrl = request.ProfileUrl
        };

        _context.ContactSocialMedia.Add(social);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.SocialMediaAdded, null,
            JsonSerializer.Serialize(new { Service = request.Service.ToString(), Username = request.Username }),
            "Social media added", ct);

        return ContactMapper.ToContactSocialMediaDto(social);
    }

    /// <inheritdoc />
    public async Task<ContactSocialMediaDto> UpdateSocialMediaAsync(Guid socialMediaId, AddSocialMediaRequest request, CancellationToken ct = default)
    {
        WebLink.Ensure(request.ProfileUrl, nameof(request.ProfileUrl));
        var social = await _context.ContactSocialMedia.FindAsync(new object[] { socialMediaId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactSocialMedia), socialMediaId);
        await FindEditableContactAsync(social.ContactId, ct, (nameof(ContactSocialMedia), socialMediaId));

        // Check for duplicate if service changed
        if (social.Service != request.Service)
        {
            var exists = await _context.ContactSocialMedia
                .AnyAsync(s => s.ContactId == social.ContactId && s.Service == request.Service && s.Id != socialMediaId, ct);
            if (exists)
            {
                throw new DuplicateEntityException(nameof(ContactSocialMedia), "Service", request.Service.ToString());
            }
        }

        social.Service = request.Service;
        social.Username = request.Username;
        social.ProfileUrl = request.ProfileUrl;

        await _context.SaveChangesAsync(ct);

        return ContactMapper.ToContactSocialMediaDto(social);
    }

    /// <inheritdoc />
    public async Task RemoveSocialMediaAsync(Guid socialMediaId, CancellationToken ct = default)
    {
        var social = await _context.ContactSocialMedia.FindAsync(new object[] { socialMediaId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactSocialMedia), socialMediaId);
        await FindEditableContactAsync(social.ContactId, ct, (nameof(ContactSocialMedia), socialMediaId));

        var contactId = social.ContactId;
        _context.ContactSocialMedia.Remove(social);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.SocialMediaRemoved, null, null, "Social media removed", ct);
    }

    #endregion

    #region Relationship Management

    /// <inheritdoc />
    public async Task<ContactRelationshipDto> AddRelationshipAsync(Guid sourceContactId, AddRelationshipRequest request, CancellationToken ct = default)
    {
        var sourceContact = await FindEditableContactAsync(sourceContactId, ct);

        var targetContact = await VisibleContacts().FirstOrDefaultAsync(c => c.Id == request.TargetContactId, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), request.TargetContactId);

        // Check for existing relationship
        var exists = await _context.ContactRelationships
            .AnyAsync(r => r.SourceContactId == sourceContactId && r.TargetContactId == request.TargetContactId, ct);
        if (exists)
        {
            throw new DuplicateEntityException(nameof(ContactRelationship), "relationship", $"{sourceContactId} -> {request.TargetContactId}");
        }

        var relationship = new ContactRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = sourceContact.TenantId,
            SourceContactId = sourceContactId,
            TargetContactId = request.TargetContactId,
            RelationshipType = request.RelationshipType,
            CustomLabel = request.CustomLabel
        };

        _context.ContactRelationships.Add(relationship);

        // Create inverse relationship if requested. The inverse is a change to the target, so
        // it is only written when the target is one this user could edit directly.
        if (request.CreateInverse && await CanEditAsync(request.TargetContactId, ct))
        {
            var inverseType = RelationshipMapper.GetInverse(request.RelationshipType, sourceContact.Gender);
            if (inverseType.HasValue)
            {
                // Check if inverse already exists
                var inverseExists = await _context.ContactRelationships
                    .AnyAsync(r => r.SourceContactId == request.TargetContactId && r.TargetContactId == sourceContactId, ct);

                if (!inverseExists)
                {
                    var inverseRelationship = new ContactRelationship
                    {
                        Id = Guid.NewGuid(),
                        TenantId = sourceContact.TenantId,
                        SourceContactId = request.TargetContactId,
                        TargetContactId = sourceContactId,
                        RelationshipType = inverseType.Value
                    };
                    _context.ContactRelationships.Add(inverseRelationship);
                }
            }
        }

        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(sourceContactId, ContactAuditAction.RelationshipAdded, null,
            JsonSerializer.Serialize(new { TargetContactId = request.TargetContactId, Type = request.RelationshipType.ToString() }),
            "Relationship added", ct);

        // Reload with target contact
        var result = await _context.ContactRelationships
            .Include(r => r.TargetContact)
            .FirstAsync(r => r.Id == relationship.Id, ct);

        return ContactMapper.ToContactRelationshipDto(result);
    }

    /// <inheritdoc />
    public async Task RemoveRelationshipAsync(Guid relationshipId, bool removeInverse = true, CancellationToken ct = default)
    {
        var relationship = await _context.ContactRelationships.FindAsync(new object[] { relationshipId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactRelationship), relationshipId);
        await FindEditableContactAsync(relationship.SourceContactId, ct, (nameof(ContactRelationship), relationshipId));

        var sourceContactId = relationship.SourceContactId;
        var targetContactId = relationship.TargetContactId;

        _context.ContactRelationships.Remove(relationship);

        // Remove inverse if requested, on the same terms it would have been created on
        if (removeInverse && await CanEditAsync(targetContactId, ct))
        {
            var inverse = await _context.ContactRelationships
                .FirstOrDefaultAsync(r => r.SourceContactId == targetContactId && r.TargetContactId == sourceContactId, ct);
            if (inverse != null)
            {
                _context.ContactRelationships.Remove(inverse);
            }
        }

        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(sourceContactId, ContactAuditAction.RelationshipRemoved, null, null, "Relationship removed", ct);
    }

    /// <inheritdoc />
    public async Task<List<ContactRelationshipDto>> GetRelationshipsAsync(Guid contactId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(contactId, ct);

        var visible = VisibleContacts();
        var relationships = await _context.ContactRelationships
            .Include(r => r.TargetContact)
            .Where(r => r.SourceContactId == contactId && visible.Any(c => c.Id == r.TargetContactId))
            .OrderBy(r => r.RelationshipType)
            .ToListAsync(ct);

        return relationships.Select(ContactMapper.ToContactRelationshipDto).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ContactRelationshipDto>> GetInverseRelationshipsAsync(Guid contactId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(contactId, ct);

        var visible = VisibleContacts();
        var relationships = await _context.ContactRelationships
            .Include(r => r.SourceContact)
            .Where(r => r.TargetContactId == contactId && visible.Any(c => c.Id == r.SourceContactId))
            .OrderBy(r => r.RelationshipType)
            .ToListAsync(ct);

        // Map with source as the "target" for display purposes
        return relationships.Select(r => new ContactRelationshipDto
        {
            Id = r.Id,
            SourceContactId = r.TargetContactId,
            TargetContactId = r.SourceContactId,
            RelationshipType = r.RelationshipType,
            CustomLabel = r.CustomLabel,
            TargetContactName = r.SourceContact != null
                ? (!string.IsNullOrWhiteSpace(r.SourceContact.PreferredName)
                    ? r.SourceContact.PreferredName
                    : $"{r.SourceContact.FirstName} {r.SourceContact.LastName}".Trim())
                : string.Empty,
            TargetIsUserLinked = r.SourceContact?.LinkedUserId.HasValue ?? false,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    #endregion

    #region Tag Management

    /// <inheritdoc />
    public async Task<ContactTagDto> CreateTagAsync(CreateContactTagRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new InvalidOperationException("Tenant ID is required");

        // Check for duplicate name
        var exists = await _context.ContactTags
            .AnyAsync(t => t.TenantId == tenantId && t.Name.ToLower() == request.Name.ToLower(), ct);
        if (exists)
        {
            throw new DuplicateEntityException(nameof(ContactTag), "Name", request.Name);
        }

        var tag = ContactMapper.FromCreateContactTagRequest(request);
        tag.Id = Guid.NewGuid();
        tag.TenantId = tenantId;

        _context.ContactTags.Add(tag);
        await _context.SaveChangesAsync(ct);

        return ContactMapper.ToContactTagDto(tag);
    }

    /// <inheritdoc />
    public async Task<ContactTagDto?> GetTagByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await _context.ContactTags
            .Include(t => t.Contacts)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return tag == null ? null : ContactMapper.ToContactTagDto(tag);
    }

    /// <inheritdoc />
    public async Task<List<ContactTagDto>> ListTagsAsync(CancellationToken ct = default)
    {
        var tags = await _context.ContactTags
            .Include(t => t.Contacts)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return tags.Select(ContactMapper.ToContactTagDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ContactTagDto> UpdateTagAsync(Guid id, UpdateContactTagRequest request, CancellationToken ct = default)
    {
        var tag = await _context.ContactTags
            .Include(t => t.Contacts)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(ContactTag), id);

        // Check for duplicate name if changed
        if (!tag.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _context.ContactTags
                .AnyAsync(t => t.Id != id && t.Name.ToLower() == request.Name.ToLower(), ct);
            if (exists)
            {
                throw new DuplicateEntityException(nameof(ContactTag), "Name", request.Name);
            }
        }

        tag.Name = request.Name;
        tag.Description = request.Description;
        tag.Color = request.Color;
        tag.Icon = request.Icon;
        await _context.SaveChangesAsync(ct);

        return ContactMapper.ToContactTagDto(tag);
    }

    /// <inheritdoc />
    public async Task DeleteTagAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await _context.ContactTags.FindAsync(new object[] { id }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactTag), id);

        _context.ContactTags.Remove(tag);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddTagToContactAsync(Guid contactId, Guid tagId, CancellationToken ct = default)
    {
        var contact = await FindEditableContactAsync(contactId, ct);

        var tag = await _context.ContactTags.FindAsync(new object[] { tagId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactTag), tagId);

        var exists = await _context.ContactTagLinks
            .AnyAsync(l => l.ContactId == contactId && l.TagId == tagId, ct);
        if (exists) return;

        var link = new ContactTagLink
        {
            Id = Guid.NewGuid(),
            TenantId = contact.TenantId,
            ContactId = contactId,
            TagId = tagId
        };

        _context.ContactTagLinks.Add(link);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RemoveTagFromContactAsync(Guid contactId, Guid tagId, CancellationToken ct = default)
    {
        await FindEditableContactAsync(contactId, ct);

        var link = await _context.ContactTagLinks
            .FirstOrDefaultAsync(l => l.ContactId == contactId && l.TagId == tagId, ct);

        if (link != null)
        {
            _context.ContactTagLinks.Remove(link);
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task SetContactTagsAsync(Guid contactId, List<Guid> tagIds, CancellationToken ct = default)
    {
        var contact = await FindEditableContactAsync(contactId, ct);

        // Remove existing tags
        var existingLinks = await _context.ContactTagLinks
            .Where(l => l.ContactId == contactId)
            .ToListAsync(ct);
        _context.ContactTagLinks.RemoveRange(existingLinks);

        // Add new tags
        foreach (var tagId in tagIds.Distinct())
        {
            var tag = await _context.ContactTags.FindAsync(new object[] { tagId }, ct);
            if (tag != null)
            {
                var link = new ContactTagLink
                {
                    Id = Guid.NewGuid(),
                    TenantId = contact.TenantId,
                    ContactId = contactId,
                    TagId = tagId
                };
                _context.ContactTagLinks.Add(link);
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    #endregion

    #region Sharing Management

    /// <inheritdoc />
    public async Task<ContactUserShareDto> ShareContactAsync(Guid contactId, ShareContactRequest request, CancellationToken ct = default)
    {
        var contact = await FindOwnedContactAsync(contactId, ct);

        var user = await _context.Users.FindAsync(new object[] { request.SharedWithUserId }, ct)
            ?? throw new EntityNotFoundException(nameof(User), request.SharedWithUserId);

        // Check for existing share
        var exists = await _context.ContactUserShares
            .AnyAsync(s => s.ContactId == contactId && s.SharedWithUserId == request.SharedWithUserId, ct);
        if (exists)
        {
            throw new DuplicateEntityException(nameof(ContactUserShare), "share", $"{contactId} -> {request.SharedWithUserId}");
        }

        var share = new ContactUserShare
        {
            Id = Guid.NewGuid(),
            TenantId = contact.TenantId,
            ContactId = contactId,
            SharedWithUserId = request.SharedWithUserId,
            CanEdit = request.CanEdit
        };

        _context.ContactUserShares.Add(share);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.SharedWithUser, null,
            JsonSerializer.Serialize(new { SharedWithUserId = request.SharedWithUserId, CanEdit = request.CanEdit }),
            "Contact shared", ct);

        // Reload with user
        var result = await _context.ContactUserShares
            .Include(s => s.SharedWithUser)
            .FirstAsync(s => s.Id == share.Id, ct);

        return ContactMapper.ToContactUserShareDto(result);
    }

    /// <inheritdoc />
    public async Task<ContactUserShareDto> UpdateShareAsync(Guid shareId, bool canEdit, CancellationToken ct = default)
    {
        var share = await _context.ContactUserShares
            .Include(s => s.SharedWithUser)
            .FirstOrDefaultAsync(s => s.Id == shareId, ct)
            ?? throw new EntityNotFoundException(nameof(ContactUserShare), shareId);
        await FindOwnedContactAsync(share.ContactId, ct);

        share.CanEdit = canEdit;
        await _context.SaveChangesAsync(ct);

        return ContactMapper.ToContactUserShareDto(share);
    }

    /// <inheritdoc />
    public async Task RemoveShareAsync(Guid shareId, CancellationToken ct = default)
    {
        var share = await _context.ContactUserShares.FindAsync(new object[] { shareId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactUserShare), shareId);
        await FindOwnedContactAsync(share.ContactId, ct);

        var contactId = share.ContactId;
        _context.ContactUserShares.Remove(share);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.UnsharedFromUser, null, null, "Contact unshared", ct);
    }

    /// <inheritdoc />
    public async Task<List<ContactUserShareDto>> GetSharesAsync(Guid contactId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(contactId, ct);

        var shares = await _context.ContactUserShares
            .Include(s => s.SharedWithUser)
            .Where(s => s.ContactId == contactId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        return shares.Select(ContactMapper.ToContactUserShareDto).ToList();
    }

    #endregion

    #region Email Management

    /// <inheritdoc />
    public async Task<ContactEmailAddressDto> AddEmailAsync(Guid contactId, AddEmailRequest request, CancellationToken ct = default)
    {
        var contact = await FindEditableContactAsync(contactId, ct);

        // If setting as primary, clear other primaries
        if (request.IsPrimary)
        {
            var existingPrimaries = await _context.ContactEmailAddresses
                .Where(e => e.ContactId == contactId && e.IsPrimary)
                .ToListAsync(ct);
            foreach (var existing in existingPrimaries)
            {
                existing.IsPrimary = false;
            }
        }

        var email = new ContactEmailAddress
        {
            Id = Guid.NewGuid(),
            TenantId = contact.TenantId,
            ContactId = contactId,
            Email = request.Email,
            NormalizedEmail = request.Email?.Trim().ToLowerInvariant(),
            Tag = request.Tag,
            IsPrimary = request.IsPrimary,
            Label = request.Label
        };

        _context.ContactEmailAddresses.Add(email);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.EmailAdded, null,
            JsonSerializer.Serialize(new { Email = request.Email, Tag = request.Tag.ToString() }),
            "Email address added", ct);

        return ContactMapper.ToContactEmailAddressDto(email);
    }

    /// <inheritdoc />
    public async Task<ContactEmailAddressDto> UpdateEmailAsync(Guid emailId, AddEmailRequest request, CancellationToken ct = default)
    {
        var email = await _context.ContactEmailAddresses.FindAsync(new object[] { emailId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactEmailAddress), emailId);
        await FindEditableContactAsync(email.ContactId, ct, (nameof(ContactEmailAddress), emailId));

        email.Email = request.Email;
        email.NormalizedEmail = request.Email?.Trim().ToLowerInvariant();
        email.Tag = request.Tag;
        email.Label = request.Label;

        // Handle primary change
        if (request.IsPrimary && !email.IsPrimary)
        {
            var existingPrimaries = await _context.ContactEmailAddresses
                .Where(e => e.ContactId == email.ContactId && e.IsPrimary && e.Id != emailId)
                .ToListAsync(ct);
            foreach (var existing in existingPrimaries)
            {
                existing.IsPrimary = false;
            }
        }
        email.IsPrimary = request.IsPrimary;

        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(email.ContactId, ContactAuditAction.EmailUpdated, null,
            JsonSerializer.Serialize(new { Email = request.Email, Tag = request.Tag.ToString() }),
            "Email address updated", ct);

        return ContactMapper.ToContactEmailAddressDto(email);
    }

    /// <inheritdoc />
    public async Task RemoveEmailAsync(Guid emailId, CancellationToken ct = default)
    {
        var email = await _context.ContactEmailAddresses.FindAsync(new object[] { emailId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactEmailAddress), emailId);
        await FindEditableContactAsync(email.ContactId, ct, (nameof(ContactEmailAddress), emailId));

        var contactId = email.ContactId;
        _context.ContactEmailAddresses.Remove(email);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.EmailRemoved, null, null, "Email address removed", ct);
    }

    /// <inheritdoc />
    public async Task SetPrimaryEmailAsync(Guid contactId, Guid emailId, CancellationToken ct = default)
    {
        await FindEditableContactAsync(contactId, ct);

        var emails = await _context.ContactEmailAddresses
            .Where(e => e.ContactId == contactId)
            .ToListAsync(ct);

        var targetEmail = emails.FirstOrDefault(e => e.Id == emailId)
            ?? throw new EntityNotFoundException(nameof(ContactEmailAddress), emailId);

        foreach (var email in emails)
        {
            email.IsPrimary = email.Id == emailId;
        }

        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.PrimaryEmailChanged, null,
            JsonSerializer.Serialize(new { EmailId = emailId }),
            "Primary email changed", ct);
    }

    #endregion

    #region Profile Image Management

    /// <inheritdoc />
    public async Task<string> UploadProfileImageAsync(Guid contactId, Stream imageStream, string fileName, CancellationToken ct = default)
    {
        var contact = await FindEditableContactAsync(contactId, ct);

        // Delete old profile image if exists
        if (!string.IsNullOrEmpty(contact.ProfileImageFileName))
        {
            await _fileStorageService.DeleteContactProfileImageAsync(contactId, contact.ProfileImageFileName, ct);
        }

        // Save new image
        var storedFileName = await _fileStorageService.SaveContactProfileImageAsync(contactId, imageStream, fileName, ct);

        contact.ProfileImageFileName = storedFileName;
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.ProfileImageUpdated, null, null, "Profile image updated", ct);

        return _fileStorageService.GetContactProfileImageUrl(contactId);
    }

    /// <inheritdoc />
    public async Task DeleteProfileImageAsync(Guid contactId, CancellationToken ct = default)
    {
        var contact = await FindEditableContactAsync(contactId, ct);

        if (!string.IsNullOrEmpty(contact.ProfileImageFileName))
        {
            await _fileStorageService.DeleteContactProfileImageAsync(contactId, contact.ProfileImageFileName, ct);
            contact.ProfileImageFileName = null;
            await _context.SaveChangesAsync(ct);

            await LogAuditAsync(contactId, ContactAuditAction.ProfileImageRemoved, null, null, "Profile image removed", ct);
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetProfileImageFileNameForSignedRequestAsync(Guid contactId, CancellationToken ct = default) =>
        await _context.Contacts
            .Where(c => c.Id == contactId)
            .Select(c => c.ProfileImageFileName)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public string? GetProfileImageUrl(Guid contactId)
    {
        return _fileStorageService.GetContactProfileImageUrl(contactId);
    }

    #endregion

    #region Audit Log

    /// <inheritdoc />
    public async Task<List<ContactAuditLogDto>> GetAuditLogAsync(Guid contactId, int? limit = null, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(contactId, ct);

        var query = _context.ContactAuditLogs
            .Include(l => l.User)
            .Where(l => l.ContactId == contactId)
            .OrderByDescending(l => l.CreatedAt)
            .AsQueryable();

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        var logs = await query.ToListAsync(ct);

        return logs.Select(ContactMapper.ToContactAuditLogDto).ToList();
    }

    #endregion

    #region Device Sync

    /// <inheritdoc />
    public async Task ApplyDeviceSyncUpdateAsync(Guid contactId, DeviceSyncUpdateRequest request, CancellationToken ct = default)
    {
        await FindEditableContactAsync(contactId, ct);

        var contact = await _context.Contacts
            .Include(c => c.PhoneNumbers)
            .Include(c => c.EmailAddresses)
            .Include(c => c.Addresses)
                .ThenInclude(a => a.Address)
            .Include(c => c.SocialMedia)
            .FirstOrDefaultAsync(c => c.Id == contactId, ct)
            ?? throw new EntityNotFoundException("Contact", contactId);

        // Resolve the device's addresses to shared rows before changing anything, since
        // creating a row saves it. Device sync supplies free text from the device's address
        // book — treated as unverified, so libpostal can canonicalize before hashing.
        // An entry with no components is not an address and is dropped.
        var wanted = new List<(DeviceSyncAddressEntry Entry, Address Row)>(request.Addresses.Count);
        foreach (var a in request.Addresses)
        {
            var identity = new AddressIdentity(a.AddressLine1, a.City, a.StateProvince, a.PostalCode, a.Country);
            if (!identity.IsEmpty)
                wanted.Add((a, await _context.FindOrCreateAsync(_addressHasher, identity, ct)));
        }

        // Update core fields
        contact.FirstName = request.FirstName;
        contact.MiddleName = request.MiddleName;
        contact.LastName = request.LastName;
        contact.PreferredName = request.PreferredName;
        contact.CompanyName = request.CompanyName;
        contact.Title = request.Title;
        contact.Website = request.Website;
        // Notes: only update if non-null (iOS cannot read notes, sends null to preserve existing)
        if (request.Notes != null)
            contact.Notes = request.Notes;

        // Birth date
        if (request.BirthYear.HasValue || request.BirthMonth.HasValue || request.BirthDay.HasValue)
        {
            contact.BirthYear = request.BirthYear;
            contact.BirthMonth = request.BirthMonth;
            contact.BirthDay = request.BirthDay;
            contact.BirthDatePrecision = (request.BirthYear.HasValue, request.BirthMonth.HasValue, request.BirthDay.HasValue) switch
            {
                (true, true, true) => Domain.Enums.DatePrecision.Full,
                (true, true, false) => Domain.Enums.DatePrecision.YearMonth,
                (true, false, false) => Domain.Enums.DatePrecision.Year,
                _ => Domain.Enums.DatePrecision.Unknown
            };
        }
        else
        {
            contact.BirthYear = null;
            contact.BirthMonth = null;
            contact.BirthDay = null;
            contact.BirthDatePrecision = Domain.Enums.DatePrecision.Unknown;
        }

        contact.UpdatedAt = DateTime.UtcNow;

        // Replace phone numbers
        _context.ContactPhoneNumbers.RemoveRange(contact.PhoneNumbers);
        for (var i = 0; i < request.PhoneNumbers.Count; i++)
        {
            var p = request.PhoneNumbers[i];
            _context.ContactPhoneNumbers.Add(new ContactPhoneNumber
            {
                Id = Guid.NewGuid(),
                TenantId = contact.TenantId,
                ContactId = contactId,
                PhoneNumber = p.PhoneNumber,
                NormalizedNumber = NormalizePhoneNumber(p.PhoneNumber),
                Tag = (Domain.Enums.PhoneTag)p.Tag,
                IsPrimary = i == 0
            });
        }

        // Replace email addresses
        _context.ContactEmailAddresses.RemoveRange(contact.EmailAddresses);
        for (var i = 0; i < request.EmailAddresses.Count; i++)
        {
            var e = request.EmailAddresses[i];
            _context.ContactEmailAddresses.Add(new ContactEmailAddress
            {
                Id = Guid.NewGuid(),
                TenantId = contact.TenantId,
                ContactId = contactId,
                Email = e.Email,
                NormalizedEmail = e.Email.Trim().ToLowerInvariant(),
                Tag = (Domain.Enums.EmailTag)e.Tag,
                IsPrimary = i == 0
            });
        }

        // Replace addresses — keep the existing link, with its unit lines, when the device
        // still has that address. The shared rows themselves are never edited (issue #119).
        var existingAddresses = contact.Addresses.ToList();
        foreach (var existingAddr in existingAddresses)
        {
            var match = wanted.FirstOrDefault(w => w.Row.Id == existingAddr.AddressId);
            if (match.Row != null)
            {
                existingAddr.Tag = (Domain.Enums.AddressTag)match.Entry.Tag;
                wanted.RemoveAll(w => w.Row.Id == existingAddr.AddressId);
            }
            else
            {
                _context.ContactAddresses.Remove(existingAddr);
            }
        }

        // Add the rest. A contact links each address once, so repeats collapse.
        var linked = new HashSet<Guid>();
        foreach (var (entry, row) in wanted)
        {
            if (!linked.Add(row.Id))
                continue;

            _context.ContactAddresses.Add(new ContactAddress
            {
                Id = Guid.NewGuid(),
                TenantId = contact.TenantId,
                ContactId = contactId,
                AddressId = row.Id,
                Tag = (Domain.Enums.AddressTag)entry.Tag,
                IsPrimary = !existingAddresses.Any() && linked.Count == 1
            });
        }

        // Set primary on first remaining address if no primary exists
        var allAddresses = await _context.ContactAddresses
            .Where(ca => ca.ContactId == contactId)
            .ToListAsync(ct);
        if (allAddresses.Count > 0 && !allAddresses.Any(a => a.IsPrimary))
        {
            allAddresses[0].IsPrimary = true;
        }

        // Replace social media
        _context.ContactSocialMedia.RemoveRange(contact.SocialMedia);
        foreach (var s in request.SocialMedia)
        {
            _context.ContactSocialMedia.Add(new ContactSocialMedia
            {
                Id = Guid.NewGuid(),
                TenantId = contact.TenantId,
                ContactId = contactId,
                Service = (Domain.Enums.SocialMediaService)s.Service,
                Username = s.Username,
                // A phone's address book can hold links that are not web pages (an app's own
                // scheme). Those are dropped rather than failing the whole sync.
                ProfileUrl = WebLink.SafeHref(s.ProfileUrl)
            });
        }

        await _context.SaveChangesAsync(ct);
        await LogAuditAsync(contactId, ContactAuditAction.Updated, null, null, "Synced from device", ct);
    }

    #endregion

    #region Contact Groups

    /// <inheritdoc />
    public async Task<ContactGroupSummaryDto> CreateGroupAsync(CreateContactGroupRequest request, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Creating contact group: {GroupName} ({ContactType})", request.GroupName, request.ContactType);

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            CompanyName = request.GroupName,
            ContactType = request.ContactType,
            Notes = request.Notes,
            Website = request.ContactType == Domain.Enums.ContactType.Business ? request.Website : null,
            BusinessCategory = request.ContactType == Domain.Enums.ContactType.Business ? request.BusinessCategory : null,
            CreatedByUserId = userId,
            Visibility = ContactVisibilityLevel.TenantShared,
            IsActive = true
        };

        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync(ct);

        // Add initial tags if provided
        if (request.TagIds?.Count > 0)
        {
            await SetContactTagsAsync(contact.Id, request.TagIds, ct);
        }

        await LogAuditAsync(contact.Id, ContactAuditAction.Created, null, contact, "Contact group created", ct);

        _logger.LogInformation("Created contact group: {Id}", contact.Id);

        return new ContactGroupSummaryDto
        {
            Id = contact.Id,
            ContactType = request.ContactType,
            GroupName = request.GroupName,
            MemberCount = 0,
            IsTenantHousehold = false,
            Website = contact.Website,
            BusinessCategory = contact.BusinessCategory,
            CreatedAt = contact.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task<ContactDto> GetGroupByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException("Contact group", id);

        if (!dto.IsGroup)
        {
            throw new InvalidOperationException("Contact is not a group");
        }

        return dto;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ContactGroupSummaryDto>> ListGroupsAsync(ContactFilterRequest filter, CancellationToken ct = default)
    {
        filter.Page = QueryLimits.Page(filter.Page);
        filter.PageSize = QueryLimits.PageSize(filter.PageSize, 25);

        var query = _context.Contacts
            .Include(c => c.Members)
            .Include(c => c.Addresses)
                .ThenInclude(a => a.Address)
            .Include(c => c.Tags)
                .ThenInclude(t => t.Tag)
            .Where(c => c.ParentContactId == null)
            .AsQueryable();

        if (filter.ContactType.HasValue)
        {
            query = query.Where(c => c.ContactType == filter.ContactType.Value);
        }

        // A group's members each carry their own visibility, so a group must neither match on,
        // count, nor name a member the reader cannot see.
        var visibleMembers = VisibleContacts();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(c =>
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(searchTerm)) ||
                visibleMembers.Any(m => m.ParentContactId == c.Id &&
                    ((m.FirstName != null && m.FirstName.ToLower().Contains(searchTerm)) ||
                     (m.LastName != null && m.LastName.ToLower().Contains(searchTerm)))));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == filter.IsActive.Value);
        }

        if (filter.TagIds?.Count > 0)
        {
            query = query.Where(c => c.Tags.Any(t => filter.TagIds.Contains(t.TagId)));
        }

        // Sort groups
        query = filter.SortBy?.ToLower() switch
        {
            "createdat" => filter.SortDescending
                ? query.OrderByDescending(c => c.CreatedAt)
                : query.OrderBy(c => c.CreatedAt),
            _ => filter.SortDescending
                ? query.OrderByDescending(c => c.CompanyName)
                : query.OrderBy(c => c.CompanyName)
        };

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip(QueryLimits.Skip(filter.Page, filter.PageSize))
            .Take(filter.PageSize)
            .ToListAsync(ct);

        var groupIds = items.Select(c => (Guid?)c.Id).ToList();
        var visibleMemberIds = (await visibleMembers
                .Where(m => groupIds.Contains(m.ParentContactId))
                .Select(m => m.Id)
                .ToListAsync(ct))
            .ToHashSet();

        var dtos = items.Select(c => new ContactGroupSummaryDto
        {
            Id = c.Id,
            ContactType = c.ContactType ?? Domain.Enums.ContactType.Household,
            GroupName = c.CompanyName ?? "Unknown",
            MemberCount = c.Members.Count(m => visibleMemberIds.Contains(m.Id)),
            MemberFirstNames = c.Members
                .Where(m => visibleMemberIds.Contains(m.Id))
                .OrderBy(m => m.FirstName)
                .Select(m => m.FirstName ?? string.Empty)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList(),
            IsTenantHousehold = c.IsTenantHousehold,
            PrimaryAddress = c.Addresses
                .Where(a => a.IsPrimary)
                .Select(a => a.Address.FormattedAddress ?? $"{a.Address.City}, {a.Address.StateProvince}")
                .FirstOrDefault(),
            TagNames = c.Tags.Select(t => t.Tag.Name).ToList(),
            TagColors = c.Tags.Select(t => t.Tag.Color).ToList(),
            Website = c.Website,
            BusinessCategory = c.BusinessCategory,
            CreatedAt = c.CreatedAt
        }).ToList();

        for (int i = 0; i < items.Count; i++)
        {
            if (!string.IsNullOrEmpty(items[i].ProfileImageFileName))
            {
                dtos[i].ProfileImageUrl = _fileUrlService.GetContactProfileImageUrl(items[i].Id, items[i].TenantId, items[i].ProfileImageFileName);
            }
        }

        return new PagedResult<ContactGroupSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    /// <inheritdoc />
    public async Task UpdateGroupAsync(Guid id, UpdateContactGroupRequest request, CancellationToken ct = default)
    {
        var contact = await _context.Contacts.FindAsync(new object[] { id }, ct)
            ?? throw new EntityNotFoundException("Contact group", id);

        if (contact.ParentContactId != null)
        {
            throw new InvalidOperationException("Contact is not a group");
        }

        var oldValues = System.Text.Json.JsonSerializer.Serialize(new
        {
            contact.CompanyName,
            contact.ContactType,
            contact.Notes,
            contact.Website,
            contact.BusinessCategory,
            contact.IsActive
        });

        contact.CompanyName = request.GroupName;
        contact.ContactType = request.ContactType;
        contact.Notes = request.Notes;
        contact.Website = request.ContactType == Domain.Enums.ContactType.Business ? request.Website : null;
        contact.BusinessCategory = request.ContactType == Domain.Enums.ContactType.Business ? request.BusinessCategory : null;
        contact.IsActive = request.IsActive;
        contact.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        var newValues = System.Text.Json.JsonSerializer.Serialize(new
        {
            contact.CompanyName,
            contact.ContactType,
            contact.Notes,
            contact.Website,
            contact.BusinessCategory,
            contact.IsActive
        });

        await LogAuditAsync(id, ContactAuditAction.Updated, oldValues, newValues, "Contact group updated", ct);

        _logger.LogInformation("Updated contact group: {Id}", id);
    }

    /// <inheritdoc />
    public async Task DeleteGroupAsync(Guid id, CancellationToken ct = default)
    {
        var contact = await _context.Contacts
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new EntityNotFoundException("Contact group", id);

        if (contact.ParentContactId != null)
        {
            throw new InvalidOperationException("Contact is not a group");
        }

        if (contact.IsTenantHousehold)
        {
            throw new InvalidOperationException("Cannot delete the tenant household group");
        }

        // Move members to tenant household
        if (contact.Members.Count > 0)
        {
            var tenantHousehold = await _context.Contacts
                .FirstOrDefaultAsync(c => c.IsTenantHousehold && c.TenantId == contact.TenantId, ct)
                ?? throw new InvalidOperationException("Tenant household not found");

            foreach (var member in contact.Members)
            {
                member.ParentContactId = tenantHousehold.Id;
            }
            await _context.SaveChangesAsync(ct);
        }

        _context.Contacts.Remove(contact);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted contact group: {Id}", id);
    }

    /// <inheritdoc />
    public async Task MoveContactToGroupAsync(Guid contactId, Guid targetGroupId, CancellationToken ct = default)
    {
        var contact = await FindEditableContactAsync(contactId, ct);

        if (contact.ParentContactId == null)
        {
            throw new InvalidOperationException("Cannot move a group contact. Only members can be moved.");
        }

        var targetGroup = await _context.Contacts
            .FirstOrDefaultAsync(c => c.Id == targetGroupId && c.ParentContactId == null, ct)
            ?? throw new EntityNotFoundException("Target contact group", targetGroupId);

        contact.ParentContactId = targetGroupId;
        contact.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Moved contact {ContactId} to group {GroupId}", contactId, targetGroupId);
    }

    /// <inheritdoc />
    public async Task<ContactDto> GetTenantHouseholdAsync(CancellationToken ct = default)
    {
        var tenantHousehold = await _context.Contacts
            .FirstOrDefaultAsync(c => c.IsTenantHousehold, ct)
            ?? throw new EntityNotFoundException("Tenant household", Guid.Empty);

        return await GetByIdAsync(tenantHousehold.Id, ct)
            ?? throw new InvalidOperationException("Failed to retrieve tenant household");
    }

    /// <inheritdoc />
    public async Task<ContactDto> EnsureTenantHouseholdAsync(string householdName, CancellationToken ct = default)
    {
        var existing = await _context.Contacts
            .FirstOrDefaultAsync(c => c.IsTenantHousehold, ct);

        if (existing != null)
        {
            // Update the name if it changed
            if (existing.CompanyName != householdName)
            {
                existing.CompanyName = householdName;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }

            return await GetByIdAsync(existing.Id, ct)
                ?? throw new InvalidOperationException("Failed to retrieve tenant household");
        }

        // Create new tenant household
        var userId = GetCurrentUserId();
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            CompanyName = householdName,
            ContactType = Domain.Enums.ContactType.Household,
            IsTenantHousehold = true,
            CreatedByUserId = userId,
            Visibility = ContactVisibilityLevel.TenantShared,
            IsActive = true,
            UsesTenantAddress = true
        };

        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contact.Id, ContactAuditAction.Created, null, contact, "Tenant household created", ct);

        _logger.LogInformation("Created tenant household: {Id}", contact.Id);

        return await GetByIdAsync(contact.Id, ct)
            ?? throw new InvalidOperationException("Failed to retrieve created tenant household");
    }

    #endregion

    #region Private Helpers

    private Guid GetCurrentUserId()
    {
        return _tenantProvider.UserId
            ?? throw new InvalidOperationException("User ID is required for this operation");
    }

    /// <summary>Contacts the current user may see. See <see cref="ContactVisibility"/>.</summary>
    private IQueryable<Contact> VisibleContacts() =>
        _context.Contacts.Where(ContactVisibility.VisibleTo(_tenantProvider.UserId));

    /// <summary>
    /// Throws not-found unless the current user may see the contact. A contact somebody else
    /// keeps private answers exactly as one that does not exist.
    /// </summary>
    private async Task EnsureVisibleAsync(Guid contactId, CancellationToken ct)
    {
        if (!await VisibleContacts().AnyAsync(c => c.Id == contactId, ct))
            throw new EntityNotFoundException(nameof(Contact), contactId);
    }

    /// <summary>
    /// The contact, provided the current user may change it: it is shared with the household,
    /// they created it, or it was shared with them with edit rights. Role is the controller's
    /// business; this is only about whose contact it is.
    /// </summary>
    /// <param name="notFoundEntity">
    /// Names the entity in the not-found error when the caller was addressed by a child id, so
    /// a phone on a hidden contact reads as a missing phone rather than confirming the contact.
    /// </param>
    private async Task<Contact> FindEditableContactAsync(
        Guid contactId, CancellationToken ct, (string Type, Guid Id)? notFoundEntity = null)
    {
        var (contact, canEdit) = await LoadWithEditRightAsync(contactId, ct);

        if (contact == null)
            throw notFoundEntity is { } e
                ? new EntityNotFoundException(e.Type, e.Id)
                : new EntityNotFoundException(nameof(Contact), contactId);

        if (!canEdit)
            throw new BusinessRuleViolationException("ContactReadOnly",
                "This contact was shared with you to view, not to change.");

        return contact;
    }

    /// <summary>
    /// Tells the caller what they may do with the contact, so a client can show it read-only
    /// rather than learn that from a rejected save.
    /// </summary>
    private async Task ApplyCallerRightsAsync(ContactDto dto, CancellationToken ct)
    {
        dto.IsOwner = dto.CreatedByUserId == _tenantProvider.UserId;
        dto.CanEdit = dto.IsOwner || await _context.Contacts
            .Where(ContactVisibility.EditableBy(_tenantProvider.UserId))
            .AnyAsync(c => c.Id == dto.Id, ct);
    }

    /// <summary>Whether <see cref="FindEditableContactAsync"/> would succeed.</summary>
    private async Task<bool> CanEditAsync(Guid contactId, CancellationToken ct) =>
        (await LoadWithEditRightAsync(contactId, ct)).CanEdit;

    private async Task<(Contact? Contact, bool CanEdit)> LoadWithEditRightAsync(Guid contactId, CancellationToken ct)
    {
        var contact = await VisibleContacts().FirstOrDefaultAsync(c => c.Id == contactId, ct);
        if (contact == null) return (null, false);

        var canEdit = await _context.Contacts
            .Where(ContactVisibility.EditableBy(_tenantProvider.UserId))
            .AnyAsync(c => c.Id == contactId, ct);

        return (contact, canEdit);
    }

    /// <summary>
    /// The contact, provided the current user created it. Who a contact is shared with, and
    /// how visible it is, belong to its creator alone.
    /// </summary>
    private async Task<Contact> FindOwnedContactAsync(Guid contactId, CancellationToken ct)
    {
        var contact = await VisibleContacts().FirstOrDefaultAsync(c => c.Id == contactId, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

        if (contact.CreatedByUserId != _tenantProvider.UserId)
            throw new BusinessRuleViolationException("ContactOwnerOnly",
                "Only the person who created this contact can change who sees it.");

        return contact;
    }

    private async Task LogAuditAsync(
        Guid contactId,
        ContactAuditAction action,
        string? oldValues,
        object? newValuesObject,
        string? description,
        CancellationToken ct)
    {
        var userId = _tenantProvider.UserId;
        if (!userId.HasValue) return;

        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue) return;

        var serializerOptions = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        string? newValues = newValuesObject switch
        {
            null => null,
            string s => s,
            _ => JsonSerializer.Serialize(newValuesObject, serializerOptions)
        };

        var log = new ContactAuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ContactId = contactId,
            UserId = userId.Value,
            Action = action,
            OldValues = oldValues,
            NewValues = newValues,
            Description = description
        };

        _context.ContactAuditLogs.Add(log);
        await _context.SaveChangesAsync(ct);
    }

    private static string? NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        return DigitsOnlyRegex().Replace(phoneNumber, "");
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnlyRegex();

    #endregion
}
