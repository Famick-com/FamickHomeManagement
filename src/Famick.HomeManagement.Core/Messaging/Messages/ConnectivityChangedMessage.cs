namespace Famick.HomeManagement.Core.Messaging.Messages;

/// <summary>Raised when network connectivity state changes.</summary>
public sealed class ConnectivityChangedMessage(bool isConnected) : Message<bool>(isConnected);
