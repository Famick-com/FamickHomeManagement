using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Messaging.DTOs;

/// <summary>
/// A fully rendered message ready for delivery through a transport.
/// </summary>
public record RenderedMessage(
    Guid? UserId,
    string? ToEmail,
    string? ToPhoneNumber,
    string? UserName,
    MessageType Type,
    string? Subject,
    string? HtmlBody,
    string? TextBody,
    string? SmsBody,
    string? PushTitle,
    string? PushBody,
    string? InAppTitle,
    string? InAppSummary,
    string? DeepLinkUrl,
    Guid? TenantId,
    string? UnsubscribeUrl = null,
    string? UnsubscribeToken = null,
    string? ContentHash = null)
{
    /// <summary>
    /// Id of the in-app notification created for this message, set by the in-app transport (which runs
    /// before the push transport). The push transport includes it in the payload so the client can mark
    /// this notification read when the user dismisses the OS notification. Null when no in-app row exists.
    /// </summary>
    public Guid? NotificationId { get; set; }
}
