namespace Famick.HomeManagement.Core.Messaging.Messages;

/// <summary>Raised when the auth session expires or is revoked.</summary>
public sealed class SessionExpiredMessage(string reason) : Message<string>(reason);
