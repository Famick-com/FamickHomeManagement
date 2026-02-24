using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing users (Admin only)
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Get all users in the current tenant
    /// </summary>
    Task<List<ManagedUserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific user by ID
    /// </summary>
    Task<ManagedUserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new user
    /// </summary>
    /// <param name="request">The create user request</param>
    /// <param name="baseUrl">The base URL of the application for login link in welcome email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing user
    /// </summary>
    Task<ManagedUserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a user. Cannot delete the current user.
    /// </summary>
    /// <param name="userId">ID of user to delete</param>
    /// <param name="currentUserId">ID of the user making the request (to prevent self-deletion)</param>
    Task DeleteUserAsync(Guid userId, Guid currentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset a user's password (Admin action)
    /// </summary>
    Task<AdminResetPasswordResponse> AdminResetPasswordAsync(Guid userId, AdminResetPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get roles for a specific user
    /// </summary>
    Task<List<Role>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Link a contact to a user
    /// </summary>
    Task<ManagedUserDto> LinkContactAsync(Guid userId, Guid contactId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlink a contact from a user
    /// </summary>
    Task<ManagedUserDto> UnlinkContactAsync(Guid userId, CancellationToken cancellationToken = default);
}
