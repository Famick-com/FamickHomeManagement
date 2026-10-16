using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Creates a household member's user, roles and contact. Shared by an admin adding a member
/// directly (a server with no mail) and by someone accepting an invitation.
/// </summary>
public class HouseholdMemberFactory
{
    private readonly HomeManagementDbContext _context;
    private readonly IContactService _contactService;
    private readonly ILogger<HouseholdMemberFactory> _logger;

    public HouseholdMemberFactory(
        HomeManagementDbContext context,
        IContactService contactService,
        ILogger<HouseholdMemberFactory> logger)
    {
        _context = context;
        _contactService = contactService;
        _logger = logger;
    }

    /// <param name="email">Already normalised.</param>
    /// <param name="contactId">An existing contact in the household to link, or null to create one.</param>
    public async Task<User> CreateAsync(
        Guid tenantId,
        string email,
        string firstName,
        string lastName,
        string passwordHash,
        bool mustChangePassword,
        IEnumerable<Role> roles,
        Guid? contactId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            Username = email,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PasswordHash = passwordHash,
            IsActive = true,
            MustChangePassword = mustChangePassword,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Users.Add(user);

        foreach (var role in roles)
        {
            _context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                Role = role,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User created: {Email}, ID: {UserId}", email, user.Id);

        // Link existing contact or create a new one
        try
        {
            if (contactId.HasValue)
            {
                var existingContact = await _context.Contacts
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Id == contactId.Value && c.TenantId == tenantId, cancellationToken);
                if (existingContact != null)
                {
                    existingContact.LinkedUserId = user.Id;
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Linked existing contact {ContactId} to user {UserId}", existingContact.Id, user.Id);
                }
                else
                {
                    _logger.LogWarning("Contact {ContactId} not found, creating new contact for user {UserId}", contactId.Value, user.Id);
                    await _contactService.CreateContactForUserAsync(user, cancellationToken);
                }
            }
            else
            {
                await _contactService.CreateContactForUserAsync(user, cancellationToken);
                _logger.LogInformation("Contact created for user: {UserId}", user.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create/link contact for user {UserId}", user.Id);
            // Don't fail user creation if contact creation fails
        }

        return user;
    }
}
