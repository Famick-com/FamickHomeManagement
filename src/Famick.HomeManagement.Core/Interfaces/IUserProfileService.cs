using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.Users;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for user self-service profile operations
/// </summary>
public interface IUserProfileService
{
    /// <summary>
    /// Get the current user's profile with linked contact information
    /// </summary>
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the current user's profile (name and language)
    /// </summary>
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update only the user's preferred language
    /// </summary>
    Task UpdatePreferredLanguageAsync(Guid userId, string languageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Change the user's password (requires current password verification)
    /// </summary>
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the user's linked contact information
    /// </summary>
    Task<ContactDto> UpdateContactInfoAsync(Guid userId, UpdateContactRequest request, CancellationToken cancellationToken = default);
}
