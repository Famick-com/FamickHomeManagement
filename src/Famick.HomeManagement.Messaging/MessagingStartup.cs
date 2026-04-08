using Famick.HomeManagement.Messaging.Configuration;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Messaging.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Messaging;

public static class MessagingStartup
{
    /// <summary>
    /// Registers the unified messaging service, template renderer, and default transports.
    /// Call from InfrastructureStartup after notification services are registered.
    /// The caller must also register:
    /// - IMessageRecipientResolver (infrastructure-specific, resolves user data)
    /// - ISmsService (NullSmsService for self-hosted, real implementation for cloud)
    /// </summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<ComplianceSettings>(configuration.GetSection(ComplianceSettings.SectionName));

        // Template renderer (singleton — caches templates from embedded resources)
        services.AddSingleton<StubbleTemplateRenderer>();
        services.AddSingleton<ITemplateRenderer>(sp => sp.GetRequiredService<StubbleTemplateRenderer>());

        // Transports (scoped — may depend on scoped services like IEmailService, INotificationService)
        services.AddScoped<IMessageTransport, EmailMessageTransport>();
        services.AddScoped<IMessageTransport, SmsMessageTransport>();
        services.AddScoped<IMessageTransport, InAppMessageTransport>();

        // No-op SMS service (cloud overrides with real implementation)
        services.AddSingleton<ISmsService, NullSmsService>();

        // Orchestrator
        services.AddScoped<IMessageService, MessageService>();

        return services;
    }

    /// <summary>
    /// Validates that all expected message templates are present.
    /// Call during application startup for fail-fast behavior.
    /// </summary>
    public static void ValidateMessagingTemplates(this IServiceProvider services)
    {
        var renderer = services.GetRequiredService<StubbleTemplateRenderer>();
        renderer.ValidateAllTemplatesExist();
    }
}
