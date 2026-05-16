using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Phase 4 chunk 4.G — Broadcast when the server-delivered
/// <c>LoginResponse.LocalServer</c> differs from the value last stored on
/// this device. <c>App.HandleLocalServerChange</c> consumes this and pushes
/// the full-screen <see cref="Pages.LocalServerChangePromptPage"/> to make
/// the user explicitly confirm before the new URL is trusted.
/// </summary>
public sealed class LocalServerChangedMessage(string oldUrl, string newUrl)
    : ValueChangedMessage<LocalServerChangedPayload>(new(oldUrl, newUrl));

public sealed record LocalServerChangedPayload(string OldUrl, string NewUrl);
