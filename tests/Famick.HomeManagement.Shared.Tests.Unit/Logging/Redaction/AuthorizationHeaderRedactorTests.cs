using Famick.HomeManagement.Logging.Redaction.Redactors;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Logging.Redaction;

public class AuthorizationHeaderRedactorTests
{
    private readonly AuthorizationHeaderRedactor _sut = new();

    [Fact]
    public void Redacts_Authorization_Bearer_header()
    {
        var input = "Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.payload.sig";
        _sut.Redact(input).Should().Be("Authorization: <redacted>");
    }

    [Fact]
    public void Redacts_Authorization_Bearer_inside_a_longer_message()
    {
        var input = "Request rejected. Authorization: Bearer abc.def.ghi was expired.";
        _sut.Redact(input).Should().Be("Request rejected. Authorization: <redacted> was expired.");
    }

    [Fact]
    public void Redacts_Authorization_Bearer_case_insensitively()
    {
        var input = "authorization: bearer SECRET";
        _sut.Redact(input).Should().Be("Authorization: <redacted>");
    }

    [Fact]
    public void Redacts_X_ProxyToken_header()
    {
        var input = "X-ProxyToken: abcdef-1234567890";
        _sut.Redact(input).Should().Be("X-ProxyToken: <redacted>");
    }

    [Fact]
    public void Leaves_unrelated_text_untouched()
    {
        var input = "GET /api/v1/health 200 OK";
        _sut.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Handles_empty_and_null_safely()
    {
        _sut.Redact("").Should().Be("");
        _sut.Redact(null!).Should().BeNull();
    }
}
