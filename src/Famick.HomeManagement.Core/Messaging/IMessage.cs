namespace Famick.HomeManagement.Core.Messaging;

/// <summary>
/// Marker interface for all pub/sub messages.
/// Provides metadata for logging, forwarding, and correlation.
/// </summary>
public interface IMessage
{
    /// <summary>UTC timestamp when the message was created.</summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>Optional correlation ID for tracing related messages.</summary>
    Guid? CorrelationId { get; }

    /// <summary>Identifies where the message originated (e.g., "blazor", "maui", "api").</summary>
    string Source { get; }
}
