namespace Famick.HomeManagement.Core.Messaging.Messages;

/// <summary>Raised when the user logs in or out.</summary>
public sealed class AuthenticationStateChangedMessage(bool isAuthenticated) : Message<bool>(isAuthenticated);
