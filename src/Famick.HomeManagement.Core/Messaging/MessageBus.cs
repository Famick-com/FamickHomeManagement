using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Core.Messaging;

/// <summary>
/// In-process message bus using ConcurrentDictionary of subscriptions.
/// Thread-safe for Blazor Server (multiple threads per circuit).
/// </summary>
public sealed class MessageBus : IMessageBus
{
    private readonly ILogger<MessageBus> _logger;
    private readonly ConcurrentDictionary<Type, List<SubscriptionEntry>> _subscriptions = new();
    private readonly IEnumerable<IMessageHandler> _handlers;

    public MessageBus(ILogger<MessageBus> logger, IEnumerable<IMessageHandler> handlers)
    {
        _logger = logger;
        _handlers = handlers;
    }

    public void Publish<TMessage>(TMessage message) where TMessage : IMessage
    {
        var type = typeof(TMessage);

        // Invoke registered subscriptions
        if (_subscriptions.TryGetValue(type, out var entries))
        {
            List<SubscriptionEntry> snapshot;
            lock (entries) { snapshot = [.. entries]; }

            foreach (var entry in snapshot)
            {
                try
                {
                    entry.Invoke(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in subscriber for {MessageType}", type.Name);
                }
            }
        }

        // Invoke pipeline handlers (for forwarding, logging, etc.)
        foreach (var handler in _handlers)
        {
            if (handler is IMessageHandler<TMessage> typed)
            {
                try
                {
                    _ = typed.HandleAsync(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in handler for {MessageType}", type.Name);
                }
            }
        }
    }

    public IDisposable Subscribe<TMessage>(Action<TMessage> handler) where TMessage : IMessage
    {
        var entry = new SubscriptionEntry(msg =>
        {
            handler((TMessage)msg);
            return Task.CompletedTask;
        });
        return AddSubscription(typeof(TMessage), entry);
    }

    public IDisposable Subscribe<TMessage>(Func<TMessage, Task> handler) where TMessage : IMessage
    {
        var entry = new SubscriptionEntry(msg => handler((TMessage)msg));
        return AddSubscription(typeof(TMessage), entry);
    }

    private IDisposable AddSubscription(Type messageType, SubscriptionEntry entry)
    {
        var list = _subscriptions.GetOrAdd(messageType, _ => new List<SubscriptionEntry>());
        lock (list) { list.Add(entry); }
        return new Unsubscriber(() =>
        {
            lock (list) { list.Remove(entry); }
        });
    }

    private sealed class SubscriptionEntry(Func<object, Task> callback)
    {
        public void Invoke(object message) => callback(message);
    }

    private sealed class Unsubscriber(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;
        public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }
}
