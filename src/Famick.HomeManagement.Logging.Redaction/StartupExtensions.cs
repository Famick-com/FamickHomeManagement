using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Logging.Redaction;

public static class StartupExtensions
{
    /// <summary>
    /// Registers <see cref="CompositeRedactor"/> as the singleton that wraps every
    /// registered <see cref="IRedactor"/> rule.
    ///
    /// Consumers resolve <see cref="CompositeRedactor"/> directly (e.g. via
    /// <c>services.GetRequiredService&lt;CompositeRedactor&gt;()</c>) — we deliberately
    /// do NOT register the composite as <see cref="IRedactor"/> because that creates
    /// a circular dependency: the composite's <c>IEnumerable&lt;IRedactor&gt;</c>
    /// dependency would resolve the composite-as-IRedactor factory, which asks for
    /// the composite while it's still being built. MS DI throws on circular before
    /// the composite's de-cycling filter has a chance to run.
    ///
    /// Phase 0 wires this in but registers no concrete <see cref="IRedactor"/> rules —
    /// Phase 3 calls <see cref="AddDefaultRedactors"/> to enable the regex/header/query rules.
    /// </summary>
    public static IServiceCollection AddLoggingRedaction(this IServiceCollection services)
    {
        services.AddSingleton<CompositeRedactor>();
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
