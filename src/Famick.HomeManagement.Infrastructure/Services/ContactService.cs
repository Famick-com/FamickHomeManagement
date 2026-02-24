using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for managing contacts, relationships, and related data
/// </summary>
public partial class ContactService : IContactService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly ITenantProvider _tenantProvider;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFileAccessTokenService _tokenService;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        HomeManagementDbContext context,
        IMapper mapper,
        ITenantProvider tenantProvider,
        IFileStorageService fileStorageService,
        IFileAccessTokenService tokenService,
        ILogger<ContactService> logger)
    {
        _context = context;
        _mapper = mapper;
        _tenantProvider = tenantProvider;
        _fileStorageService = fileStorageService;
        _tokenService = tokenService;
        _logger = logger;
    }

    #region Contact CRUD

    /// <inheritdoc />
    public async Task<ContactDto> CreateAsync(CreateContactRequest request, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Creating contact: {FirstName} {LastName}", request.FirstName, request.LastName);

        var contact = _mapper.Map<Contact>(request);
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
        var contact = await _context.Contacts
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

        var dto = _mapper.Map<ContactDto>(contact);

        // Populate members list for group contacts
        if (contact.IsGroup && contact.Members.Count > 0)
        {
            dto.Members = _mapper.Map<List<ContactSummaryDto>>(contact.Members);
        }

        // Set profile image URL if exists (with signed token for browser access)
        if (!string.IsNullOrEmpty(contact.ProfileImageFileName))
        {
            var accessToken = _tokenService.GenerateToken("contact-profile-image", contact.Id, contact.TenantId);
            dto.ProfileImageUrl = _fileStorageService.GetContactProfileImageUrl(contact.Id, accessToken);
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
        var contact = await _context.Contacts
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

        var dto = _mapper.Map<ContactDto>(contact);

        // Set profile image URL if exists (with signed token for browser access)
        if (!string.IsNullOrEmpty(contact.ProfileImageFileName))
        {
            var accessToken = _tokenService.GenerateToken("contact-profile-image", contact.Id, contact.TenantId);
            dto.ProfileImageUrl = _fileStorageService.GetContactProfileImageUrl(contact.Id, accessToken);
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
        var query = _context.Contacts
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
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        var dtos = _mapper.Map<List<ContactSummaryDto>>(items);

        // Set profile image URLs and Gravatar URLs for contacts
        for (int i = 0; i < items.Count; i++)
        {
            if (!string.IsNullOrEmpty(items[i].ProfileImageFileName))
            {
                var accessToken = _tokenService.GenerateToken("contact-profile-image", items[i].Id, items[i].TenantId);
                dtos[i].ProfileImageUrl = _fileStorageService.GetContactProfileImageUrl(items[i].Id, accessToken);
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
        var contact = await _context.Contacts.FindAsync(new object[] { id }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), id);

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

        _mapper.Map(request, contact);
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
        var contacts = await _context.Contacts
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

        var dtos = _mapper.Map<List<ContactSummaryDto>>(contacts);

        // Set profile image URLs and Gravatar URLs for contacts
        for (int i = 0; i < contacts.Count; i++)
        {
            if (!string.IsNullOrEmpty(contacts[i].ProfileImageFileName))
            {
                var accessToken = _tokenService.GenerateToken("contact-profile-image", contacts[i].Id, contacts[i].TenantId);
                dtos[i].ProfileImageUrl = _fileStorageService.GetContactProfileImageUrl(contacts[i].Id, accessToken);
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
        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

        Guid addressId;

        if (request.AddressId.HasValue)
        {
            // Use existing address by ID
            var existingAddress = await _context.Addresses.FindAsync(new object[] { request.AddressId.Value }, ct)
                ?? throw new EntityNotFoundException(nameof(Address), request.AddressId.Value);
            addressId = existingAddress.Id;
        }
        else
        {
            // Generate normalized hash for duplicate detection
            var normalizedHash = GenerateAddressHash(request);

            // Try to find existing address by GeoapifyPlaceId or NormalizedHash
            Address? existingAddress = null;

            if (!string.IsNullOrEmpty(request.GeoapifyPlaceId))
            {
                existingAddress = await _context.Addresses
                    .FirstOrDefaultAsync(a => a.GeoapifyPlaceId == request.GeoapifyPlaceId, ct);
            }

            // If not found by PlaceId, try by NormalizedHash
            if (existingAddress == null && !string.IsNullOrEmpty(normalizedHash))
            {
                existingAddress = await _context.Addresses
                    .FirstOrDefaultAsync(a => a.NormalizedHash == normalizedHash, ct);
            }

            if (existingAddress != null)
            {
                // Reuse existing address
                addressId = existingAddress.Id;
                _logger.LogInformation("Reusing existing address with Id {AddressId} (PlaceId: {PlaceId}, Hash: {Hash})",
                    existingAddress.Id, existingAddress.GeoapifyPlaceId, existingAddress.NormalizedHash);
            }
            else
            {
                // Create new address
                var address = new Address
                {
                    Id = Guid.NewGuid(),
                    AddressLine1 = request.AddressLine1,
                    AddressLine2 = request.AddressLine2,
                    AddressLine3 = request.AddressLine3,
                    AddressLine4 = request.AddressLine4,
                    City = request.City,
                    StateProvince = request.StateProvince,
                    PostalCode = request.PostalCode,
                    Country = request.Country,
                    CountryCode = request.CountryCode,
                    // Geoapify normalization fields
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    GeoapifyPlaceId = request.GeoapifyPlaceId,
                    FormattedAddress = request.FormattedAddress,
                    NormalizedHash = normalizedHash
                };

                _context.Addresses.Add(address);
                addressId = address.Id;
            }
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

        var contactAddress = new ContactAddress
        {
            Id = Guid.NewGuid(),
            TenantId = contact.TenantId,
            ContactId = contactId,
            AddressId = addressId,
            Tag = request.Tag,
            IsPrimary = request.IsPrimary
        };

        _context.ContactAddresses.Add(contactAddress);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.AddressAdded, null,
            JsonSerializer.Serialize(new { AddressId = addressId, Tag = request.Tag.ToString() }),
            "Address added", ct);

        // Reload with address included
        var result = await _context.ContactAddresses
            .Include(ca => ca.Address)
            .FirstAsync(ca => ca.Id == contactAddress.Id, ct);

        return _mapper.Map<ContactAddressDto>(result);
    }

    /// <inheritdoc />
    public async Task RemoveAddressAsync(Guid contactId, Guid contactAddressId, CancellationToken ct = default)
    {
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
        var contactAddress = await _context.ContactAddresses
            .Include(ca => ca.Address)
            .FirstOrDefaultAsync(ca => ca.Id == contactAddressId && ca.ContactId == contactId, ct)
            ?? throw new EntityNotFoundException(nameof(ContactAddress), contactAddressId);

        var currentAddress = contactAddress.Address;
        var currentPlaceId = currentAddress.GeoapifyPlaceId;
        var newPlaceId = request.GeoapifyPlaceId;

        // Determine if we need to change the linked address
        bool needsNewAddress = false;

        // Generate hash for the new address data
        var normalizedHash = GenerateAddressHash(request);

        // If the GeoapifyPlaceId changed, we need to handle address linking
        if (!string.IsNullOrEmpty(newPlaceId) && newPlaceId != currentPlaceId)
        {
            // Check if an address with the new PlaceId already exists
            var existingByPlaceId = await _context.Addresses
                .FirstOrDefaultAsync(a => a.GeoapifyPlaceId == newPlaceId, ct);

            if (existingByPlaceId != null)
            {
                // Link to the existing address
                contactAddress.AddressId = existingByPlaceId.Id;
                _logger.LogInformation("Re-linking contact address to existing address with GeoapifyPlaceId {PlaceId}", newPlaceId);
            }
            else
            {
                // Also check by NormalizedHash
                Address? existingByHash = null;
                if (!string.IsNullOrEmpty(normalizedHash))
                {
                    existingByHash = await _context.Addresses
                        .FirstOrDefaultAsync(a => a.NormalizedHash == normalizedHash, ct);
                }

                if (existingByHash != null)
                {
                    // Link to the existing address found by hash
                    contactAddress.AddressId = existingByHash.Id;
                    _logger.LogInformation("Re-linking contact address to existing address with NormalizedHash {Hash}", normalizedHash);
                }
                else
                {
                    // Check if current address is shared with other ContactAddresses
                    var addressUsageCount = await _context.ContactAddresses
                        .CountAsync(ca => ca.AddressId == currentAddress.Id, ct);

                    if (addressUsageCount > 1)
                    {
                        // Address is shared, create a new one
                        needsNewAddress = true;
                    }
                    else
                    {
                        // Update the existing address (it's only used by this contact)
                        UpdateAddressEntity(currentAddress, request, normalizedHash);
                    }
                }
            }
        }
        else
        {
            // No PlaceId change, check if address is shared
            var addressUsageCount = await _context.ContactAddresses
                .CountAsync(ca => ca.AddressId == currentAddress.Id, ct);

            if (addressUsageCount > 1)
            {
                // Address is shared, check if we can link to an existing one
                Address? existingByHash = null;
                if (!string.IsNullOrEmpty(normalizedHash))
                {
                    existingByHash = await _context.Addresses
                        .FirstOrDefaultAsync(a => a.NormalizedHash == normalizedHash && a.Id != currentAddress.Id, ct);
                }

                if (existingByHash != null)
                {
                    // Link to the existing address found by hash
                    contactAddress.AddressId = existingByHash.Id;
                    _logger.LogInformation("Re-linking contact address to existing address with NormalizedHash {Hash}", normalizedHash);
                }
                else
                {
                    // Create a new one for this contact
                    needsNewAddress = true;
                }
            }
            else
            {
                // Update the existing address (it's only used by this contact)
                UpdateAddressEntity(currentAddress, request, normalizedHash);
            }
        }

        if (needsNewAddress)
        {
            // Create new address since the current one is shared
            var newAddress = new Address
            {
                Id = Guid.NewGuid(),
                AddressLine1 = request.AddressLine1,
                AddressLine2 = request.AddressLine2,
                AddressLine3 = request.AddressLine3,
                AddressLine4 = request.AddressLine4,
                City = request.City,
                StateProvince = request.StateProvince,
                PostalCode = request.PostalCode,
                Country = request.Country,
                CountryCode = request.CountryCode,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                GeoapifyPlaceId = request.GeoapifyPlaceId,
                FormattedAddress = request.FormattedAddress,
                NormalizedHash = normalizedHash
            };
            _context.Addresses.Add(newAddress);
            contactAddress.AddressId = newAddress.Id;
            _logger.LogInformation("Created new address for contact since previous address was shared");
        }

        // Update the contact address entity
        contactAddress.Tag = request.Tag;

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

        return _mapper.Map<ContactAddressDto>(contactAddress);
    }

    /// <inheritdoc />
    public async Task<List<ContactAddressDto>> GetAddressesAsync(Guid contactId, CancellationToken ct = default)
    {
        var addresses = await _context.ContactAddresses
            .Include(ca => ca.Address)
            .Where(ca => ca.ContactId == contactId)
            .OrderByDescending(ca => ca.IsPrimary)
            .ThenBy(ca => ca.CreatedAt)
            .ToListAsync(ct);

        return _mapper.Map<List<ContactAddressDto>>(addresses);
    }

    private static void UpdateAddressEntity(Address address, AddContactAddressRequest request, string? normalizedHash = null)
    {
        address.AddressLine1 = request.AddressLine1;
        address.AddressLine2 = request.AddressLine2;
        address.AddressLine3 = request.AddressLine3;
        address.AddressLine4 = request.AddressLine4;
        address.City = request.City;
        address.StateProvince = request.StateProvince;
        address.PostalCode = request.PostalCode;
        address.Country = request.Country;
        address.CountryCode = request.CountryCode;
        address.Latitude = request.Latitude;
        address.Longitude = request.Longitude;
        address.GeoapifyPlaceId = request.GeoapifyPlaceId;
        address.FormattedAddress = request.FormattedAddress;
        address.NormalizedHash = normalizedHash;
    }

    #endregion

    #region Phone Management

    /// <inheritdoc />
    public async Task<ContactPhoneNumberDto> AddPhoneAsync(Guid contactId, AddPhoneRequest request, CancellationToken ct = default)
    {
        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

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

        return _mapper.Map<ContactPhoneNumberDto>(phone);
    }

    /// <inheritdoc />
    public async Task<ContactPhoneNumberDto> UpdatePhoneAsync(Guid phoneId, AddPhoneRequest request, CancellationToken ct = default)
    {
        var phone = await _context.ContactPhoneNumbers.FindAsync(new object[] { phoneId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactPhoneNumber), phoneId);

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

        return _mapper.Map<ContactPhoneNumberDto>(phone);
    }

    /// <inheritdoc />
    public async Task RemovePhoneAsync(Guid phoneId, CancellationToken ct = default)
    {
        var phone = await _context.ContactPhoneNumbers.FindAsync(new object[] { phoneId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactPhoneNumber), phoneId);

        var contactId = phone.ContactId;
        _context.ContactPhoneNumbers.Remove(phone);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.PhoneRemoved, null, null, "Phone number removed", ct);
    }

    /// <inheritdoc />
    public async Task SetPrimaryPhoneAsync(Guid contactId, Guid phoneId, CancellationToken ct = default)
    {
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
        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

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

        return _mapper.Map<ContactSocialMediaDto>(social);
    }

    /// <inheritdoc />
    public async Task<ContactSocialMediaDto> UpdateSocialMediaAsync(Guid socialMediaId, AddSocialMediaRequest request, CancellationToken ct = default)
    {
        var social = await _context.ContactSocialMedia.FindAsync(new object[] { socialMediaId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactSocialMedia), socialMediaId);

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

        return _mapper.Map<ContactSocialMediaDto>(social);
    }

    /// <inheritdoc />
    public async Task RemoveSocialMediaAsync(Guid socialMediaId, CancellationToken ct = default)
    {
        var social = await _context.ContactSocialMedia.FindAsync(new object[] { socialMediaId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactSocialMedia), socialMediaId);

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
        var sourceContact = await _context.Contacts.FindAsync(new object[] { sourceContactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), sourceContactId);

        var targetContact = await _context.Contacts.FindAsync(new object[] { request.TargetContactId }, ct)
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

        // Create inverse relationship if requested
        if (request.CreateInverse)
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

        return _mapper.Map<ContactRelationshipDto>(result);
    }

    /// <inheritdoc />
    public async Task RemoveRelationshipAsync(Guid relationshipId, bool removeInverse = true, CancellationToken ct = default)
    {
        var relationship = await _context.ContactRelationships.FindAsync(new object[] { relationshipId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactRelationship), relationshipId);

        var sourceContactId = relationship.SourceContactId;
        var targetContactId = relationship.TargetContactId;

        _context.ContactRelationships.Remove(relationship);

        // Remove inverse if requested
        if (removeInverse)
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
        var relationships = await _context.ContactRelationships
            .Include(r => r.TargetContact)
            .Where(r => r.SourceContactId == contactId)
            .OrderBy(r => r.RelationshipType)
            .ToListAsync(ct);

        return _mapper.Map<List<ContactRelationshipDto>>(relationships);
    }

    /// <inheritdoc />
    public async Task<List<ContactRelationshipDto>> GetInverseRelationshipsAsync(Guid contactId, CancellationToken ct = default)
    {
        var relationships = await _context.ContactRelationships
            .Include(r => r.SourceContact)
            .Where(r => r.TargetContactId == contactId)
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

        var tag = _mapper.Map<ContactTag>(request);
        tag.Id = Guid.NewGuid();
        tag.TenantId = tenantId;

        _context.ContactTags.Add(tag);
        await _context.SaveChangesAsync(ct);

        return _mapper.Map<ContactTagDto>(tag);
    }

    /// <inheritdoc />
    public async Task<ContactTagDto?> GetTagByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await _context.ContactTags
            .Include(t => t.Contacts)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return tag == null ? null : _mapper.Map<ContactTagDto>(tag);
    }

    /// <inheritdoc />
    public async Task<List<ContactTagDto>> ListTagsAsync(CancellationToken ct = default)
    {
        var tags = await _context.ContactTags
            .Include(t => t.Contacts)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return _mapper.Map<List<ContactTagDto>>(tags);
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

        _mapper.Map(request, tag);
        await _context.SaveChangesAsync(ct);

        return _mapper.Map<ContactTagDto>(tag);
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
        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

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
        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

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
        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

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

        return _mapper.Map<ContactUserShareDto>(result);
    }

    /// <inheritdoc />
    public async Task<ContactUserShareDto> UpdateShareAsync(Guid shareId, bool canEdit, CancellationToken ct = default)
    {
        var share = await _context.ContactUserShares
            .Include(s => s.SharedWithUser)
            .FirstOrDefaultAsync(s => s.Id == shareId, ct)
            ?? throw new EntityNotFoundException(nameof(ContactUserShare), shareId);

        share.CanEdit = canEdit;
        await _context.SaveChangesAsync(ct);

        return _mapper.Map<ContactUserShareDto>(share);
    }

    /// <inheritdoc />
    public async Task RemoveShareAsync(Guid shareId, CancellationToken ct = default)
    {
        var share = await _context.ContactUserShares.FindAsync(new object[] { shareId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactUserShare), shareId);

        var contactId = share.ContactId;
        _context.ContactUserShares.Remove(share);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.UnsharedFromUser, null, null, "Contact unshared", ct);
    }

    /// <inheritdoc />
    public async Task<List<ContactUserShareDto>> GetSharesAsync(Guid contactId, CancellationToken ct = default)
    {
        var shares = await _context.ContactUserShares
            .Include(s => s.SharedWithUser)
            .Where(s => s.ContactId == contactId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        return _mapper.Map<List<ContactUserShareDto>>(shares);
    }

    #endregion

    #region Email Management

    /// <inheritdoc />
    public async Task<ContactEmailAddressDto> AddEmailAsync(Guid contactId, AddEmailRequest request, CancellationToken ct = default)
    {
        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

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

        return _mapper.Map<ContactEmailAddressDto>(email);
    }

    /// <inheritdoc />
    public async Task<ContactEmailAddressDto> UpdateEmailAsync(Guid emailId, AddEmailRequest request, CancellationToken ct = default)
    {
        var email = await _context.ContactEmailAddresses.FindAsync(new object[] { emailId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactEmailAddress), emailId);

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

        return _mapper.Map<ContactEmailAddressDto>(email);
    }

    /// <inheritdoc />
    public async Task RemoveEmailAsync(Guid emailId, CancellationToken ct = default)
    {
        var email = await _context.ContactEmailAddresses.FindAsync(new object[] { emailId }, ct)
            ?? throw new EntityNotFoundException(nameof(ContactEmailAddress), emailId);

        var contactId = email.ContactId;
        _context.ContactEmailAddresses.Remove(email);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(contactId, ContactAuditAction.EmailRemoved, null, null, "Email address removed", ct);
    }

    /// <inheritdoc />
    public async Task SetPrimaryEmailAsync(Guid contactId, Guid emailId, CancellationToken ct = default)
    {
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
        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

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
        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

        if (!string.IsNullOrEmpty(contact.ProfileImageFileName))
        {
            await _fileStorageService.DeleteContactProfileImageAsync(contactId, contact.ProfileImageFileName, ct);
            contact.ProfileImageFileName = null;
            await _context.SaveChangesAsync(ct);

            await LogAuditAsync(contactId, ContactAuditAction.ProfileImageRemoved, null, null, "Profile image removed", ct);
        }
    }

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

        return _mapper.Map<List<ContactAuditLogDto>>(logs);
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

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(c =>
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(searchTerm)) ||
                c.Members.Any(m =>
                    (m.FirstName != null && m.FirstName.ToLower().Contains(searchTerm)) ||
                    (m.LastName != null && m.LastName.ToLower().Contains(searchTerm))));
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
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(c => new ContactGroupSummaryDto
        {
            Id = c.Id,
            ContactType = c.ContactType ?? Domain.Enums.ContactType.Household,
            GroupName = c.CompanyName ?? "Unknown",
            MemberCount = c.Members.Count,
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
        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, ct)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

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

    /// <summary>
    /// Generates a normalized hash for an address for duplicate detection.
    /// Format: lowercase, trimmed components joined by pipe: line1|city|state|postal|country
    /// </summary>
    private static string? GenerateAddressHash(AddContactAddressRequest request)
    {
        var parts = new[]
        {
            request.AddressLine1?.Trim().ToLowerInvariant(),
            request.City?.Trim().ToLowerInvariant(),
            request.StateProvince?.Trim().ToLowerInvariant(),
            request.PostalCode?.Trim().ToLowerInvariant(),
            request.Country?.Trim().ToLowerInvariant()
        };

        var combined = string.Join("|", parts.Where(p => !string.IsNullOrEmpty(p)));
        return string.IsNullOrEmpty(combined) ? null : combined;
    }

    #endregion
}
