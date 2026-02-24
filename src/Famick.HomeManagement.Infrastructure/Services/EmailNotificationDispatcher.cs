using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Dispatches notifications via email with RFC 8058 List-Unsubscribe headers.
/// </summary>
public class EmailNotificationDispatcher : INotificationDispatcher
{
    private readonly IEmailService _emailService;
    private readonly IUnsubscribeTokenService _unsubscribeTokenService;
    private readonly ILogger<EmailNotificationDispatcher> _logger;

    public EmailNotificationDispatcher(
        IEmailService emailService,
        IUnsubscribeTokenService unsubscribeTokenService,
        ILogger<EmailNotificationDispatcher> logger)
    {
        _emailService = emailService;
        _unsubscribeTokenService = unsubscribeTokenService;
        _logger = logger;
    }

    public async Task DispatchAsync(
        NotificationItem item,
        NotificationPreference preference,
        User user,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!preference.EmailEnabled)
        {
            _logger.LogDebug("Email notifications disabled for user {UserId}, type {Type}", user.Id, item.Type);
            return;
        }

        if (string.IsNullOrEmpty(item.EmailHtmlBody) || string.IsNullOrEmpty(item.EmailSubject))
        {
            _logger.LogDebug("No email content for notification type {Type}, skipping email dispatch", item.Type);
            return;
        }

        var unsubscribeToken = _unsubscribeTokenService.GenerateToken(user.Id, tenantId, item.Type);

        try
        {
            await _emailService.SendNotificationEmailAsync(
                user.Email,
                $"{user.FirstName} {user.LastName}",
                item.EmailSubject,
                item.EmailHtmlBody,
                item.EmailTextBody ?? item.Summary,
                unsubscribeToken,
                cancellationToken);

            _logger.LogDebug("Email notification sent to user {UserId}, type {Type}", user.Id, item.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification to user {UserId}, type {Type}", user.Id, item.Type);
        }
    }
}
