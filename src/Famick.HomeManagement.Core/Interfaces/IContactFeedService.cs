using Famick.HomeManagement.Core.DTOs.Contacts;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing vCard feed tokens and generating vCard contact feeds.
/// </summary>
public interface IContactFeedService
{
    /// <summary>
    /// Gets all vCard feed tokens for a user.
    /// </summary>
    Task<List<UserContactVcfTokenDto>> GetTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new vCard feed token for a user.
    /// </summary>
    Task<UserContactVcfTokenDto> CreateTokenAsync(
        CreateVcfTokenRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a vCard feed token. Revoked tokens return 404 on feed requests.
    /// </summary>
    Task RevokeTokenAsync(
        Guid tokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a vCard feed token permanently.
    /// </summary>
    Task DeleteTokenAsync(
        Guid tokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a vCard 3.0 feed string for the given token.
    /// Returns null if the token is invalid or revoked.
    /// </summary>
    Task<string?> GenerateVcfFeedAsync(
        string token,
        CancellationToken cancellationToken = default);
}
