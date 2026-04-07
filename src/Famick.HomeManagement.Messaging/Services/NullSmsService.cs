using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Messaging.Interfaces;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Messaging.Services;

/// <summary>
/// No-op SMS service for self-hosted deployments.
/// The cloud deployment overrides this with a real implementation (e.g., AWS SNS or Twilio).
/// </summary>
public class NullSmsService : ISmsService
{
    private readonly ILogger<NullSmsService> _logger;

    public NullSmsService(ILogger<NullSmsService> logger)
    {
        _logger = logger;
    }

    public Task SendSmsAsync(string toPhoneNumber, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SMS not configured. Would send to {PhoneNumber}: {Body}", toPhoneNumber, body);
        return Task.CompletedTask;
    }
}
