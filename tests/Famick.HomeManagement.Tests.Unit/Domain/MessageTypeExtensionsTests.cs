using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Domain;

public class MessageTypeExtensionsTests
{
    [Theory]
    [InlineData(MessageType.CalendarReminder)]
    [InlineData(MessageType.Expiry)]
    [InlineData(MessageType.LowStock)]
    [InlineData(MessageType.TaskSummary)]
    public void IsLocallySchedulable_TrueForScheduledTypes(MessageType type)
    {
        type.IsLocallySchedulable().Should().BeTrue();
    }

    [Theory]
    [InlineData(MessageType.NewFeatures)]      // event-driven announcement — stays on push
    [InlineData(MessageType.EmailVerification)] // transactional (100+)
    [InlineData(MessageType.PasswordReset)]
    [InlineData(MessageType.PasswordChanged)]
    [InlineData(MessageType.Welcome)]
    public void IsLocallySchedulable_FalseForEventDrivenAndTransactional(MessageType type)
    {
        type.IsLocallySchedulable().Should().BeFalse();
    }

    [Fact]
    public void LocallySchedulable_ImpliesNotification_NotTransactional()
    {
        foreach (MessageType type in Enum.GetValues<MessageType>())
        {
            if (type.IsLocallySchedulable())
            {
                type.IsNotification().Should().BeTrue($"{type} is locally schedulable");
                type.IsTransactional().Should().BeFalse($"{type} is locally schedulable");
            }
        }
    }
}
