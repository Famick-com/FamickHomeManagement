namespace Famick.HomeManagement.Shared.Captcha;

/// <summary>
/// Configuration for the Google reCAPTCHA v3 binding. Bound from the
/// <c>RecaptchaSettings</c> section in appsettings (or env vars like
/// <c>RecaptchaSettings__SecretKey</c>). When <see cref="SecretKey"/> is empty
/// the host should bind <see cref="NoOpCaptchaService"/> instead — see
/// <see cref="StartupExtensions.AddCaptcha"/>.
/// </summary>
public class RecaptchaSettings
{
    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Minimum v3 score considered "human". Tokens scoring below this fail
    /// validation. Default 0.5 follows Google's recommendation for general
    /// surfaces; tune per-call-site via the provider dashboard, not here.
    /// </summary>
    public double ScoreThreshold { get; set; } = 0.5;
}
