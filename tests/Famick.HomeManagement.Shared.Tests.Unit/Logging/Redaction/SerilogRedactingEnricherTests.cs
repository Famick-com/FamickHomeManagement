using Famick.HomeManagement.Logging.Redaction;
using Famick.HomeManagement.Logging.Redaction.Redactors;
using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Famick.HomeManagement.Shared.Tests.Unit.Logging.Redaction;

public class SerilogRedactingEnricherTests
{
    [Fact]
    public void Rewrites_property_strings_through_the_redactor()
    {
        var (logger, sink) = BuildLogger();
        logger.Information("Request: {Header}", "Authorization: Bearer abc.def.ghi");

        var captured = sink.Events.Should().ContainSingle().Subject;
        var headerProp = (ScalarValue)captured.Properties["Header"];
        ((string)headerProp.Value!).Should().Be("Authorization: <redacted>");
    }

    [Fact]
    public void Rendered_message_uses_redacted_property_values()
    {
        var (logger, sink) = BuildLogger();
        logger.Information("Caller sent {Header}", "Authorization: Bearer secret");

        var captured = sink.Events.Should().ContainSingle().Subject;
        var rendered = captured.RenderMessage();
        rendered.Should().Be("Caller sent \"Authorization: <redacted>\"");
    }

    [Fact]
    public void Leaves_unrelated_properties_untouched()
    {
        var (logger, sink) = BuildLogger();
        logger.Information("Hit {Path} with status {Status}", "/api/v1/health", 200);

        var captured = sink.Events.Should().ContainSingle().Subject;
        // The path doesn't match any redactor rule; the status is an int (non-string scalar).
        ((string)((ScalarValue)captured.Properties["Path"]).Value!).Should().Be("/api/v1/health");
        ((int)((ScalarValue)captured.Properties["Status"]).Value!).Should().Be(200);
    }

    [Fact]
    public void Redacts_querystring_values_alongside_authorization_headers()
    {
        var (logger, sink) = BuildLogger();
        logger.Information("Request {Url}", "/api/auth/callback?code=abc&state=xyz");

        var captured = sink.Events.Should().ContainSingle().Subject;
        var url = (string)((ScalarValue)captured.Properties["Url"]).Value!;
        url.Should().Contain("code=<redacted>");
        url.Should().Contain("state=<redacted>");
    }

    private static (ILogger logger, CapturingSink sink) BuildLogger()
    {
        var composite = new CompositeRedactor(new IRedactor[]
        {
            new HighEntropyPathRedactor(),
            new AuthorizationHeaderRedactor(),
            new QueryStringRedactor(),
        });
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SerilogRedactingEnricher(composite))
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
