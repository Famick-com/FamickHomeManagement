using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Sent when the household's subscription tier or trial state has changed, carrying the
/// new tier.
/// </summary>
/// <remarks>
/// Raised after a refresh has read the server and found something different. The gated
/// navigation in <c>AppShell</c> re-reads state on every navigation and so needs no
/// prompting, but anything already on screen — the plans page waiting on a purchase to
/// land — does.
/// </remarks>
public sealed class SubscriptionStateChangedMessage(string tier) : ValueChangedMessage<string>(tier);
