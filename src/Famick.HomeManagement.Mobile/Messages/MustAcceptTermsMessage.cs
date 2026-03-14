using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Sent when the server returns a MUST_ACCEPT_TERMS 403, requiring the user to accept terms.
/// </summary>
public sealed class MustAcceptTermsMessage(string reason) : ValueChangedMessage<string>(reason);
