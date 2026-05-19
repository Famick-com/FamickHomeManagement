using Famick.HomeManagement.Mobile.Messages;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Phase 4 chunk 4.G — central place every successful login flows through
/// to compare the server-delivered <c>LoginResponse.LocalServer</c> against
/// the last value stored on this device. First delivery is silent (per
/// design-doc rule 1); subsequent mismatch returns a non-null payload so
/// the caller can push the full-screen
/// <see cref="Pages.LocalServerChangePromptPage"/> on its own navigation
/// stack BEFORE transitioning to the dashboard. Earlier versions of this
/// detector broadcast via WeakReferenceMessenger, but that races against
/// the login modal's pop/transition on iOS — the in-flight transition
/// swallows the modal-push and the prompt never appears.
///
/// Returns:
/// <list type="bullet">
/// <item><c>null</c> — no prompt needed. Cloud login, missing/older server,
/// or first-time delivery (silently stored).</item>
/// <item><see cref="LocalServerChangedPayload"/> — caller must show the
/// prompt. <c>Preferences</c> is NOT updated; the prompt page writes the
/// new value only after the user explicitly confirms.</item>
/// </list>
/// </summary>
public static class LocalServerChangeDetector
{
    public const string LastLocalServerKey = "last_local_server";

    public static LocalServerChangedPayload? ObserveLogin(string? loginResponseLocalServer)
    {
        // Cloud login (or older server without the field) → no signal.
        if (string.IsNullOrEmpty(loginResponseLocalServer))
            return null;

        var stored = Preferences.Default.Get(LastLocalServerKey, string.Empty);

        if (string.IsNullOrEmpty(stored))
        {
            // First-time delivery: store silently. No prompt.
            Preferences.Default.Set(LastLocalServerKey, loginResponseLocalServer);
            return null;
        }

        if (!string.Equals(stored, loginResponseLocalServer, StringComparison.Ordinal))
        {
            return new LocalServerChangedPayload(stored, loginResponseLocalServer);
        }

        return null;
    }
}
