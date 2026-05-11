using Famick.HomeManagement.Logging.Redaction;
using Famick.HomeManagement.Logging.Redaction.Redactors;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Logging.Redaction;

public class CompositeRedactorTests
{
    [Fact]
    public void Applies_every_registered_redactor()
    {
        var sut = new CompositeRedactor(new IRedactor[]
        {
            new AuthorizationHeaderRedactor(),
            new QueryStringRedactor(),
        });

        var input = "Authorization: Bearer abc && /api/foo?token=xyz";
        var output = sut.Redact(input);

        output.Should().Contain("Authorization: <redacted>");
        output.Should().Contain("token=<redacted>");
    }

    [Fact]
    public void Skips_nested_CompositeRedactor_to_avoid_infinite_recursion()
    {
        var inner = new CompositeRedactor(new IRedactor[] { new AuthorizationHeaderRedactor() });
        var outer = new CompositeRedactor(new IRedactor[] { inner, new QueryStringRedactor() });

        var input = "Authorization: Bearer abc && /api/foo?token=xyz";
        var output = outer.Redact(input);

        // The Authorization header isn't redacted because the inner composite was filtered out;
        // only the QueryStringRedactor applies. This documents the de-cycling behavior.
        output.Should().Contain("Authorization: Bearer abc");
        output.Should().Contain("token=<redacted>");
    }

    [Fact]
    public void Returns_input_unchanged_when_no_redactor_matches()
    {
        var sut = new CompositeRedactor(new IRedactor[]
        {
            new AuthorizationHeaderRedactor(),
            new QueryStringRedactor(),
        });

        var input = "GET /api/v1/health 200 OK";
        sut.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Handles_empty_safely()
    {
        var sut = new CompositeRedactor(Array.Empty<IRedactor>());
        sut.Redact("").Should().Be("");
    }
}
