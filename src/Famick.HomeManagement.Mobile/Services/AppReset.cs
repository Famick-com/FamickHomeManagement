namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Wipes every locally-stored bit of authentication and server config so the
/// next launch lands on WelcomePage as if this were a fresh install.
/// </summary>
/// <remarks>
/// Two callers: the "Reset app" action on the Settings page, and the prompt
/// that fires when the sign-in method chosen earlier turns out to be
/// unreachable (e.g. a paused cloud account while offline). Both need the same
/// wipe, so it lives here rather than on either caller.
/// <para>
/// Nothing here touches the server — this is local state only.
/// </para>
/// </remarks>
public static class AppReset
{
    public static async Task RunAsync()
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        if (services == null) return;

        // Best-effort: try to unregister the push token while we still
        // have it. Failures here don't block the reset.
        try
        {
            var pushService = services.GetService<PushNotificationRegistrationService>();
            if (pushService != null) await pushService.UnregisterAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppReset] Push unregister error: {ex.Message}");
        }

        // Clear tokens (Keychain on iOS — the bit that survives uninstall).
        var tokenStorage = services.GetService<TokenStorage>();
        if (tokenStorage != null) await tokenStorage.ClearTokensAsync();

        // Clear tenant display name, ApiSettings (Mode / BaseUrl / proxied
        // home-server snapshot), and the proxied email-lookup cache.
        services.GetService<TenantStorage>()?.Clear();
        services.GetService<ApiSettings>()?.Reset();
        services.GetService<ProxiedEmailCache>()?.Clear();

        // Forget that onboarding ever completed — next launch should
        // re-run it like a fresh install.
        services.GetService<OnboardingService>()?.ResetOnboarding();
    }
}
