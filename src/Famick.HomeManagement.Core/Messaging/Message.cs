namespace Famick.HomeManagement.Core.Messaging;

/// <summary>
/// Base class for messages without a payload.
/// </summary>
public abstract class Message : IMessage
{
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public Guid? CorrelationId { get; init; }
    public string Source { get; init; } = "unknown";
}

/// <summary>
/// Base class for messages carrying a strongly-typed payload.
/// </summary>
public abstract class Message<T> : Message
{
    public T Value { get; }

    protected Message(T value)
    {
        Value = value;
    }
}
