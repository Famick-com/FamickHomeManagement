using Famick.HomeManagement.Logging.Redaction.Redactors;
using FluentAssertions;

namespace Famick.HomeManagement.Logging.Redaction.Tests.Unit;

public class AuthorizationHeaderRedactorTests
{
    private readonly AuthorizationHeaderRedactor _redactor = new();

    [Fact]
    public void Redacts_bearer_token()
    {
        var input = "Request headers: Authorization: Bearer eyJhbGciOiJSUzI1NiIsImtpZCI6ImFi.eyJzdWIiOiJ1c2VyIn0.SignaturE";
        var result = _redactor.Redact(input);
        result.Should().Contain("Authorization: <redacted>");
        result.Should().NotContain("eyJhbGciOiJSUzI1NiIsImtpZCI6ImFi");
    }

    [Fact]
    public void Redacts_bearer_token_case_insensitively()
    {
        var input = "authorization: bearer eyJhbGciOiJSUzI1NiIsImtpZCI6ImFi.eyJzdWIiOiJ1c2VyIn0.SignaturE";
        _redactor.Redact(input).Should().Contain("Authorization: <redacted>");
    }

    [Fact]
    public void Redacts_x_proxy_token()
    {
        var input = "Forwarding request with X-ProxyToken: eyJabcDEF12345.SignaturE to backend";
        var result = _redactor.Redact(input);
        result.Should().Contain("X-ProxyToken: <redacted>");
        result.Should().NotContain("eyJabcDEF12345");
    }

    [Fact]
    public void Leaves_other_headers_alone()
    {
        var input = "Request: Content-Type: application/json, Accept: */*, X-Request-Id: 42";
        _redactor.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Handles_empty_input()
    {
        _redactor.Redact("").Should().Be("");
    }
}
