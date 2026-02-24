namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Service for managing first-run detection and onboarding flow state
/// </summary>
public class OnboardingService
{
    private const string OnboardingCompletedKey = "onboarding_completed";
    private const string HomeSetupWizardCompletedKey = "home_setup_wizard_completed";
    private const string PendingVerificationEmailKey = "pending_verification_email";
    private const string PendingVerificationTokenKey = "pending_verification_token";

    /// <summary>
    /// Checks if onboarding has been completed (user has logged in at least once)
    /// </summary>
    public bool IsOnboardingCompleted()
    {
        return Preferences.Default.Get(OnboardingCompletedKey, false);
    }

    /// <summary>
    /// Marks onboarding as completed
    /// </summary>
    public void MarkOnboardingCompleted()
    {
        Preferences.Default.Set(OnboardingCompletedKey, true);
    }

    /// <summary>
    /// Resets onboarding state (for testing or sign out)
    /// </summary>
    public void ResetOnboarding()
    {
        Preferences.Default.Remove(OnboardingCompletedKey);
        Preferences.Default.Remove(HomeSetupWizardCompletedKey);
        ClearPendingVerification();
    }

    /// <summary>
    /// Checks if the home setup wizard has been completed
    /// </summary>
    public bool IsHomeSetupWizardCompleted()
    {
        return Preferences.Default.Get(HomeSetupWizardCompletedKey, false);
    }

    /// <summary>
    /// Marks the home setup wizard as completed
    /// </summary>
    public void MarkHomeSetupWizardCompleted()
    {
        Preferences.Default.Set(HomeSetupWizardCompletedKey, true);
    }

    /// <summary>
    /// Saves pending verification state for resuming after email click
    /// </summary>
    public void SetPendingVerification(string email, string? token = null)
    {
        Preferences.Default.Set(PendingVerificationEmailKey, email);
        if (token != null)
        {
            Preferences.Default.Set(PendingVerificationTokenKey, token);
        }
    }

    /// <summary>
    /// Gets the email address waiting for verification
    /// </summary>
    public string? GetPendingVerificationEmail()
    {
        return Preferences.Default.Get<string?>(PendingVerificationEmailKey, null);
    }

    /// <summary>
    /// Gets the verification token if available
    /// </summary>
    public string? GetPendingVerificationToken()
    {
        return Preferences.Default.Get<string?>(PendingVerificationTokenKey, null);
    }

    /// <summary>
    /// Checks if there's a pending verification
    /// </summary>
    public bool HasPendingVerification()
    {
        return !string.IsNullOrEmpty(GetPendingVerificationEmail());
    }

    /// <summary>
    /// Clears pending verification state
    /// </summary>
    public void ClearPendingVerification()
    {
        Preferences.Default.Remove(PendingVerificationEmailKey);
        Preferences.Default.Remove(PendingVerificationTokenKey);
    }

    /// <summary>
    /// Determines which page to show on app start.
    ///
    /// Flow behavior by server type:
    /// - Cloud (*.famick.com): Full wizard for registration/account creation
    /// - Self-hosted: QR scan to configure, then straight to login
    ///
    /// Both server types go to Login once configured, skipping the wizard.
    /// </summary>
    public OnboardingState GetCurrentState(TokenStorage tokenStorage, ApiSettings apiSettings)
    {
        // If user has valid tokens, go to main app
        // Wizard completion is checked in DashboardPage via server API
        var hasTokens = !string.IsNullOrEmpty(tokenStorage.GetAccessToken());
        if (hasTokens)
        {
            return OnboardingState.LoggedIn;
        }

        // If server is configured (QR scan or previous login), show login
        // This applies to both cloud and self-hosted - once configured, skip wizard
        if (apiSettings.HasServerConfigured())
        {
            return OnboardingState.Login;
        }

        // If there's a pending verification (cloud registration flow), resume it
        if (HasPendingVerification())
        {
            return OnboardingState.EmailVerification;
        }

        // Otherwise, show welcome page for new users
        // - Cloud users: Full wizard (Welcome -> Email -> Password -> etc.)
        // - Self-hosted users: Welcome -> QR Scan -> Login
        return OnboardingState.Welcome;
    }
}

/// <summary>
/// Represents the current state of the onboarding flow
/// </summary>
public enum OnboardingState
{
    /// <summary>
    /// First-time user, show welcome page
    /// </summary>
    Welcome,

    /// <summary>
    /// Waiting for email verification
    /// </summary>
    EmailVerification,

    /// <summary>
    /// Server configured, show login page
    /// </summary>
    Login,

    /// <summary>
    /// User is authenticated but hasn't completed the home setup wizard
    /// </summary>
    HomeSetupWizard,

    /// <summary>
    /// User is logged in, go to main app
    /// </summary>
    LoggedIn
}
