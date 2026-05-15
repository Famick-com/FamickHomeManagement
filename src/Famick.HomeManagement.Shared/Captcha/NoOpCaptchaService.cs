namespace Famick.HomeManagement.Shared.Captcha;

/// <summary>
/// No-op binding for self-hosted Web (single-tenant; no public-internet abuse
/// surface) and unit tests. Always returns success with a perfect score.
/// </summary>
public sealed class NoOpCaptchaService : ICaptchaService
{
    public Task<CaptchaResult> ValidateAsync(
        string? token,
        string action,
        CancellationToken cancellationToken = default)
        => Task.FromResult(CaptchaResult.Pass());
}
