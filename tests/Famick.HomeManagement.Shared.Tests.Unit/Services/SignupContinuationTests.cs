using System.Text.Json;
using Famick.HomeManagement.Shared.Net;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Where the external-sign-in callback goes on a <c>SIGNUP_REQUIRED</c> answer. Only a path on
/// this origin is followed; anything a browser could read as another host is ignored.
/// </summary>
public class SignupContinuationTests
{
    private static readonly IRedirectUrlValidator Validator = new RedirectUrlValidator(
        Options.Create(new RedirectUriAllowListOptions { Hosts = ["app.famick.com"] }));

    private static ApiResult Answer(string? continueUrl, string code = "SIGNUP_REQUIRED")
    {
        var json = JsonSerializer.Serialize(new { code, signup = new { ticket = "t", continueUrl } });
        return new ApiResult
        {
            IsSuccess = false,
            StatusCode = 409,
            ErrorCode = code,
            ErrorBody = JsonDocument.Parse(json).RootElement.Clone()
        };
    }

    [Theory]
    [InlineData("/register/provider?ticket=abc")]
    [InlineData("/")]
    public void Local_path_is_followed(string url) =>
        SignupContinuation.LocalContinueUrl(Answer(url), Validator).Should().Be(url);

    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("/\\evil.example/x")]
    [InlineData("/\t/evil.example/x")]
    [InlineData("/\n/evil.example/x")]
    [InlineData("/\r/evil.example/x")]
    [InlineData("/.//evil.example/x")]
    [InlineData("https://evil.example/x")]
    [InlineData("https://app.famick.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("register/provider")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_is_ignored(string? url) =>
        SignupContinuation.LocalContinueUrl(Answer(url), Validator).Should().BeNull();

    [Fact]
    public void Other_error_codes_are_ignored() =>
        SignupContinuation.LocalContinueUrl(Answer("/register/provider", code: "INVALID_CREDENTIALS"), Validator)
            .Should().BeNull();

    [Fact]
    public void Missing_body_is_ignored() =>
        SignupContinuation.LocalContinueUrl(new ApiResult { ErrorCode = "SIGNUP_REQUIRED" }, Validator)
            .Should().BeNull();
}
