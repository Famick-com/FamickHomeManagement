using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.Core.Messaging.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Messaging;

public class MessageBusTests
{
    private readonly MessageBus _messageBus;
    private readonly Mock<ILogger<MessageBus>> _logger = new();

    public MessageBusTests()
    {
        _messageBus = new MessageBus(_logger.Object, Enumerable.Empty<IMessageHandler>());
    }

    #region Publish & Subscribe

    [Fact]
    public void Publish_WithSyncSubscriber_InvokesHandler()
    {
        // Arrange
        SessionExpiredMessage? received = null;
        _messageBus.Subscribe<SessionExpiredMessage>(msg => received = msg);

        // Act
        _messageBus.Publish(new SessionExpiredMessage("token expired"));

        // Assert
        received.Should().NotBeNull();
        received!.Value.Should().Be("token expired");
    }

    [Fact]
    public async Task Publish_WithAsyncSubscriber_InvokesHandler()
    {
        // Arrange
        AuthenticationStateChangedMessage? received = null;
        _messageBus.Subscribe<AuthenticationStateChangedMessage>(msg =>
        {
            received = msg;
            return Task.CompletedTask;
        });

        // Act
        _messageBus.Publish(new AuthenticationStateChangedMessage(true));

        // Assert
        await Task.Delay(10); // Allow async handler to complete
        received.Should().NotBeNull();
        received!.Value.Should().BeTrue();
    }

    [Fact]
    public void Publish_WithMultipleSubscribers_InvokesAll()
    {
        // Arrange
        var count = 0;
        _messageBus.Subscribe<SessionExpiredMessage>(_ => count++);
        _messageBus.Subscribe<SessionExpiredMessage>(_ => count++);
        _messageBus.Subscribe<SessionExpiredMessage>(_ => count++);

        // Act
        _messageBus.Publish(new SessionExpiredMessage("expired"));

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _messageBus.Publish(new SessionExpiredMessage("expired"));
        act.Should().NotThrow();
    }

    [Fact]
    public void Publish_DifferentMessageType_DoesNotInvokeUnrelatedSubscriber()
    {
        // Arrange
        var invoked = false;
        _messageBus.Subscribe<SessionExpiredMessage>(_ => invoked = true);

        // Act
        _messageBus.Publish(new MustChangePasswordMessage("change password"));

        // Assert
        invoked.Should().BeFalse();
    }

    #endregion

    #region Unsubscribe

    [Fact]
    public void Dispose_Subscription_StopsReceivingMessages()
    {
        // Arrange
        var count = 0;
        var subscription = _messageBus.Subscribe<SessionExpiredMessage>(_ => count++);

        _messageBus.Publish(new SessionExpiredMessage("first"));
        count.Should().Be(1);

        // Act
        subscription.Dispose();
        _messageBus.Publish(new SessionExpiredMessage("second"));

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public void Dispose_Subscription_Twice_DoesNotThrow()
    {
        // Arrange
        var subscription = _messageBus.Subscribe<SessionExpiredMessage>(_ => { });

        // Act & Assert
        subscription.Dispose();
        var act = () => subscription.Dispose();
        act.Should().NotThrow();
    }

    #endregion

    #region CompositeDisposable

    [Fact]
    public void CompositeDisposable_DisposesAllSubscriptions()
    {
        // Arrange
        var count1 = 0;
        var count2 = 0;
        var composite = new CompositeDisposable();
        composite.Add(_messageBus.Subscribe<SessionExpiredMessage>(_ => count1++));
        composite.Add(_messageBus.Subscribe<MustChangePasswordMessage>(_ => count2++));

        _messageBus.Publish(new SessionExpiredMessage("test"));
        _messageBus.Publish(new MustChangePasswordMessage("test"));
        count1.Should().Be(1);
        count2.Should().Be(1);

        // Act
        composite.Dispose();
        _messageBus.Publish(new SessionExpiredMessage("test2"));
        _messageBus.Publish(new MustChangePasswordMessage("test2"));

        // Assert
        count1.Should().Be(1);
        count2.Should().Be(1);
    }

    #endregion

    #region Message Metadata

    [Fact]
    public void Message_HasTimestamp()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var message = new SessionExpiredMessage("test");

        // Assert
        message.Timestamp.Should().BeOnOrAfter(before);
        message.Timestamp.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Message_DefaultSource_IsUnknown()
    {
        // Act
        var message = new SessionExpiredMessage("test");

        // Assert
        message.Source.Should().Be("unknown");
    }

    [Fact]
    public void Message_WithSource_RetainsValue()
    {
        // Act
        var message = new SessionExpiredMessage("test") { Source = "blazor" };

        // Assert
        message.Source.Should().Be("blazor");
    }

    [Fact]
    public void Message_WithCorrelationId_RetainsValue()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        // Act
        var message = new SessionExpiredMessage("test") { CorrelationId = correlationId };

        // Assert
        message.CorrelationId.Should().Be(correlationId);
    }

