namespace Famick.HomeManagement.Core.Messaging;

/// <summary>
/// Non-generic marker interface for pipeline handlers.
/// Enables DI collection resolution via IEnumerable&lt;IMessageHandler&gt;.
/// </summary>
public interface IMessageHandler;

/// <summary>
/// Handles messages of a specific type in the pipeline.
/// Implementations are resolved from DI and invoked after subscriber callbacks.
/// Use for cross-cutting concerns: forwarding, logging, analytics.
/// </summary>
public interface IMessageHandler<in TMessage> : IMessageHandler where TMessage : IMessage
{
    Task HandleAsync(TMessage message, CancellationToken ct = default);
}
