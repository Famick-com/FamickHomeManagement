using Famick.HomeManagement.Logging.Redaction.Redactors;
using FluentAssertions;

namespace Famick.HomeManagement.Logging.Redaction.Tests.Unit;

public class CompositeRedactorTests
{
    [Fact]
    public void Applies_every_registered_redactor()
    {
        var composite = new CompositeRedactor(new IRedactor[]
        {
            new HighEntropyPathRedactor(),
            new AuthorizationHeaderRedactor(),
            new QueryStringRedactor()
        });

        var input = "GET /share/recipe/abcdefABCDEF1234567890_-AB?api_key=secret returned 200, "
                  + "Authorization: Bearer eyJhbGciOiJSUzI1NiIsImtpZCI6ImFi.SignaturE";

        var result = composite.Redact(input);

        // High-entropy path segment redacted
        result.Should().NotContain("abcdefABCDEF1234567890_-AB");
        // Bearer token redacted
        result.Should().NotContain("eyJhbGciOiJSUzI1NiIsImtpZCI6ImFi");
        // Query value redacted
        result.Should().NotContain("?api_key=secret");
        // All three redaction markers present
        result.Should().Contain("<redacted>");
    }

    [Fact]
    public void Empty_redactor_set_passes_input_through()
    {
        var composite = new CompositeRedactor(Array.Empty<IRedactor>());
        var input = "Authorization: Bearer secret";
        composite.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Skips_self_to_avoid_infinite_recursion_when_registered_alongside_components()
    {
        // Defensive: the StartupExtensions registers CompositeRedactor as IRedactor; if it were
        // also enumerated as a member, we'd hit infinite recursion. CompositeRedactor filters
        // itself out of its own input list.
        var inner = new HighEntropyPathRedactor();
        var composite = new CompositeRedactor(new IRedactor[] { inner });
        var nested = new CompositeRedactor(new IRedactor[] { composite, inner });

        var input = "GET /share/recipe/abcdefABCDEF1234567890_-AB returned 200";
        var act = () => nested.Redact(input);
        act.Should().NotThrow();
    }
}
