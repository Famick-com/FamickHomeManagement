using Famick.HomeManagement.Logging.Redaction.Redactors;
using FluentAssertions;

namespace Famick.HomeManagement.Logging.Redaction.Tests.Unit;

public class QueryStringRedactorTests
{
    private readonly QueryStringRedactor _redactor = new();

    [Fact]
    public void Redacts_single_query_parameter_value()
    {
        var input = "GET /api/foo?api_key=secret123 returned 200";
        var result = _redactor.Redact(input);
        result.Should().Contain("?api_key=<redacted>");
        result.Should().NotContain("secret123");
    }

    [Fact]
    public void Redacts_multiple_query_parameters()
    {
        var input = "GET /api/foo?api_key=secret&token=abc123&sort=name returned 200";
        var result = _redactor.Redact(input);
        result.Should().Contain("?api_key=<redacted>");
        result.Should().Contain("&token=<redacted>");
        result.Should().Contain("&sort=<redacted>");
    }

    [Fact]
    public void Leaves_pathless_strings_alone()
    {
        var input = "Service started successfully";
        _redactor.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Preserves_query_parameter_keys()
    {
        var input = "GET /api/foo?api_key=secret&sort=name returned 200";
        var result = _redactor.Redact(input);
        result.Should().Contain("api_key=");
        result.Should().Contain("sort=");
    }
}
