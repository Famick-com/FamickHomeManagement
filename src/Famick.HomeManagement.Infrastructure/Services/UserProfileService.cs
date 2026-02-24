using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for user self-service profile operations
/// </summary>
public class UserProfileService : IUserProfileService
{
    private readonly HomeManagementDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;
    private readonly IFileAccessTokenService _tokenService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        HomeManagementDbContext context,
        IPasswordHasher passwordHasher,
        IMapper mapper,
        IFileAccessTokenService tokenService,
        IFileStorageService fileStorageService,
        ILogger<UserProfileService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _tokenService = tokenService;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.Contact)
                .ThenInclude(c => c!.PhoneNumbers)
            .Include(u => u.Contact)
                .ThenInclude(c => c!.EmailAddresses)
            .Include(u => u.Contact)
                .ThenInclude(c => c!.Addresses)
                    .ThenInclude(ca => ca.Address)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        return MapToProfileDto(user);
    }

    /// <inheritdoc />
    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.Contact)
                .ThenInclude(c => c!.PhoneNumbers)
            .Include(u => u.Contact)
                .ThenInclude(c => c!.EmailAddresses)
            .Include(u => u.Contact)
                .ThenInclude(c => c!.Addresses)
                    .ThenInclude(ca => ca.Address)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PreferredLanguage = request.PreferredLanguage;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User profile updated: {UserId}", userId);

        return MapToProfileDto(user);
    }

    /// <inheritdoc />
    public async Task UpdatePreferredLanguageAsync(Guid userId, string languageCode, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        user.PreferredLanguage = languageCode;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User language preference updated: {UserId} to {Language}", userId, languageCode);
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        // Validate passwords match
        if (request.NewPassword != request.ConfirmPassword)
        {
            throw new BusinessRuleViolationException("PasswordMismatch", "New password and confirmation do not match");
        }

        // Verify current password
        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidCredentialsException("Current password is incorrect");
        }

        // Update password and clear forced password change flag
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke all refresh tokens (force re-login on other devices)
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User password changed: {UserId}", userId);
    }

    /// <inheritdoc />
    public async Task<ContactDto> UpdateContactInfoAsync(Guid userId, UpdateContactRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.Contact)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        if (user.Contact == null)
        {
            throw new BusinessRuleViolationException("NoLinkedContact", "User does not have a linked contact");
        }

        // Update contact fields
        user.Contact.FirstName = request.FirstName;
        user.Contact.MiddleName = request.MiddleName;
        user.Contact.LastName = request.LastName;
        user.Contact.PreferredName = request.PreferredName;
        user.Contact.CompanyName = request.CompanyName;
        user.Contact.Title = request.Title;
        user.Contact.Gender = request.Gender;
        user.Contact.BirthYear = request.BirthYear;
        user.Contact.BirthMonth = request.BirthMonth;
        user.Contact.BirthDay = request.BirthDay;
        user.Contact.BirthDatePrecision = request.BirthDatePrecision;
        user.Contact.Notes = request.Notes;
        user.Contact.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User contact info updated: {UserId}, Contact: {ContactId}", userId, user.Contact.Id);

        // Reload contact with related data
        var contact = await _context.Contacts
            .Include(c => c.PhoneNumbers)
            .Include(c => c.EmailAddresses)
            .Include(c => c.Addresses)
                .ThenInclude(ca => ca.Address)
            .FirstAsync(c => c.Id == user.ContactId, cancellationToken);

        var contactDto = _mapper.Map<ContactDto>(contact);

        // Set the profile image URL if the contact has one
        if (!string.IsNullOrEmpty(contact.ProfileImageFileName))
        {
            var accessToken = _tokenService.GenerateToken("contact-profile-image", contact.Id, contact.TenantId);
            contactDto.ProfileImageUrl = _fileStorageService.GetContactProfileImageUrl(contact.Id, accessToken);
        }

        return contactDto;
    }

    private UserProfileDto MapToProfileDto(User user)
    {
        ContactDto? contactDto = null;
        if (user.Contact != null)
        {
            contactDto = _mapper.Map<ContactDto>(user.Contact);

            // Set the profile image URL if the contact has one
            if (!string.IsNullOrEmpty(user.Contact.ProfileImageFileName))
            {
                var accessToken = _tokenService.GenerateToken("contact-profile-image", user.Contact.Id, user.Contact.TenantId);
                contactDto.ProfileImageUrl = _fileStorageService.GetContactProfileImageUrl(user.Contact.Id, accessToken);
            }
        }

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PreferredLanguage = user.PreferredLanguage,
            HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            ContactId = user.ContactId,
            Contact = contactDto
        };
    }
}
