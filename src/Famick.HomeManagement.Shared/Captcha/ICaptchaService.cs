namespace Famick.HomeManagement.Shared.Captcha;

/// <summary>
/// Validates a CAPTCHA token (reCAPTCHA v3, hCaptcha, Turnstile, etc.).
/// Implementations are registered per host: cloud Web uses
/// <see cref="RecaptchaService"/>; self-hosted Web uses
/// <see cref="NoOpCaptchaService"/>; tests bind their own fakes.
///
/// <para>The <paramref name="action"/> parameter is the provider-side action
/// name attached to the token at issuance — used by reCAPTCHA v3 for risk
/// scoring per surface (login, signup, contact, …). Pass a stable string per
/// call site.</para>
/// </summary>
public interface ICaptchaService
{
    Task<CaptchaResult> ValidateAsync(
        string? token,
        string action,
        CancellationToken cancellationToken = default);
}
