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
    string? ContentHash = null);
