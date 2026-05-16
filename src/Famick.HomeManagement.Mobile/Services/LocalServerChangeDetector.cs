using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Phase 4 chunk 4.G — central place every successful login flows through
/// to compare the server-delivered <c>LoginResponse.LocalServer</c> against
/// the last value stored on this device. First delivery is silent (per
/// design-doc rule 1); subsequent mismatch broadcasts
/// <see cref="LocalServerChangedMessage"/> for <c>App.xaml.cs</c> to surface
/// the full-screen <see cref="Pages.LocalServerChangePromptPage"/>.
///
/// The canonical-equality contract is upheld server-side: every value the
/// server emits is already <c>scheme://host[:port]</c> per the resolver
/// (chunk 4.D), so a string compare suffices on the client.
/// </summary>
public static class LocalServerChangeDetector
{
    public const string LastLocalServerKey = "last_local_server";

    public static void ObserveLogin(string? loginResponseLocalServer)
    {
        // Cloud login (or older server without the field) → no signal.
        if (string.IsNullOrEmpty(loginResponseLocalServer))
            return;

        var stored = Preferences.Default.Get(LastLocalServerKey, string.Empty);

        if (string.IsNullOrEmpty(stored))
        {
            // First-time delivery: store silently. No prompt.
            Preferences.Default.Set(LastLocalServerKey, loginResponseLocalServer);
            return;
        }

        if (!string.Equals(stored, loginResponseLocalServer, StringComparison.Ordinal))
        {
            // Mismatch: broadcast. Do NOT update Preferences here — the
            // prompt page writes it after the user explicitly confirms.
            WeakReferenceMessenger.Default.Send(
                new LocalServerChangedMessage(stored, loginResponseLocalServer));
        }
    }
}
