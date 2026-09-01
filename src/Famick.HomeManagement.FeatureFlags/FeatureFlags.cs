namespace Famick.HomeManagement.FeatureFlags;

/// <summary>
/// Strongly-typed feature-flag names. Using these constants instead of raw strings
/// catches typos at compile time and makes flag usage discoverable across the codebase.
///
/// All flags are registered in <see cref="StartupExtensions.AddFeatureFlags"/> with a
/// default value of <c>false</c>. Per-environment overrides come from configuration
/// (appsettings.json under <c>FeatureManagement:</c> or env vars
/// <c>FeatureManagement__&lt;FlagName&gt;=true</c>).
/// </summary>
public static class FeatureFlags
{
    /// <summary>Phase 2 — gates the step-up authentication middleware.</summary>
    public const string StepUpEnabled = "step_up_enabled";

    /// <summary>Phase 3/4 — gates the new <c>/check</c> endpoint server-side.</summary>
    public const string CheckEndpointEnabled = "check_endpoint_enabled";

    /// <summary>Phase 4 — gates the mobile two-step login UI (email page → password page).</summary>
    public const string TwoStepLoginV2 = "two_step_login_v2";

    /// <summary>Phase 5 — gates whether mobile/SPA clients use auth.famick.com vs. app.famick.com.</summary>
    public const string UseAuthFamickCom = "use_auth_famick_com";

    /// <summary>Phase 8 — gates the proxy.famick.com sign-up flow.</summary>
    public const string ProxySignupEnabled = "proxy_signup_enabled";

    /// <summary>Phase 9 — gates the self-hosted agent's outbound WebSocket connection to the proxy.</summary>
    public const string ProxyAgentEnabled = "proxy_agent_enabled";

    /// <summary>Phase 10 — gates whether tunneled traffic flows end-to-end through the proxy.</summary>
    public const string ProxyTunnelEnabled = "proxy_tunnel_enabled";

    /// <summary>
    /// Gates recording a person's allergies and dietary restrictions, and the meal-planner
    /// warnings derived from them.
    /// </summary>
    /// <remarks>
    /// Off while the obligations around holding health data outside HIPAA are settled —
    /// an allergy recorded against a named household member is health information, and
    /// state laws aimed at non-covered entities reach it. Turning this on again resumes
    /// collection; nothing already stored was removed.
    /// <para>
    /// This does not gate a product's allergen content. That is food composition read from
    /// a product database, not a fact about a person.
    /// </para>
    /// </remarks>
    public const string DietaryProfilesEnabled = "dietary_profiles_enabled";

    /// <summary>
    /// Returns the full list of registered flag names. Used by diagnostics endpoints and tests
    /// to verify every defined flag is wired up.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        StepUpEnabled,
        CheckEndpointEnabled,
        TwoStepLoginV2,
        UseAuthFamickCom,
        ProxySignupEnabled,
        ProxyAgentEnabled,
        ProxyTunnelEnabled,
        DietaryProfilesEnabled
    ];
}
