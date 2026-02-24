using Famick.HomeManagement.Core.DTOs.Calendar;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing external calendar subscriptions and syncing ICS feeds.
/// </summary>
public interface IExternalCalendarService
{
    /// <summary>
    /// Gets all external calendar subscriptions for a user.
    /// </summary>
    Task<List<ExternalCalendarSubscriptionDto>> GetSubscriptionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single subscription by ID.
    /// </summary>
    Task<ExternalCalendarSubscriptionDto?> GetSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new external calendar subscription.
    /// </summary>
    Task<ExternalCalendarSubscriptionDto> CreateSubscriptionAsync(
        CreateExternalCalendarSubscriptionRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an external calendar subscription.
    /// </summary>
    Task<ExternalCalendarSubscriptionDto> UpdateSubscriptionAsync(
        Guid subscriptionId,
        UpdateExternalCalendarSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an external calendar subscription and all its imported events.
    /// </summary>
    Task DeleteSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs a single subscription by fetching its ICS feed and upserting events.
    /// </summary>
    Task SyncSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs all subscriptions that are due for sync (based on their sync interval).
    /// </summary>
    Task SyncDueSubscriptionsAsync(
        CancellationToken cancellationToken = default);
}
