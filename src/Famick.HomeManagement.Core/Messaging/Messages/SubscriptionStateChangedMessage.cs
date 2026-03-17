namespace Famick.HomeManagement.Core.Messaging.Messages;

/// <summary>Raised when subscription tier or trial status changes.</summary>
public sealed class SubscriptionStateChangedMessage(string tier) : Message<string>(tier);
