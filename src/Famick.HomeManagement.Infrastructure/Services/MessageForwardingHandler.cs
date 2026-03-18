using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.Core.Messaging.Messages;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Forwards select messages to an external ingestion endpoint.
/// Currently a stub — implement HandleAsync methods when the ingestion endpoint is ready.
/// </summary>
public class MessageForwardingHandler :
    IMessageHandler<SessionExpiredMessage>,
    IMessageHandler<MustChangePasswordMessage>,
    IMessageHandler<MustAcceptTermsMessage>,
    IMessageHandler<AuthenticationStateChangedMessage>,
    IMessageHandler<SubscriptionStateChangedMessage>,
    IMessageHandler<EntityChangedMessage>
{
    private readonly ILogger<MessageForwardingHandler> _logger;

    public MessageForwardingHandler(ILogger<MessageForwardingHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(SessionExpiredMessage message, CancellationToken ct = default)
    {
        _logger.LogDebug("MessageForwarding: SessionExpired from {Source}", message.Source);
        return Task.CompletedTask;
    }

    public Task HandleAsync(MustChangePasswordMessage message, CancellationToken ct = default)
    {
        _logger.LogDebug("MessageForwarding: MustChangePassword from {Source}", message.Source);
        return Task.CompletedTask;
    }

    public Task HandleAsync(MustAcceptTermsMessage message, CancellationToken ct = default)
    {
        _logger.LogDebug("MessageForwarding: MustAcceptTerms from {Source}", message.Source);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AuthenticationStateChangedMessage message, CancellationToken ct = default)
    {
        _logger.LogDebug("MessageForwarding: AuthStateChanged isAuthenticated={IsAuth} from {Source}",
            message.Value, message.Source);
        return Task.CompletedTask;
    }

    public Task HandleAsync(SubscriptionStateChangedMessage message, CancellationToken ct = default)
    {
        _logger.LogDebug("MessageForwarding: SubscriptionStateChanged tier={Tier} from {Source}",
            message.Value, message.Source);
        return Task.CompletedTask;
    }

    public Task HandleAsync(EntityChangedMessage message, CancellationToken ct = default)
    {
        _logger.LogDebug("MessageForwarding: EntityChanged {EntityType}/{EntityId} {ChangeType} from {Source}",
            message.EntityType, message.EntityId, message.ChangeType, message.Source);
        return Task.CompletedTask;
    }
}
