using System.Net;
using System.Text;
using Famick.HomeManagement.Shared.Captcha;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Captcha;

public class RecaptchaServiceTests
{
    private static RecaptchaService BuildSut(string responseBody, string secretKey = "test-secret", double threshold = 0.5)
    {
        var handler = new StubHandler(responseBody);
        var http = new HttpClient(handler);
        var settings = Options.Create(new RecaptchaSettings
        {
            SecretKey = secretKey,
            ScoreThreshold = threshold,
        });
        return new RecaptchaService(http, settings, NullLogger<RecaptchaService>.Instance);
    }

    [Fact]
    public async Task ValidateAsync_returns_pass_when_score_meets_threshold()
    {
        var sut = BuildSut("""{"success":true,"score":0.9,"action":"login"}""", threshold: 0.5);

        var result = await sut.ValidateAsync("valid-token", action: "login");

        result.Success.Should().BeTrue();
        result.Score.Should().Be(0.9);
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_returns_fail_when_upstream_rejects()
    {
        var sut = BuildSut("""{"success":false,"error-codes":["invalid-input-response"]}""");

        var result = await sut.ValidateAsync("bad-token", action: "login");

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("upstream_rejected");
    }

    [Fact]
    public async Task ValidateAsync_returns_fail_when_score_below_threshold()
    {
        var sut = BuildSut("""{"success":true,"score":0.2,"action":"login"}""", threshold: 0.5);

        var result = await sut.ValidateAsync("low-score", action: "login");

        result.Success.Should().BeFalse();
        result.Score.Should().Be(0.2);
        result.FailureReason.Should().Be("below_threshold");
    }

    [Fact]
    public async Task ValidateAsync_returns_fail_on_missing_token()
    {
        var sut = BuildSut("""{"success":true,"score":1.0}""");

        var result = await sut.ValidateAsync(token: "", action: "login");

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("missing_token");
    }

    [Fact]
    public async Task ValidateAsync_bypasses_validation_when_secret_is_empty()
    {
        var sut = BuildSut("""{"success":false}""", secretKey: "");

        var result = await sut.ValidateAsync("any-token", action: "login");

        result.Success.Should().BeTrue("an unconfigured secret short-circuits to pass so dev environments work");
    }

    [Fact]
    public async Task ValidateAsync_returns_upstream_null_on_malformed_response()
    {
        var sut = BuildSut("null");

        var result = await sut.ValidateAsync("token", action: "login");

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("upstream_null");
    }

    private class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        public StubHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
