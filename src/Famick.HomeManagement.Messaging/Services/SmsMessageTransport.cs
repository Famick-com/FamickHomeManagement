using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Messaging.Services;

/// <summary>
/// Sends rendered messages via SMS using the ISmsService transport.
/// </summary>
public class SmsMessageTransport : IMessageTransport
{
    private readonly ISmsService _smsService;
    private readonly ILogger<SmsMessageTransport> _logger;

    public TransportChannel Channel => TransportChannel.Sms;

    public SmsMessageTransport(
        ISmsService smsService,
        ILogger<SmsMessageTransport> logger)
    {
        _smsService = smsService;
        _logger = logger;
    }

    public async Task SendAsync(RenderedMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(message.ToPhoneNumber))
        {
            _logger.LogDebug("Cannot send SMS: no phone number for user {UserId}", message.UserId);
            return;
        }

        if (string.IsNullOrEmpty(message.SmsBody))
        {
            _logger.LogWarning("Cannot send SMS: no SMS body for message type {Type}", message.Type);
            return;
        }

        await _smsService.SendSmsAsync(message.ToPhoneNumber, message.SmsBody, cancellationToken);
    }
}
