using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Logging.Redaction;

public static class StartupExtensions
{
    /// <summary>
    /// Registers <see cref="CompositeRedactor"/> and the marker that tells consumers
    /// to wrap their <see cref="ILoggerProvider"/>s with <see cref="RedactingLoggerProvider"/>.
    ///
    /// Phase 0 wires this in but registers no concrete <see cref="IRedactor"/> rules —
    /// Phase 3 calls <see cref="AddDefaultRedactors"/> to enable the regex/header/query rules.
    /// </summary>
    public static IServiceCollection AddLoggingRedaction(this IServiceCollection services)
    {
        services.AddSingleton<CompositeRedactor>();
        services.AddSingleton<IRedactor>(sp => sp.GetRequiredService<CompositeRedactor>());
        return services;
    }

    /// <summary>
    /// Registers every <see cref="IRedactor"/> implementation we ship by default.
    /// Called by Phase 3 to flip redaction from no-op to active.
    ///
    /// Order matters: high-entropy path segments first (so URL paths in log lines
    /// have their tokens stripped before query-string redaction sees the URL).
    /// </summary>
    public static IServiceCollection AddDefaultRedactors(this IServiceCollection services)
    {
        services.AddSingleton<IRedactor, Redactors.HighEntropyPathRedactor>();
        services.AddSingleton<IRedactor, Redactors.AuthorizationHeaderRedactor>();
        services.AddSingleton<IRedactor, Redactors.QueryStringRedactor>();
        return services;
    }
}
