using Famick.HomeManagement.Logging.Redaction.Redactors;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Logging.Redaction;

public class HighEntropyPathRedactorTests
{
    private readonly HighEntropyPathRedactor _sut = new();

    [Fact]
    public void Redacts_long_base64url_path_segment()
    {
        var input = "GET /api/v1/calendar/feed/aB-cDeF_gHiJkLmNoPqRsTuVwXyZ12345 200";
        _sut.Redact(input).Should().Be("GET /api/v1/calendar/feed/<redacted> 200");
    }

    [Fact]
    public void Redacts_long_hex_path_segment()
    {
        var input = "GET /api/v1/shares/0123456789abcdef0123456789abcdef 200";
        _sut.Redact(input).Should().Be("GET /api/v1/shares/<redacted> 200");
    }

    [Fact]
    public void Leaves_uuids_untouched()
    {
        // 36-char UUID with dashes — passes the hex regex's 16+ threshold only on
        // short subsegments (max 8 hex), so it survives unredacted. This is the
        // intended behavior per the redactor's design comment.
        var input = "GET /api/v1/contacts/550e8400-e29b-41d4-a716-446655440000 200";
        _sut.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Leaves_short_id_path_segments_untouched()
    {
        var input = "GET /api/v1/products/12345 200";
        _sut.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Redacts_token_followed_by_querystring()
    {
        var input = "GET /api/v1/r/aB-cDeF_gHiJkLmNoPqRsTuVwXyZ12345?fmt=ics 200";
        _sut.Redact(input).Should().Be("GET /api/v1/r/<redacted>?fmt=ics 200");
    }

    [Fact]
    public void Handles_empty_safely()
    {
        _sut.Redact("").Should().Be("");
    }
}
