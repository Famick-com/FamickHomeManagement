namespace Famick.HomeManagement.Core.Messaging.Messages;

/// <summary>Raised when the server requires terms acceptance.</summary>
public sealed class MustAcceptTermsMessage(string reason) : Message<string>(reason);
