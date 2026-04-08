using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Messaging.Interfaces;

/// <summary>
/// Unified messaging service that routes messages through the appropriate transport(s)
/// based on message type and user notification preferences.
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Sends a notification message to a user through enabled transport channels
    /// based on their notification preferences.
    /// </summary>
    /// <param name="userId">The target user's ID</param>
    /// <param name="type">The message type (must be a preference-based type, not transactional)</param>
    /// <param name="data">The data model used to render templates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendAsync(Guid userId, MessageType type, IMessageData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a transactional email (email-only, bypasses notification preferences).
    /// Used for verification, password reset, welcome emails, etc.
    /// </summary>
    /// <param name="toEmail">The recipient's email address</param>
    /// <param name="type">The transactional message type</param>
    /// <param name="data">The data model used to render templates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendTransactionalAsync(string toEmail, MessageType type, IMessageData data, CancellationToken cancellationToken = default);
}
