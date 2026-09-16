using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Sent when the server refuses a change because the household's subscription has ended.
/// </summary>
/// <remarks>
/// Carries the server's own explanation. Distinct from
/// <see cref="SessionExpiredMessage"/>, which means "sign in again" — this one means the
/// sign-in is fine and the plan is not, so bouncing the user to a login screen would be
/// both wrong and impossible to act on.
///
/// <para>Only writes are refused; reading carries on working. That is the promise the
/// screen has to make good on, so the wording says the data is safe rather than implying
/// the app is broken.</para>
/// </remarks>
public sealed class SubscriptionExpiredMessage(string reason) : ValueChangedMessage<string>(reason);
