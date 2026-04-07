using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Evaluates tenant data and produces notification items for the daily background service.
/// Each notification type has its own evaluator implementation.
/// </summary>
public interface INotificationEvaluator
{
    /// <summary>
    /// The notification type this evaluator produces
    /// </summary>
    MessageType Type { get; }

    /// <summary>
    /// Evaluates a tenant's data and returns notification items to dispatch
    /// </summary>
    Task<IReadOnlyList<NotificationItem>> EvaluateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A notification item produced by an evaluator, targeting a specific user.
/// Contains a data model for template rendering instead of pre-rendered HTML.
/// </summary>
public record NotificationItem(
    Guid UserId,
    MessageType Type,
    string Title,
    string Summary,
    string? DeepLinkUrl,
    IMessageData Data);
