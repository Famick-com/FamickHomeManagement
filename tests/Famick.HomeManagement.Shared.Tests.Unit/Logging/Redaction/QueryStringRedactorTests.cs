using Famick.HomeManagement.Logging.Redaction.Redactors;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Logging.Redaction;

public class QueryStringRedactorTests
{
    private readonly QueryStringRedactor _sut = new();

    [Fact]
    public void Redacts_single_query_param_value()
    {
        var input = "GET /api/foo?api_key=secret123";
        _sut.Redact(input).Should().Be("GET /api/foo?api_key=<redacted>");
    }

    [Fact]
    public void Redacts_every_query_param_value()
    {
        var input = "GET /api/foo?api_key=abc&sort=name&page=2";
        _sut.Redact(input).Should().Be("GET /api/foo?api_key=<redacted>&sort=<redacted>&page=<redacted>");
    }

    [Fact]
    public void Preserves_keys()
    {
        // Even though every value is replaced, the key shape is preserved so
        // shape-of-the-URL debugging still works.
        var input = "/api/auth/callback?code=abc&state=xyz";
        var output = _sut.Redact(input);
        output.Should().Contain("code=<redacted>");
        output.Should().Contain("state=<redacted>");
    }

    [Fact]
    public void Leaves_path_without_querystring_untouched()
    {
        var input = "GET /api/v1/health";
        _sut.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Handles_empty_safely()
    {
        _sut.Redact("").Should().Be("");
    }
}
