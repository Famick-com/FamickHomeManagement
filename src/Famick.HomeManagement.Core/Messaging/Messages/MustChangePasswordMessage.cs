namespace Famick.HomeManagement.Core.Messaging.Messages;

/// <summary>Raised when the server requires a forced password change.</summary>
public sealed class MustChangePasswordMessage(string reason) : Message<string>(reason);
