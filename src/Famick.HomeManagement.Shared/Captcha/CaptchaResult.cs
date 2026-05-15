namespace Famick.HomeManagement.Shared.Captcha;

/// <summary>
/// Outcome of an <see cref="ICaptchaService.ValidateAsync"/> call. <c>Score</c>
/// is the v3 risk score in [0.0, 1.0] (1.0 = very likely human); <c>0.0</c>
/// when the provider doesn't return one (e.g. the NoOp service, or a malformed
/// upstream response).
/// </summary>
public readonly record struct CaptchaResult(bool Success, double Score, string? FailureReason)
{
    public static CaptchaResult Pass(double score = 1.0) => new(true, score, null);
    public static CaptchaResult Fail(string reason) => new(false, 0.0, reason);
}
