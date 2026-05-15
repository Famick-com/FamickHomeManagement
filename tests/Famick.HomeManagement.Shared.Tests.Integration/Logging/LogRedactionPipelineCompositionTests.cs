using Famick.HomeManagement.Logging.Redaction;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Logging;

/// <summary>
/// Phase 3 chunk 3.D.2 — composition smoke test. Asserts that the redaction
/// pipeline produces a logger that actually scrubs sensitive content when
/// assembled the way both Web Program.cs files assemble it:
///
///   services.AddLoggingRedaction().AddDefaultRedactors();
///   ...
///   builder.Host.UseSerilog((ctx, services, cfg) =&gt; cfg
///       ...
///       .Enrich.WithFamickRedaction(services));
///
/// This test mirrors that wiring against an in-memory ServiceCollection +
/// Serilog config so a regression in any of the three extension methods —
/// or in how they compose — fails CI rather than reaching staging logs.
///
/// What this test does NOT cover: a Program.cs that stops calling
/// AddDefaultRedactors or stops adding the enricher to UseSerilog. That kind
/// of wiring drift is a Code Review / PR-template responsibility. The unit
/// tests in tests/Famick.HomeManagement.Shared.Tests.Unit/Logging/Redaction/
/// already pin every individual redactor's behavior; this file pins the
/// composition.
/// </summary>
public class LogRedactionPipelineCompositionTests
{
    [Fact]
    public void Pipeline_redacts_authorization_bearer_in_property_value()
    {
        var (logger, sink) = BuildPipelineLikeProgramCs();

        logger.Information("Request: {Header}", "Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.payload.signature");

        var ev = sink.Events.Should().ContainSingle().Subject;
        var header = (string)((ScalarValue)ev.Properties["Header"]).Value!;
        header.Should().Be("Authorization: <redacted>");
    }

    [Fact]
    public void Pipeline_redacts_high_entropy_path_segment_in_property_value()
    {
        var (logger, sink) = BuildPipelineLikeProgramCs();

        // 24-char base64url-ish segment — what HighEntropyPathRedactor scrubs.
        logger.Information("Hit {Url}", "/api/v1/share/aB-cDeF_gHiJkLmNoPqRsTuVwXyZ12345");

        var ev = sink.Events.Should().ContainSingle().Subject;
        var url = (string)((ScalarValue)ev.Properties["Url"]).Value!;
        url.Should().Contain("/<redacted>");
        url.Should().NotContain("aB-cDeF_gHiJkLmNoPqRsTuVwXyZ12345");
    }

    [Fact]
    public void Pipeline_redacts_querystring_values_in_property_value()
    {
        var (logger, sink) = BuildPipelineLikeProgramCs();

        logger.Information("Callback {Url}", "https://app.famick.com/auth/cb?code=secret-code&state=secret-state");

        var ev = sink.Events.Should().ContainSingle().Subject;
        var url = (string)((ScalarValue)ev.Properties["Url"]).Value!;
        url.Should().Contain("code=<redacted>");
        url.Should().Contain("state=<redacted>");
        url.Should().NotContain("secret-code");
        url.Should().NotContain("secret-state");
    }

    [Fact]
    public void Pipeline_renders_message_using_redacted_properties()
    {
        // Serilog renders the message template against the property dictionary at
        // sink time. Redacting properties is enough — the rendered text picks up
        // the redacted values automatically. Pin this so a refactor of
        // SerilogRedactingEnricher.Enrich doesn't accidentally start mutating
        // only the rendered string while leaving properties intact (or vice versa).
        var (logger, sink) = BuildPipelineLikeProgramCs();

        logger.Information("Got {Header}", "Authorization: Bearer real-token");

        var ev = sink.Events.Should().ContainSingle().Subject;
        ev.RenderMessage().Should().Contain("<redacted>").And.NotContain("real-token");
    }

    private static (ILogger Logger, CapturingSink Sink) BuildPipelineLikeProgramCs()
    {
        // Same call sequence as both Program.cs files (self-hosted + cloud).
        var services = new ServiceCollection();
        services.AddLoggingRedaction().AddDefaultRedactors();
        var serviceProvider = services.BuildServiceProvider();

        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithFamickRedaction(serviceProvider)
            .WriteTo.Sink(sink)
            .CreateLogger();

        return (logger, sink);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
