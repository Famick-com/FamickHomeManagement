using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Sent when the server returns a must_change_password 403, requiring the user to change their password.
/// </summary>
public sealed class MustChangePasswordMessage(string reason) : ValueChangedMessage<string>(reason);
