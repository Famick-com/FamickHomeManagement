namespace Famick.HomeManagement.Messaging.Interfaces;

/// <summary>
/// Transport-only interface for sending SMS messages.
/// Self-hosted uses NullSmsService; cloud overrides with a real implementation (e.g., AWS SNS or Twilio).
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Sends an SMS message to the specified phone number.
    /// </summary>
    Task SendSmsAsync(string toPhoneNumber, string body, CancellationToken cancellationToken = default);
}
