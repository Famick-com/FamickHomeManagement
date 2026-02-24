using Famick.HomeManagement.Core.DTOs.Calendar;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing ICS feed tokens and generating ICS calendar feeds.
/// </summary>
public interface ICalendarFeedService
{
    /// <summary>
    /// Gets all ICS tokens for a user.
    /// </summary>
    Task<List<UserCalendarIcsTokenDto>> GetTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new ICS feed token for a user.
    /// </summary>
    Task<UserCalendarIcsTokenDto> CreateTokenAsync(
        CreateIcsTokenRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an ICS feed token. Revoked tokens return 404 on feed requests.
    /// </summary>
    Task RevokeTokenAsync(
        Guid tokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an ICS feed token permanently.
    /// </summary>
    Task DeleteTokenAsync(
        Guid tokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an ICS (iCalendar) feed string for the given token.
    /// Includes the user's calendar events expanded for the next 90 days.
    /// Returns null if the token is invalid or revoked.
    /// </summary>
    Task<string?> GenerateIcsFeedAsync(
        string token,
        CancellationToken cancellationToken = default);
}
