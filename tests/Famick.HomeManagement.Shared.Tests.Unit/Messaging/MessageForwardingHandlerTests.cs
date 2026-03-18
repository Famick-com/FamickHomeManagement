using Famick.HomeManagement.Core.Messaging.Messages;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Messaging;

public class MessageForwardingHandlerTests
{
    private readonly MessageForwardingHandler _handler;
    private readonly Mock<ILogger<MessageForwardingHandler>> _logger = new();

    public MessageForwardingHandlerTests()
    {
        _handler = new MessageForwardingHandler(_logger.Object);
    }

    [Fact]
    public async Task HandleAsync_SessionExpired_CompletesSuccessfully()
    {
        // Arrange
        var message = new SessionExpiredMessage("token expired") { Source = "blazor" };

        // Act
        var act = () => _handler.HandleAsync(message);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_MustChangePassword_CompletesSuccessfully()
    {
        // Arrange
        var message = new MustChangePasswordMessage("server requires change") { Source = "maui" };

        // Act
        var act = () => _handler.HandleAsync(message);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_MustAcceptTerms_CompletesSuccessfully()
    {
        // Arrange
        var message = new MustAcceptTermsMessage("terms updated") { Source = "blazor" };

        // Act
        var act = () => _handler.HandleAsync(message);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_AuthenticationStateChanged_CompletesSuccessfully()
    {
        // Arrange
        var message = new AuthenticationStateChangedMessage(true) { Source = "blazor" };

        // Act
        var act = () => _handler.HandleAsync(message);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_SubscriptionStateChanged_CompletesSuccessfully()
    {
        // Arrange
        var message = new SubscriptionStateChangedMessage("Pro") { Source = "blazor" };

        // Act
        var act = () => _handler.HandleAsync(message);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_EntityChanged_CompletesSuccessfully()
    {
        // Arrange
        var message = new EntityChangedMessage
        {
            EntityType = "Product",
            EntityId = Guid.NewGuid(),
            ChangeType = ChangeType.Created,
            Source = "blazor"
        };

        // Act
        var act = () => _handler.HandleAsync(message);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
