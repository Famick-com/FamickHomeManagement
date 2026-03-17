namespace Famick.HomeManagement.Core.Messaging;

/// <summary>
/// Lightweight in-process pub/sub message bus.
/// Scoped per Blazor circuit / per MAUI app instance.
/// </summary>
public interface IMessageBus
{
    /// <summary>Publish a message to all subscribers of TMessage.</summary>
    void Publish<TMessage>(TMessage message) where TMessage : IMessage;

    /// <summary>
    /// Subscribe to messages of type TMessage.
    /// Returns an IDisposable that unsubscribes when disposed.
    /// </summary>
    IDisposable Subscribe<TMessage>(Action<TMessage> handler) where TMessage : IMessage;

    /// <summary>
    /// Subscribe to messages of type TMessage with an async handler.
    /// Returns an IDisposable that unsubscribes when disposed.
    /// </summary>
    IDisposable Subscribe<TMessage>(Func<TMessage, Task> handler) where TMessage : IMessage;
}
