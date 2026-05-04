using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Logging.Redaction;

/// <summary>
/// <see cref="ILoggerProvider"/> decorator that pipes every formatted log message
/// through the configured <see cref="IRedactor"/> before forwarding to the inner
/// providers. Wrapping at the provider layer means redaction runs once per log call,
/// regardless of how many sinks are attached (Console, Serilog, CloudWatch, etc.).
///
/// Phase 0 wires this in but registers no <see cref="IRedactor"/> rules — Phase 3
/// turns the regex/header/query rules on. Until then this is a pass-through.
/// </summary>
public sealed class RedactingLoggerProvider : ILoggerProvider
{
    private readonly ILoggerProvider _inner;
    private readonly IRedactor _redactor;

    public RedactingLoggerProvider(ILoggerProvider inner, IRedactor redactor)
    {
        _inner = inner;
        _redactor = redactor;
    }

    public ILogger CreateLogger(string categoryName)
        => new RedactingLogger(_inner.CreateLogger(categoryName), _redactor);

    public void Dispose() => _inner.Dispose();

    private sealed class RedactingLogger : ILogger
    {
        private readonly ILogger _inner;
        private readonly IRedactor _redactor;

        public RedactingLogger(ILogger inner, IRedactor redactor)
        {
            _inner = inner;
            _redactor = redactor;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => _inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Format once, redact, then pass through with a formatter that returns
            // the already-redacted string so structured log sinks see the scrubbed value.
            var formatted = formatter(state, exception);
            var redacted = _redactor.Redact(formatted);
            _inner.Log(logLevel, eventId, redacted, exception, static (s, _) => s);
        }
    }
}
