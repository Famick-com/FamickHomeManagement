using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Core.Messaging;
using CoreMessages = Famick.HomeManagement.Core.Messaging.Messages;
using MobileMessages = Famick.HomeManagement.Mobile.Messages;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Bridges auth-critical messages from WeakReferenceMessenger to IMessageBus.
/// Enables the future forwarding handler to capture messages from both web and mobile.
/// </summary>
public sealed class MessageBusAdapter : IDisposable
{
    private readonly IMessageBus _messageBus;

    public MessageBusAdapter(IMessageBus messageBus)
    {
        _messageBus = messageBus;

        WeakReferenceMessenger.Default.Register<MobileMessages.SessionExpiredMessage>(this, (_, msg) =>
            _messageBus.Publish(new CoreMessages.SessionExpiredMessage(msg.Value) { Source = "maui" }));

        WeakReferenceMessenger.Default.Register<MobileMessages.MustChangePasswordMessage>(this, (_, msg) =>
            _messageBus.Publish(new CoreMessages.MustChangePasswordMessage(msg.Value) { Source = "maui" }));

        WeakReferenceMessenger.Default.Register<MobileMessages.MustAcceptTermsMessage>(this, (_, msg) =>
            _messageBus.Publish(new CoreMessages.MustAcceptTermsMessage(msg.Value) { Source = "maui" }));
    }

    public void Dispose() => WeakReferenceMessenger.Default.UnregisterAll(this);
}
