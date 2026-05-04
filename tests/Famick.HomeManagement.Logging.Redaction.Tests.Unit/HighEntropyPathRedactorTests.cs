using Famick.HomeManagement.Logging.Redaction.Redactors;
using FluentAssertions;

namespace Famick.HomeManagement.Logging.Redaction.Tests.Unit;

public class HighEntropyPathRedactorTests
{
    private readonly HighEntropyPathRedactor _redactor = new();

    [Fact]
    public void Redacts_long_base64url_segment()
    {
        var input = "GET /calendar/ics/abcdefABCDEF1234567890_-AB returned 200";
        _redactor.Redact(input).Should().Contain("/calendar/ics/<redacted>")
            .And.NotContain("abcdefABCDEF1234567890_-AB");
    }

    [Fact]
    public void Redacts_long_hex_segment()
    {
        var input = "GET /share/recipe/0123456789abcdef00 returned 200";
        _redactor.Redact(input).Should().Contain("/share/recipe/<redacted>");
    }

    [Fact]
    public void Leaves_short_path_segments_alone()
    {
        var input = "GET /api/users/42 returned 200";
        _redactor.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Leaves_resource_slugs_alone()
    {
        // Slugs and short hex IDs should not trigger the redaction.
        var input = "GET /api/products/grocery-store-inventory returned 200";
        _redactor.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Handles_null_and_empty_input()
    {
        _redactor.Redact("").Should().Be("");
        _redactor.Redact(null!).Should().Be(null);
    }

    [Fact]
    public void Redacts_token_at_end_of_string()
    {
        var input = "GET /share/recipe/abcdefABCDEF1234567890_-AB";
        _redactor.Redact(input).Should().EndWith("/<redacted>");
    }

    [Fact]
    public void Redacts_multiple_high_entropy_segments_in_one_message()
    {
        var input = "Following redirect /share/recipe/abcdefABCDEF1234567890_-AB to /calendar/ics/0123456789abcdef00";
        var result = _redactor.Redact(input);
        result.Should().NotContain("abcdefABCDEF1234567890_-AB");
        result.Should().NotContain("0123456789abcdef00");
        result.Should().Contain("/<redacted>");
    }
}
