using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.Home;
using Famick.HomeManagement.Core.DTOs.Vehicles;
using Famick.HomeManagement.Core.DTOs.Wizard;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class WizardService : IWizardService
{
    private readonly HomeManagementDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IContactService _contactService;
    private readonly IUserManagementService _userManagementService;
    private readonly IMapper _mapper;
    private readonly ILogger<WizardService> _logger;

    public WizardService(
        HomeManagementDbContext context,
        ITenantProvider tenantProvider,
        IContactService contactService,
        IUserManagementService userManagementService,
        IMapper mapper,
        ILogger<WizardService> logger)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _contactService = contactService;
        _userManagementService = userManagementService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<WizardStateDto> GetWizardStateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting wizard state for current tenant");

        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException("Tenant ID is required");
        }

        // Get tenant info (Page 1)
        var tenant = await _context.Tenants
            .Include(t => t.Address)
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);

        // Get home info (Page 3)
        var home = await _context.Homes
            .Include(h => h.PropertyLinks)
            .FirstOrDefaultAsync(cancellationToken);

        // Get household members (Page 2)
        var members = await GetHouseholdMembersAsync(cancellationToken);

        // Get vehicles (Page 5)
        var vehicles = await _context.Vehicles
            .Include(v => v.PrimaryDriver)
            .Where(v => v.IsActive)
            .OrderBy(v => v.Year)
            .ThenBy(v => v.Make)
            .ThenBy(v => v.Model)
            .ToListAsync(cancellationToken);

        return new WizardStateDto
        {
            IsComplete = home?.IsSetupComplete ?? false,
            HouseholdInfo = new HouseholdInfoDto
            {
                TenantId = tenantId.Value,
                Name = tenant?.Name ?? string.Empty,
                Street1 = tenant?.Address?.AddressLine1,
                Street2 = tenant?.Address?.AddressLine2,
                City = tenant?.Address?.City,
                State = tenant?.Address?.StateProvince,
                PostalCode = tenant?.Address?.PostalCode,
                Country = tenant?.Address?.Country,
                IsAddressNormalized = !string.IsNullOrEmpty(tenant?.Address?.NormalizedHash)
            },
            HouseholdMembers = members,
            HomeStatistics = new HomeStatisticsDto
            {
                HomeId = home?.Id,
                SquareFootage = home?.SquareFootage,
                YearBuilt = home?.YearBuilt,
                Bedrooms = home?.Bedrooms,
                Bathrooms = home?.Bathrooms,
                Unit = home?.Unit,
                HoaName = home?.HoaName,
                HoaContactInfo = home?.HoaContactInfo,
                HoaRulesLink = home?.HoaRulesLink,
                PropertyLinks = home?.PropertyLinks
                    .OrderBy(p => p.SortOrder)
                    .Select(p => new PropertyLinkDto
                    {
                        Id = p.Id,
                        HomeId = p.HomeId,
                        Url = p.Url,
                        Label = p.Label,
                        SortOrder = p.SortOrder,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .ToList() ?? new List<PropertyLinkDto>()
            },
            MaintenanceItems = new MaintenanceItemsDto
            {
                AcFilterSizes = home?.AcFilterSizes,
                FridgeWaterFilterType = home?.FridgeWaterFilterType,
                UnderSinkFilterType = home?.UnderSinkFilterType,
                WholeHouseFilterType = home?.WholeHouseFilterType,
                SmokeCoDetectorBatteryType = home?.SmokeCoDetectorBatteryType
            },
            Vehicles = vehicles.Select(v => new VehicleSummaryDto
            {
                Id = v.Id,
                Year = v.Year,
                Make = v.Make,
                Model = v.Model,
                Trim = v.Trim,
                LicensePlate = v.LicensePlate,
                Color = v.Color,
                CurrentMileage = v.CurrentMileage,
                PrimaryDriverName = v.PrimaryDriver?.DisplayName,
                IsActive = v.IsActive,
                DisplayName = v.DisplayName
            }).ToList()
        };
    }

    public async Task<List<HouseholdMemberDto>> GetHouseholdMembersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting household members");

        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException("Tenant ID is required");
        }

        // Get contacts that belong to this household
        var contacts = await _context.Contacts
            .Include(c => c.LinkedUser)
            .Where(c => c.HouseholdTenantId == tenantId.Value || c.TenantId == tenantId.Value)
            .Where(c => c.IsActive)
            .OrderBy(c => c.FirstName)
            .ThenBy(c => c.LastName)
            .ToListAsync(cancellationToken);

        // Get current user's contact to determine which one is "self"
        var currentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId.Value, cancellationToken);

        // Load relationships where the current user's contact is the source
        var currentUserContact = currentUser != null
            ? contacts.FirstOrDefault(c => c.LinkedUserId == currentUser.Id)
            : null;

        var contactIds = contacts.Select(c => c.Id).ToList();
        var relationships = currentUserContact != null
            ? await _context.ContactRelationships
                .Where(r => r.SourceContactId == currentUserContact.Id && contactIds.Contains(r.TargetContactId))
                .ToListAsync(cancellationToken)
            : new List<ContactRelationship>();

        var relationshipByTarget = relationships.ToDictionary(r => r.TargetContactId, r => r.RelationshipType);

        return contacts.Select(c =>
        {
            var isCurrentUser = c.LinkedUserId.HasValue && c.LinkedUserId == currentUser?.Id;
            relationshipByTarget.TryGetValue(c.Id, out var relType);

            return new HouseholdMemberDto
            {
                ContactId = c.Id,
                FirstName = c.FirstName ?? string.Empty,
                LastName = c.LastName,
                DisplayName = c.DisplayName,
                ProfileImageFileName = c.ProfileImageFileName,
                RelationshipType = isCurrentUser ? "Self" : (relationshipByTarget.ContainsKey(c.Id) ? relType.ToString() : null),
                IsCurrentUser = isCurrentUser,
                HasUserAccount = c.LinkedUserId.HasValue,
                Email = c.LinkedUser?.Email
            };
        }).ToList();
    }

    public async Task<HouseholdMemberDto> SaveCurrentUserContactAsync(
        SaveCurrentUserContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue)
            throw new InvalidOperationException("Tenant ID is required");

        var currentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId.Value, cancellationToken);
        if (currentUser == null)
            throw new InvalidOperationException("Current user not found");

        // Update user name
        currentUser.FirstName = request.FirstName.Trim();
        currentUser.LastName = request.LastName?.Trim() ?? string.Empty;
        currentUser.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var existingContact = await _context.Contacts
            .FirstOrDefaultAsync(c => c.LinkedUserId == currentUser.Id, cancellationToken);

        // Get tenant household for linking
        var tenantHousehold = await _context.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId.Value && c.IsTenantHousehold, cancellationToken);

        if (existingContact == null)
        {
            // Use ContactService to create contact linked to user (handles email, address, user.ContactId)
            var contactDto = await _contactService.CreateContactForUserAsync(currentUser, cancellationToken);

            // Set household membership
            var contact = await _context.Contacts.FindAsync(new object[] { contactDto.Id }, cancellationToken);
            contact!.HouseholdTenantId = tenantId.Value;
            contact.ParentContactId = tenantHousehold?.Id;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Current user contact created: {ContactId}", contactDto.Id);

            return new HouseholdMemberDto
            {
                ContactId = contactDto.Id,
                FirstName = contactDto.FirstName ?? string.Empty,
                LastName = contactDto.LastName,
                DisplayName = contactDto.DisplayName,
                RelationshipType = "Self",
                IsCurrentUser = true,
                HasUserAccount = true,
                Email = currentUser.Email
            };
        }
        else
        {
            // Update existing contact
            existingContact.FirstName = request.FirstName.Trim();
            existingContact.LastName = request.LastName?.Trim();
            existingContact.HouseholdTenantId = tenantId.Value;
            existingContact.UsesTenantAddress = true;
            existingContact.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            // Ensure bidirectional link exists
            if (currentUser.ContactId != existingContact.Id)
            {
                await _userManagementService.LinkContactAsync(currentUser.Id, existingContact.Id, cancellationToken);
            }

            _logger.LogInformation("Current user contact updated: {ContactId}", existingContact.Id);

            return new HouseholdMemberDto
            {
                ContactId = existingContact.Id,
                FirstName = existingContact.FirstName ?? string.Empty,
                LastName = existingContact.LastName,
                DisplayName = existingContact.DisplayName,
                RelationshipType = "Self",
                IsCurrentUser = true,
                HasUserAccount = true,
                Email = currentUser.Email
            };
        }
    }

    public async Task<HouseholdMemberDto> AddHouseholdMemberAsync(
        AddHouseholdMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding household member: {FirstName} {LastName}", request.FirstName, request.LastName);

        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue)
            throw new InvalidOperationException("Tenant ID is required");

        // Get current user and their contact for relationship creation
        var currentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId.Value, cancellationToken);
        if (currentUser == null)
            throw new InvalidOperationException("Current user not found");

        var currentUserContact = await _context.Contacts
            .FirstOrDefaultAsync(c => c.LinkedUserId == currentUser.Id, cancellationToken);

        // Parse relationship type
        RelationshipType? relationshipType = null;
        if (!string.IsNullOrEmpty(request.RelationshipType) &&
            Enum.TryParse<RelationshipType>(request.RelationshipType, out var parsed))
        {
            relationshipType = parsed;
        }

        Guid contactId;
        string? firstName, lastName, displayName, profileImage;
        bool hasUserAccount;

        // Get tenant household group for linking
        var tenantHousehold = await _context.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId.Value && c.IsTenantHousehold, cancellationToken);

        if (request.ExistingContactId.HasValue)
        {
            // Link existing contact to household
            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == request.ExistingContactId.Value, cancellationToken)
                ?? throw new EntityNotFoundException(nameof(Contact), request.ExistingContactId.Value);

            contact.HouseholdTenantId = tenantId.Value;
            contact.UsesTenantAddress = true;
            contact.ParentContactId = tenantHousehold?.Id;
            await _context.SaveChangesAsync(cancellationToken);

            contactId = contact.Id;
            firstName = contact.FirstName;
            lastName = contact.LastName;
            displayName = contact.DisplayName;
            profileImage = contact.ProfileImageFileName;
            hasUserAccount = contact.LinkedUserId.HasValue;
        }
        else
        {
            // Create new contact directly (avoid ContactService.CreateAsync which requires
            // UserId from ITenantProvider claims — not always available in wizard context)
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Gender = relationshipType.HasValue
                    ? RelationshipMapper.InferGender(relationshipType.Value)
                    : Gender.Unknown,
                CreatedByUserId = currentUser.Id,
                IsActive = true,
                HouseholdTenantId = tenantId.Value,
                UsesTenantAddress = true,
                TenantId = tenantId.Value,
                ParentContactId = tenantHousehold?.Id
            };

            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync(cancellationToken);

            contactId = contact.Id;
            firstName = contact.FirstName;
            lastName = contact.LastName;
            displayName = contact.DisplayName;
            profileImage = contact.ProfileImageFileName;
            hasUserAccount = false;
        }

        // Create relationship (with automatic inverse) via ContactService
        if (relationshipType.HasValue && currentUserContact != null)
        {
            await _contactService.AddRelationshipAsync(currentUserContact.Id, new AddRelationshipRequest
            {
                TargetContactId = contactId,
                RelationshipType = relationshipType.Value,
                CreateInverse = true
            }, cancellationToken);
        }

        _logger.LogInformation("Household member added: {ContactId}", contactId);

        return new HouseholdMemberDto
        {
            ContactId = contactId,
            FirstName = firstName ?? string.Empty,
            LastName = lastName,
            DisplayName = displayName,
            ProfileImageFileName = profileImage,
            RelationshipType = request.RelationshipType,
            IsCurrentUser = false,
            HasUserAccount = hasUserAccount
        };
    }

    public async Task<HouseholdMemberDto> UpdateHouseholdMemberAsync(
        Guid contactId,
        UpdateHouseholdMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating household member: {ContactId}", contactId);

        var contact = await _context.Contacts
            .Include(c => c.LinkedUser)
            .FirstOrDefaultAsync(c => c.Id == contactId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

        var tenantId = _tenantProvider.TenantId;
        var currentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId!.Value, cancellationToken);

        var currentUserContact = currentUser != null
            ? await _context.Contacts.FirstOrDefaultAsync(c => c.LinkedUserId == currentUser.Id, cancellationToken)
            : null;

        // Parse new relationship type
        RelationshipType? newRelType = null;
        if (!string.IsNullOrEmpty(request.RelationshipType) &&
            Enum.TryParse<RelationshipType>(request.RelationshipType, out var parsed))
        {
            newRelType = parsed;
        }

        // Update gender inference
        if (newRelType.HasValue)
        {
            contact.Gender = RelationshipMapper.InferGender(newRelType.Value);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Update relationships via ContactService
        if (currentUserContact != null)
        {
            // Remove existing relationships between these two contacts
            var existingRelationships = await _context.ContactRelationships
                .Where(r =>
                    (r.SourceContactId == currentUserContact.Id && r.TargetContactId == contactId) ||
                    (r.SourceContactId == contactId && r.TargetContactId == currentUserContact.Id))
                .ToListAsync(cancellationToken);

            foreach (var rel in existingRelationships)
            {
                await _contactService.RemoveRelationshipAsync(rel.Id, removeInverse: false, cancellationToken);
            }

            // Add new relationship with automatic inverse
            if (newRelType.HasValue)
            {
                await _contactService.AddRelationshipAsync(currentUserContact.Id, new AddRelationshipRequest
                {
                    TargetContactId = contactId,
                    RelationshipType = newRelType.Value,
                    CreateInverse = true
                }, cancellationToken);
            }
        }

        return new HouseholdMemberDto
        {
            ContactId = contact.Id,
            FirstName = contact.FirstName ?? string.Empty,
            LastName = contact.LastName,
            DisplayName = contact.DisplayName,
            ProfileImageFileName = contact.ProfileImageFileName,
            RelationshipType = request.RelationshipType,
            IsCurrentUser = contact.LinkedUserId.HasValue && contact.LinkedUserId == currentUser?.Id,
            HasUserAccount = contact.LinkedUserId.HasValue,
            Email = contact.LinkedUser?.Email
        };
    }

    public async Task RemoveHouseholdMemberAsync(
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing household member: {ContactId}", contactId);

        var contact = await _context.Contacts
            .FirstOrDefaultAsync(c => c.Id == contactId, cancellationToken);

        if (contact == null)
        {
            throw new EntityNotFoundException(nameof(Contact), contactId);
        }

        // Unlink from household instead of deleting
        contact.HouseholdTenantId = null;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Household member removed: {ContactId}", contactId);
    }

    public async Task<DuplicateContactResultDto> CheckDuplicateContactAsync(
        CheckDuplicateContactRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking for duplicate contacts: {FirstName} {LastName}", request.FirstName, request.LastName);

        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException("Tenant ID is required");
        }

        // Search for contacts with matching name
        var query = _context.Contacts
            .Where(c => c.IsActive);

        // Exact match on first name (case-insensitive)
        query = query.Where(c => c.FirstName != null &&
            c.FirstName.ToLower() == request.FirstName.ToLower());

        // If last name provided, filter by it too
        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            query = query.Where(c => c.LastName != null &&
                c.LastName.ToLower() == request.LastName.ToLower());
        }

        var matches = await query
            .Take(10)
            .ToListAsync(cancellationToken);

        return new DuplicateContactResultDto
        {
            HasDuplicates = matches.Count != 0,
            Matches = matches.Select(c => new DuplicateContactMatchDto
            {
                ContactId = c.Id,
                FirstName = c.FirstName ?? string.Empty,
                LastName = c.LastName,
                DisplayName = c.DisplayName,
                ProfileImageFileName = c.ProfileImageFileName,
                IsHouseholdMember = c.HouseholdTenantId == tenantId.Value,
                MatchType = "Exact"
            }).ToList()
        };
    }

    public async Task SaveHouseholdInfoAsync(HouseholdInfoDto info, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving household info for wizard");

        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue)
            throw new InvalidOperationException("Tenant ID is required");

        var tenant = await _context.Tenants
            .Include(t => t.Address)
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);

        if (tenant == null)
            throw new EntityNotFoundException("Tenant", tenantId.Value);

        tenant.Name = info.Name;

        if (tenant.Address == null)
        {
            var address = new Address { Id = Guid.NewGuid() };
            _context.Addresses.Add(address);
            tenant.Address = address;
            tenant.AddressId = address.Id;
        }

        tenant.Address.AddressLine1 = info.Street1;
        tenant.Address.AddressLine2 = info.Street2;
        tenant.Address.City = info.City;
        tenant.Address.StateProvince = info.State;
        tenant.Address.PostalCode = info.PostalCode;
        tenant.Address.Country = info.Country;

        await _context.SaveChangesAsync(cancellationToken);

        // Ensure tenant household contact group exists
        await _contactService.EnsureTenantHouseholdAsync(info.Name + " Household", cancellationToken);
    }

    public async Task SaveHomeStatisticsAsync(HomeStatisticsDto stats, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving home statistics for wizard");

        var home = await _context.Homes.FirstOrDefaultAsync(cancellationToken);

        if (home == null)
        {
            home = new Home { Id = Guid.NewGuid() };
            _context.Homes.Add(home);
        }

        home.Unit = stats.Unit;
        home.YearBuilt = stats.YearBuilt;
        home.SquareFootage = stats.SquareFootage;
        home.Bedrooms = stats.Bedrooms;
        home.Bathrooms = stats.Bathrooms;
        home.HoaName = stats.HoaName;
        home.HoaContactInfo = stats.HoaContactInfo;
        home.HoaRulesLink = stats.HoaRulesLink;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveMaintenanceItemsAsync(MaintenanceItemsDto items, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving maintenance items for wizard");

        var home = await _context.Homes.FirstOrDefaultAsync(cancellationToken);

        if (home == null)
        {
            home = new Home { Id = Guid.NewGuid() };
            _context.Homes.Add(home);
        }

        home.AcFilterSizes = items.AcFilterSizes;
        home.FridgeWaterFilterType = items.FridgeWaterFilterType;
        home.UnderSinkFilterType = items.UnderSinkFilterType;
        home.WholeHouseFilterType = items.WholeHouseFilterType;
        home.SmokeCoDetectorBatteryType = items.SmokeCoDetectorBatteryType;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteWizardAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Completing wizard");

        var home = await _context.Homes.FirstOrDefaultAsync(cancellationToken);

        if (home == null)
        {
            // Create minimal home record if it doesn't exist
            home = new Home
            {
                Id = Guid.NewGuid(),
                IsSetupComplete = true
            };
            _context.Homes.Add(home);
        }
        else
        {
            home.IsSetupComplete = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Wizard completed");
    }
}
