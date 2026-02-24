using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Sent when the refresh token is expired or revoked and the user must re-authenticate.
/// </summary>
public sealed class SessionExpiredMessage(string reason) : ValueChangedMessage<string>(reason);
