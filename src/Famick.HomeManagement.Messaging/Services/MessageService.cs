using Famick.HomeManagement.Messaging.Configuration;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Messaging.Services;

/// <summary>
/// Unified messaging service that renders Mustache templates and routes messages
/// through the appropriate transport(s) based on user preferences.
/// </summary>
public class MessageService : IMessageService
{
    private readonly StubbleTemplateRenderer _templateRenderer;
    private readonly IEnumerable<IMessageTransport> _transports;
    private readonly INotificationService _notificationService;
    private readonly IUnsubscribeTokenService _unsubscribeTokenService;
    private readonly IMessageRecipientResolver _recipientResolver;
    private readonly ComplianceSettings _complianceSettings;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        StubbleTemplateRenderer templateRenderer,
        IEnumerable<IMessageTransport> transports,
        INotificationService notificationService,
        IUnsubscribeTokenService unsubscribeTokenService,
        IMessageRecipientResolver recipientResolver,
        IOptions<ComplianceSettings> complianceSettings,
        ILogger<MessageService> logger)
    {
        _templateRenderer = templateRenderer;
        _transports = transports;
        _notificationService = notificationService;
        _unsubscribeTokenService = unsubscribeTokenService;
        _recipientResolver = recipientResolver;
        _complianceSettings = complianceSettings.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        Guid userId,
        MessageType type,
        IMessageData data,
        CancellationToken cancellationToken = default)
    {
        var recipient = await _recipientResolver.ResolveAsync(userId, cancellationToken);

        if (recipient is null)
        {
            _logger.LogWarning("Cannot send message: user {UserId} not found", userId);
            return;
        }

        if (!recipient.IsActive)
        {
            _logger.LogDebug("Skipping message for inactive user {UserId}", userId);
            return;
        }

        var preferences = await _notificationService.GetPreferencesAsync(userId, cancellationToken);
        var pref = preferences.FirstOrDefault(p => p.MessageType == type);

        // Extract deep link URL from data if available
        var deepLinkUrl = data.GetType().GetProperty("DeepLinkUrl")?.GetValue(data) as string;

        // Generate unsubscribe URL for non-transactional emails
        string? unsubscribeToken = null;
        string? unsubscribeUrl = null;
        if (type.IsNotification())
        {
            unsubscribeToken = _unsubscribeTokenService.GenerateToken(userId, recipient.TenantId, type);
            if (!string.IsNullOrEmpty(_complianceSettings.UnsubscribeBaseUrl))
            {
                unsubscribeUrl = $"{_complianceSettings.UnsubscribeBaseUrl.TrimEnd('/')}/api/v1/notifications/unsubscribe?token={unsubscribeToken}";
            }
        }

        // Build compliance layout context for non-transactional email rendering
        var layoutContext = BuildComplianceContext(type, unsubscribeUrl);

        // Render subject
        var subject = await _templateRenderer.RenderSubjectAsync(type, data, cancellationToken);

        // Compute content hash for change detection
        var contentHash = ContentHasher.ComputeHash(data);

        // Pre-render all content
        var renderedContent = await RenderAllContentAsync(type, data, layoutContext, cancellationToken);

        var message = new RenderedMessage(
            UserId: userId,
            ToEmail: recipient.Email,
            ToPhoneNumber: recipient.PhoneNumber,
            UserName: recipient.FullName,
            Type: type,
            Subject: subject,
            HtmlBody: renderedContent.EmailHtml,
            TextBody: renderedContent.EmailText,
            SmsBody: renderedContent.Sms,
            PushTitle: subject,
            PushBody: renderedContent.Push,
            InAppTitle: subject,
            InAppSummary: renderedContent.InApp,
            DeepLinkUrl: deepLinkUrl,
            TenantId: recipient.TenantId,
            UnsubscribeUrl: unsubscribeUrl,
            UnsubscribeToken: unsubscribeToken,
            ContentHash: contentHash);

        // Dispatch through each enabled transport
        foreach (var transport in _transports)
        {
            var isEnabled = transport.Channel switch
            {
                TransportChannel.EmailHtml => pref?.EmailEnabled ?? true,
                TransportChannel.Sms => pref?.SmsEnabled ?? false,
                TransportChannel.Push => pref?.PushEnabled ?? true,
                TransportChannel.InApp => pref?.InAppEnabled ?? true,
                _ => false
            };

            if (!isEnabled)
                continue;

            try
            {
                await transport.SendAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send {Type} via {Channel} to user {UserId}",
                    type, transport.Channel, userId);
            }
        }
    }

    public async Task SendTransactionalAsync(
        string toEmail,
        MessageType type,
        IMessageData data,
        CancellationToken cancellationToken = default)
    {
        var subject = await _templateRenderer.RenderSubjectAsync(type, data, cancellationToken);

        // Transactional emails: no compliance footer
        var htmlBody = await _templateRenderer.RenderAsync(type, TransportChannel.EmailHtml, data, layoutContext: null, cancellationToken);

        string? textBody = null;
        if (_templateRenderer.HasTemplate(type, TransportChannel.EmailText))
            textBody = await _templateRenderer.RenderAsync(type, TransportChannel.EmailText, data, cancellationToken);

        var message = new RenderedMessage(
            UserId: null,
            ToEmail: toEmail,
            ToPhoneNumber: null,
            UserName: null,
            Type: type,
            Subject: subject,
            HtmlBody: htmlBody,
            TextBody: textBody ?? string.Empty,
            SmsBody: null,
            PushTitle: null,
            PushBody: null,
            InAppTitle: null,
            InAppSummary: null,
            DeepLinkUrl: null,
            TenantId: null);

        var emailTransport = _transports.FirstOrDefault(t => t.Channel == TransportChannel.EmailHtml);
        if (emailTransport is null)
        {
            _logger.LogError("No email transport registered; cannot send transactional email {Type}", type);
            return;
        }

        await emailTransport.SendAsync(message, cancellationToken);
    }

    private IDictionary<string, object>? BuildComplianceContext(MessageType type, string? unsubscribeUrl)
    {
        if (type.IsTransactional())
            return null;

        var preferencesUrl = !string.IsNullOrEmpty(_complianceSettings.UnsubscribeBaseUrl)
            ? $"{_complianceSettings.UnsubscribeBaseUrl.TrimEnd('/')}/settings/notifications"
            : null;

        var footer = new Dictionary<string, object?>
        {
            { "CompanyName", _complianceSettings.CompanyName },
            { "UnsubscribeUrl", unsubscribeUrl },
            { "PreferencesUrl", preferencesUrl },
            { "PhysicalAddress", string.IsNullOrEmpty(_complianceSettings.PhysicalAddress) ? null : _complianceSettings.PhysicalAddress },
            { "PrivacyPolicyUrl", _complianceSettings.PrivacyPolicyUrl }
        };

        var cleaned = footer
            .Where(kvp => kvp.Value is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

        return new Dictionary<string, object>
        {
            { "complianceFooter", cleaned }
        };
    }

    private async Task<RenderedContent> RenderAllContentAsync(
        MessageType type,
        IMessageData data,
        IDictionary<string, object>? layoutContext,
        CancellationToken cancellationToken)
    {
        string? emailHtml = null, emailText = null, sms = null, push = null, inApp = null;

        if (_templateRenderer.HasTemplate(type, TransportChannel.EmailHtml))
            emailHtml = await _templateRenderer.RenderAsync(type, TransportChannel.EmailHtml, data, layoutContext, cancellationToken);

        if (_templateRenderer.HasTemplate(type, TransportChannel.EmailText))
            emailText = await _templateRenderer.RenderAsync(type, TransportChannel.EmailText, data, cancellationToken);

        if (_templateRenderer.HasTemplate(type, TransportChannel.Sms))
            sms = await _templateRenderer.RenderAsync(type, TransportChannel.Sms, data, cancellationToken);

        if (_templateRenderer.HasTemplate(type, TransportChannel.Push))
            push = await _templateRenderer.RenderAsync(type, TransportChannel.Push, data, cancellationToken);

        if (_templateRenderer.HasTemplate(type, TransportChannel.InApp))
            inApp = await _templateRenderer.RenderAsync(type, TransportChannel.InApp, data, cancellationToken);

        return new RenderedContent(emailHtml, emailText, sms, push, inApp);
    }

    private record RenderedContent(string? EmailHtml, string? EmailText, string? Sms, string? Push, string? InApp);
}
