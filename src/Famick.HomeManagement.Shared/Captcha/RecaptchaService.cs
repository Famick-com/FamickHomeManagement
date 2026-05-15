using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Shared.Captcha;

/// <summary>
/// Validates reCAPTCHA v3 tokens against
/// <c>https://www.google.com/recaptcha/api/siteverify</c>. Registered as a
/// typed <see cref="HttpClient"/>.
/// </summary>
public sealed class RecaptchaService : ICaptchaService
{
    private const string SiteVerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    private readonly HttpClient _httpClient;
    private readonly RecaptchaSettings _settings;
    private readonly ILogger<RecaptchaService> _logger;

    public RecaptchaService(
        HttpClient httpClient,
        IOptions<RecaptchaSettings> settings,
        ILogger<RecaptchaService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CaptchaResult> ValidateAsync(
        string? token,
        string action,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            _logger.LogWarning("reCAPTCHA secret key not configured; bypassing validation for action={Action}", action);
            return CaptchaResult.Pass();
        }

        if (string.IsNullOrWhiteSpace(token))
            return CaptchaResult.Fail("missing_token");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = _settings.SecretKey,
            ["response"] = token,
        });

        using var response = await _httpClient.PostAsync(SiteVerifyUrl, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<SiteVerifyResponse>(json);

        if (result is null)
        {
            _logger.LogWarning("reCAPTCHA siteverify returned null body for action={Action}", action);
            return CaptchaResult.Fail("upstream_null");
        }

        if (!result.Success)
        {
            _logger.LogWarning(
                "reCAPTCHA siteverify failed for action={Action} errors={Errors}",
                action,
                string.Join(",", result.ErrorCodes ?? []));
            return CaptchaResult.Fail("upstream_rejected");
        }

        if (result.Score < _settings.ScoreThreshold)
        {
            _logger.LogWarning(
                "reCAPTCHA score {Score} below threshold {Threshold} for action={Action}",
                result.Score,
                _settings.ScoreThreshold,
                action);
            return new CaptchaResult(false, result.Score, "below_threshold");
        }

        return CaptchaResult.Pass(result.Score);
    }

    private class SiteVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