    #endregion

    #region Pipeline Handlers

    [Fact]
    public void Publish_WithPipelineHandler_InvokesHandler()
    {
        // Arrange
        var mockHandler = new Mock<IMessageHandler<SessionExpiredMessage>>();
        mockHandler
            .Setup(h => h.HandleAsync(It.IsAny<SessionExpiredMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var bus = new MessageBus(_logger.Object, new IMessageHandler[] { mockHandler.Object });

        // Act
        bus.Publish(new SessionExpiredMessage("expired"));

        // Assert
        mockHandler.Verify(
            h => h.HandleAsync(It.Is<SessionExpiredMessage>(m => m.Value == "expired"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Publish_WithPipelineHandler_WrongMessageType_DoesNotInvoke()
    {
        // Arrange
        var mockHandler = new Mock<IMessageHandler<SessionExpiredMessage>>();
        var bus = new MessageBus(_logger.Object, new IMessageHandler[] { mockHandler.Object });

        // Act
        bus.Publish(new MustChangePasswordMessage("change"));

        // Assert
        mockHandler.Verify(
            h => h.HandleAsync(It.IsAny<SessionExpiredMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Error Handling

    [Fact]
    public void Publish_SubscriberThrows_ContinuesWithOtherSubscribers()
    {
        // Arrange
        var secondInvoked = false;
        _messageBus.Subscribe<SessionExpiredMessage>(_ => throw new InvalidOperationException("subscriber error"));
        _messageBus.Subscribe<SessionExpiredMessage>(_ => secondInvoked = true);

        // Act
        _messageBus.Publish(new SessionExpiredMessage("test"));

        // Assert
        secondInvoked.Should().BeTrue();
    }

    [Fact]
    public void Publish_PipelineHandlerThrows_DoesNotThrow()
    {
        // Arrange
        var mockHandler = new Mock<IMessageHandler<SessionExpiredMessage>>();
        mockHandler
            .Setup(h => h.HandleAsync(It.IsAny<SessionExpiredMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("handler error"));

        var bus = new MessageBus(_logger.Object, new IMessageHandler[] { mockHandler.Object });

        // Act & Assert
        var act = () => bus.Publish(new SessionExpiredMessage("test"));
        act.Should().NotThrow();
    }

    #endregion

    #region EntityChangedMessage

    [Fact]
    public void EntityChangedMessage_CarriesAllProperties()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        // Act
        var message = new EntityChangedMessage
        {
            EntityType = "Product",
            EntityId = entityId,
            ChangeType = ChangeType.Updated,
            Source = "blazor"
        };

        // Assert
        message.EntityType.Should().Be("Product");
        message.EntityId.Should().Be(entityId);
        message.ChangeType.Should().Be(ChangeType.Updated);
        message.Source.Should().Be("blazor");
    }

    #endregion
}
