using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Validators.Common;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for managing users (Admin only)
/// </summary>
public class UserManagementService : IUserManagementService
{
    private readonly HomeManagementDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHouseholdInvitationService _invitations;
    private readonly HouseholdMemberFactory _memberFactory;
    private readonly IFileStorageService? _storage;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        HomeManagementDbContext context,
        IPasswordHasher passwordHasher,
        ITenantProvider tenantProvider,
        IHouseholdInvitationService invitations,
        HouseholdMemberFactory memberFactory,
        ILogger<UserManagementService> logger,
        IFileStorageService? storage = null)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tenantProvider = tenantProvider;
        _invitations = invitations;
        _memberFactory = memberFactory;
        _storage = storage;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ManagedUserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .Include(u => u.UserRoles)
            .Include(u => u.Contact)
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

        return users.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ManagedUserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .Include(u => u.Contact)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user == null ? null : MapToDto(user);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A server that can send email invites the person instead of creating their account, so
    /// the admin never sets a password for someone else and never learns whether the address
    /// already has an account. Without mail (a self-hosted install with none set up) the account
    /// is created here, with a password the admin hands over and the member must change.
    /// </remarks>
    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, string baseUrl, CancellationToken cancellationToken = default)
    {
        if (_invitations.InvitationsEnabled)
        {
            var invitation = await _invitations.InviteAsync(request, baseUrl, cancellationToken);
            return new CreateUserResponse
            {
                UserId = Guid.Empty,
                Email = invitation.Email,
                InvitationId = invitation.Id,
                WelcomeEmailSent = true
            };
        }

        var email = EmailAddress.Normalize(request.Email);

        // Emails are unique across every household, not just this one.
        var exists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException("User", "Email", email);
        }

        // Generate password if not provided
        string password;
        string? generatedPassword = null;
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            password = PasswordRules.Generate();
            generatedPassword = password; // Return to admin
        }
        else
        {
            PasswordRules.Ensure(request.Password, nameof(request.Password));
            password = request.Password;
        }

        // No fallback. This one value stamps the users row, every user_roles row, and scopes the
        // contacts lookup further down, so a wrong-but-plausible tenant here files a whole member
        // into another household and silently fails to link the contact the admin picked. On a
        // multi-tenant host an absent tenant means something upstream is broken rather than that
        // a default is wanted: the request only reaches here through
        // [Authorize(Policy = "RequireAdmin")], so a cloud-minted token carrying a tenant_id
        // claim exists by construction, and a 500 is the honest answer. Self-hosted's
        // FixedTenantProvider never returns null.
        var tenantId = _tenantProvider.TenantId
            ?? throw new InvalidOperationException(
                "No tenant is resolved for this request, so a user cannot be created. This is a " +
                "broken tenant resolution upstream, not a client error.");

        var user = await _memberFactory.CreateAsync(
            tenantId,
            email,
            request.FirstName,
            request.LastName,
            _passwordHasher.HashPassword(password),
            // With no mail there is no link to set a password from, so the member must replace
            // the one the admin handed over.
            mustChangePassword: true,
            request.Roles,
            request.ContactId,
            cancellationToken);

        return new CreateUserResponse
        {
            UserId = user.Id,
            Email = email,
            GeneratedPassword = generatedPassword,
            WelcomeEmailSent = false
        };
    }

    /// <inheritdoc />
    public async Task<ManagedUserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        // A member's sign-in email is fixed once their account exists; it is not editable here.
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        if (user.IsActive && !request.IsActive)
            await FeedTokens.RevokeAllForUserAsync(_context, user.Id, cancellationToken);
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        // Update roles - remove old and add new
        var existingRoles = user.UserRoles.ToList();
        _context.UserRoles.RemoveRange(existingRoles);

        foreach (var role in request.Roles)
        {
            var userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                Role = role,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserRoles.Add(userRole);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User updated: {Email}, ID: {UserId}", user.Email, user.Id);

        // Reload to get updated roles
        user = await _context.Users
            .Include(u => u.UserRoles)
            .FirstAsync(u => u.Id == userId, cancellationToken);

        return MapToDto(user);
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(Guid userId, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (userId == currentUserId)
        {
            throw new BusinessRuleViolationException("CannotDeleteSelf", "You cannot delete your own account");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        // The removing Admin inherits what the household could see of the member's work.
        var withdrawnImages = await HouseholdMemberDeparture.StageAsync(_context, user, currentUserId, cancellationToken);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);

        await HouseholdMemberDeparture.RemoveImagesAsync(withdrawnImages, _storage, _logger, cancellationToken);

        _logger.LogInformation("User deleted: {Email}, ID: {UserId}", user.Email, user.Id);
    }

    /// <inheritdoc />
    public async Task<AdminResetPasswordResponse> AdminResetPasswordAsync(Guid userId, AdminResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        // Generate password if not provided
        string password;
        string? generatedPassword = null;
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            password = PasswordRules.Generate();
            generatedPassword = password;
        }
        else
        {
            PasswordRules.Ensure(request.NewPassword, nameof(request.NewPassword));
            password = request.NewPassword;
        }

        user.PasswordHash = _passwordHasher.HashPassword(password);
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke all refresh tokens
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await FeedTokens.RevokeAllForUserAsync(_context, userId, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset by admin for user: {Email}, ID: {UserId}", user.Email, user.Id);

        return new AdminResetPasswordResponse
        {
            Success = true,
            GeneratedPassword = generatedPassword
        };
    }

    /// <inheritdoc />
    public async Task<List<Role>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ManagedUserDto> LinkContactAsync(Guid userId, Guid contactId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .Include(u => u.Contact)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(User), userId);

        var contact = await _context.Contacts.FindAsync(new object[] { contactId }, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

        // Unlink from any previous contact
        if (user.Contact != null)
        {
            user.Contact.LinkedUserId = null;
        }

        // Link user to new contact
        user.ContactId = contactId;
        contact.LinkedUserId = userId;

        await _context.SaveChangesAsync(cancellationToken);

        // Reload to get updated contact info
        await _context.Entry(user).Reference(u => u.Contact).LoadAsync(cancellationToken);

        _logger.LogInformation("Linked user {UserId} to contact {ContactId}", userId, contactId);

        return MapToDto(user);
    }

    /// <inheritdoc />
    public async Task<ManagedUserDto> UnlinkContactAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .Include(u => u.Contact)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(User), userId);

        if (user.Contact != null)
        {
            user.Contact.LinkedUserId = null;
        }
        user.ContactId = null;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unlinked contact from user {UserId}", userId);

        return MapToDto(user);
    }

    private static ManagedUserDto MapToDto(User user)
    {
        return new ManagedUserDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            Roles = user.UserRoles.Select(ur => ur.Role).ToList(),
            CreatedAt = user.CreatedAt,
            ContactId = user.ContactId,
            ContactName = user.Contact != null ? $"{user.Contact.FirstName} {user.Contact.LastName}".Trim() : null
        };
    }
}
