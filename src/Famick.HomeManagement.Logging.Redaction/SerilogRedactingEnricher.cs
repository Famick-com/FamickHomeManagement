using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Famick.HomeManagement.Logging.Redaction;

/// <summary>
/// Serilog enricher that applies the configured <see cref="IRedactor"/> to every
/// string-valued property on the log event before the sink renders it. Because
/// Serilog renders message templates against the property dictionary at sink time,
/// rewriting a property here also rewrites the rendered message — no separate
/// message-template rewrite needed.
///
/// Limitations:
/// - Only <c>ScalarValue</c> properties with a <see cref="string"/> value are walked.
///   <c>SequenceValue</c>, <c>StructureValue</c>, and <c>DictionaryValue</c> are left
///   untouched. In practice ASP.NET Core's request/response logs put header values
///   and URLs as flat strings, so this covers the common leak paths.
/// - Literal text in the template itself (the unparameterized parts) is NOT redacted.
///   <c>Log.Information("Bearer abc123")</c> still leaks; <c>Log.Information("Token: {Token}", "abc123")</c>
///   redacts. Properly structured logs are unaffected by this limitation.
/// </summary>
public sealed class SerilogRedactingEnricher : ILogEventEnricher
{
    private readonly IRedactor _redactor;

    public SerilogRedactingEnricher(IRedactor redactor)
    {
        _redactor = redactor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Snapshot the keys so we can mutate the dictionary inside the loop via
        // AddOrUpdateProperty without "collection modified" exceptions.
        foreach (var key in logEvent.Properties.Keys.ToArray())
        {
            if (logEvent.Properties[key] is not ScalarValue { Value: string original }) continue;
            var redacted = _redactor.Redact(original);
            if (redacted != original)
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, redacted));
            }
        }
    }
}

/// <summary>
/// Serilog config sugar. Lets <c>UseSerilog((ctx, services, cfg) =&gt; cfg.Enrich.WithFamickRedaction(services))</c>
/// pull the singleton <see cref="IRedactor"/> from DI without callers having to
/// build the enricher by hand.
/// </summary>
public static class SerilogRedactionExtensions
{
    public static LoggerConfiguration WithFamickRedaction(
        this LoggerEnrichmentConfiguration enrich,
        IServiceProvider services)
    {
        // Resolve CompositeRedactor directly rather than IRedactor — MS DI's
        // "last registration wins" rule for GetService<T> would otherwise hand
        // back the last default redactor (currently QueryStringRedactor)
        // instead of the composite that applies all of them.
        var redactor = (CompositeRedactor?)services.GetService(typeof(CompositeRedactor))
            ?? throw new InvalidOperationException(
                "CompositeRedactor is not registered. Call services.AddLoggingRedaction() before UseSerilog.");
        return enrich.With(new SerilogRedactingEnricher(redactor));
    }
}
