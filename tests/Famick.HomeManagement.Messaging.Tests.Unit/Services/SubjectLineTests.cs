using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Famick.HomeManagement.Messaging.Tests.Unit.Services;

/// <summary>
/// A subject is a mail header, and <c>System.Net.Mail</c> throws on one containing CR or
/// LF rather than cleaning it up — "the specified string is not in the form required for a
/// subject", which names neither the header nor the character at fault.
/// </summary>
/// <remarks>
/// The usual cause is invisible in a diff and added automatically by most editors: a
/// template file saved with a trailing newline. Four templates shipped that way and every
/// email they should have sent failed, with the send reported as successful right up until
/// the transport tried to build the message.
/// </remarks>
public class SubjectLineTests
{
    private readonly StubbleTemplateRenderer _renderer = new(NullLogger<StubbleTemplateRenderer>.Instance);

    public static TheoryData<MessageType, IMessageData> DeletionSubjects() => new()
    {
        { MessageType.AccountDeletionScheduled, Household() },
        { MessageType.AccountDeletionCancelled, Household() },
        { MessageType.AccountDeletionReminder, Household() },
        { MessageType.AccountDeleted, Household() },
        { MessageType.AccountDeletionScheduled, Individual() },
        { MessageType.AccountDeletionCancelled, Individual() },
        { MessageType.AccountDeletionReminder, Individual() },
        { MessageType.AccountDeleted, Individual() }
    };

    [Theory]
    [MemberData(nameof(DeletionSubjects))]
    public async Task SubjectIsASingleLineAndUsable(MessageType type, IMessageData data)
    {
        var subject = await _renderer.RenderSubjectAsync(type, data);

        subject.Should().NotBeNullOrWhiteSpace();
        subject.Should().NotContain("\n").And.NotContain("\r",
            "System.Net.Mail refuses a subject containing a line break, so the email never sends");

        // The exact check the mail library performs, so a passing test means a sendable
        // message rather than an approximation of one.
        var act = () => new System.Net.Mail.MailMessage { Subject = subject };
        act.Should().NotThrow();
    }

    private static AccountDeletionData Household() => new()
    {
        UserName = "Mike",
        IsHousehold = true,
        HouseholdName = "The Therien Family",
        RequestedOn = "30 August 2026",
        DeletedOn = "29 September 2026",
        DaysRemaining = 3
    };

    private static AccountDeletionData Individual() => new()
    {
        UserName = "Mike",
        IsHousehold = false,
        RequestedOn = "30 August 2026",
        DeletedOn = "29 September 2026",
        DaysRemaining = 3
    };
}
