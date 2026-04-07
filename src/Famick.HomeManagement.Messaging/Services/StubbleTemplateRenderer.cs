using System.Collections.Concurrent;
using System.Reflection;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.Extensions.Logging;
using Stubble.Core;
using Stubble.Core.Builders;

namespace Famick.HomeManagement.Messaging.Services;

/// <summary>
/// Renders Mustache templates loaded from embedded resources using Stubble.
/// Templates are cached after first load.
/// </summary>
public class StubbleTemplateRenderer : ITemplateRenderer
{
    private readonly StubbleVisitorRenderer _stubble;
    private readonly ILogger<StubbleTemplateRenderer> _logger;
    private readonly ConcurrentDictionary<string, string?> _templateCache = new();
    private readonly Assembly _assembly;
    private readonly string _resourcePrefix;
    private string? _layoutTemplate;

    public StubbleTemplateRenderer(ILogger<StubbleTemplateRenderer> logger)
    {
        _logger = logger;
        _stubble = new StubbleBuilder().Build();
        _assembly = typeof(StubbleTemplateRenderer).Assembly;
        _resourcePrefix = "Famick.HomeManagement.Messaging.Templates.Templates.";
    }

    public async Task<string> RenderAsync(
        MessageType type,
        TransportChannel channel,
        IMessageData data,
        CancellationToken cancellationToken = default)
    {
        return await RenderAsync(type, channel, data, layoutContext: null, cancellationToken);
    }

    /// <summary>
    /// Renders a template with additional layout context (e.g., compliance footer data).
    /// </summary>
    public async Task<string> RenderAsync(
        MessageType type,
        TransportChannel channel,
        IMessageData data,
        IDictionary<string, object>? layoutContext,
        CancellationToken cancellationToken = default)
    {
        var templateKey = GetTemplateKey(type, channel);
        var template = LoadTemplate(templateKey);

        if (template is null)
            throw new InvalidOperationException($"Template not found: {templateKey}");

        var rendered = await _stubble.RenderAsync(template, data);

        // Wrap email-html content in the shared layout
        if (channel == TransportChannel.EmailHtml)
        {
            var layout = LoadLayoutTemplate();
            if (layout is not null)
            {
                var context = new Dictionary<string, object> { { "content", rendered } };

                if (layoutContext is not null)
                {
                    foreach (var kvp in layoutContext)
                        context[kvp.Key] = kvp.Value;
                }

                rendered = await _stubble.RenderAsync(layout, context);
            }
        }

        return rendered;
    }

    public bool HasTemplate(MessageType type, TransportChannel channel)
    {
        var templateKey = GetTemplateKey(type, channel);
        return LoadTemplate(templateKey) is not null;
    }

    /// <summary>
    /// Validates that all expected templates exist. Call during startup for fail-fast behavior.
    /// </summary>
    public void ValidateAllTemplatesExist()
    {
        var missing = new List<string>();

        foreach (var type in Enum.GetValues<MessageType>())
        {
            var isTransactional = type.IsTransactional();

            // All types need email-html, email-text, and subject
            ValidateTemplate(type, TransportChannel.EmailHtml, missing);
            ValidateTemplate(type, TransportChannel.EmailText, missing);

            // Subject template (special — not a TransportChannel, loaded by convention)
            var subjectKey = GetSubjectTemplateKey(type);
            if (LoadTemplate(subjectKey) is null)
                missing.Add(subjectKey);

            // Non-transactional types need push, in-app, and sms templates
            if (!isTransactional)
            {
                ValidateTemplate(type, TransportChannel.Push, missing);
                ValidateTemplate(type, TransportChannel.InApp, missing);
                ValidateTemplate(type, TransportChannel.Sms, missing);
            }
        }

        if (missing.Count > 0)
        {
            var missingList = string.Join(", ", missing);
            throw new InvalidOperationException($"Missing message templates: {missingList}");
        }

        _logger.LogInformation("All message templates validated successfully");
    }

    /// <summary>
    /// Renders the subject template for a message type.
    /// </summary>
    public async Task<string> RenderSubjectAsync(
        MessageType type,
        IMessageData data,
        CancellationToken cancellationToken = default)
    {
        var templateKey = GetSubjectTemplateKey(type);
        var template = LoadTemplate(templateKey);

        if (template is null)
            throw new InvalidOperationException($"Subject template not found: {templateKey}");

        return await _stubble.RenderAsync(template, data);
    }

    private void ValidateTemplate(MessageType type, TransportChannel channel, List<string> missing)
    {
        var key = GetTemplateKey(type, channel);
        if (LoadTemplate(key) is null)
            missing.Add(key);
    }

    private string? LoadTemplate(string templateKey)
    {
        return _templateCache.GetOrAdd(templateKey, key =>
        {
            var resourceName = $"{_resourcePrefix}{key}";
            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                _logger.LogDebug("Template resource not found: {ResourceName}", resourceName);
                return null;
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    private string? LoadLayoutTemplate()
    {
        if (_layoutTemplate is not null)
            return _layoutTemplate;

        var resourceName = $"{_resourcePrefix}_layout.email_html.mustache";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            // Try alternate naming convention
            var altResourceName = $"{_resourcePrefix}_layout.email-html.mustache";
            using var altStream = _assembly.GetManifestResourceStream(altResourceName);
            if (altStream is null)
            {
                _logger.LogWarning("Layout template not found");
                return null;
            }
            using var altReader = new StreamReader(altStream);
            _layoutTemplate = altReader.ReadToEnd();
            return _layoutTemplate;
        }

        using var reader = new StreamReader(stream);
        _layoutTemplate = reader.ReadToEnd();
        return _layoutTemplate;
    }

    private static string GetTemplateKey(MessageType type, TransportChannel channel)
    {
        var typeName = type.ToString();
        var channelName = channel switch
        {
            TransportChannel.EmailHtml => "email-html",
            TransportChannel.EmailText => "email-text",
            TransportChannel.Sms => "sms",
            TransportChannel.Push => "push",
            TransportChannel.InApp => "in-app",
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };

        // Embedded resources use dots for directory separators
        return $"{typeName}.{channelName}.mustache";
    }

    private static string GetSubjectTemplateKey(MessageType type)
    {
        return $"{type}.subject.mustache";
    }
}
