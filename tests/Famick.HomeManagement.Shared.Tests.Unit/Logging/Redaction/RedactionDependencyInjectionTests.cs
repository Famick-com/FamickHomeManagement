using Famick.HomeManagement.Logging.Redaction;
using Famick.HomeManagement.Logging.Redaction.Redactors;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Shared.Tests.Unit.Logging.Redaction;

/// <summary>
/// Guards the DI wiring contract. The Phase 3 staging deployment crashed at
/// `builder.Build()` because an earlier registration had the composite both as
/// itself AND as <see cref="IRedactor"/>, which created a cycle when the
/// composite's constructor pulled <c>IEnumerable&lt;IRedactor&gt;</c>. These tests
/// build a real service provider so a future regression of that pattern fails in
/// CI before it reaches staging.
/// </summary>
public class RedactionDependencyInjectionTests
{
    [Fact]
    public void CompositeRedactor_resolves_without_circular_dependency()
    {
        var services = new ServiceCollection();
        services.AddLoggingRedaction().AddDefaultRedactors();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var composite = provider.GetRequiredService<CompositeRedactor>();

        composite.Should().NotBeNull();
    }

    [Fact]
    public void CompositeRedactor_redacts_through_every_default_rule()
    {
        var services = new ServiceCollection();
        services.AddLoggingRedaction().AddDefaultRedactors();

        using var provider = services.BuildServiceProvider();
        var composite = provider.GetRequiredService<CompositeRedactor>();

        // One input that exercises all three default redactors: an Authorization
        // header, a high-entropy path segment, and a sensitive query param.
        var input = "Authorization: Bearer abc.def.ghi /api/v1/shares/aB-cDeF_gHiJkLmNoPqRsTuVwXyZ12345?token=secret";
        var output = composite.Redact(input);

        output.Should().Contain("Authorization: <redacted>");
        output.Should().Contain("/<redacted>");
        output.Should().Contain("token=<redacted>");
    }

    [Fact]
    public void Registering_extra_redactor_extends_composite_pipeline()
    {
        var services = new ServiceCollection();
        services.AddLoggingRedaction().AddDefaultRedactors();
        services.AddSingleton<IRedactor, StubRedactor>();

        using var provider = services.BuildServiceProvider();
        var composite = provider.GetRequiredService<CompositeRedactor>();

        composite.Redact("normal-input").Should().Be("normal-input-STUBBED");
    }

    private sealed class StubRedactor : IRedactor
    {
        public string Redact(string input) => input + "-STUBBED";
    }
}
