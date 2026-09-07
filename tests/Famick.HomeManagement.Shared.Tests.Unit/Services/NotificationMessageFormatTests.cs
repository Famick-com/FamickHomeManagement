using System.Text.RegularExpressions;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// The notification email is assembled by hand, because the simple send cannot carry the
/// List-Unsubscribe headers. Hand-assembly means the message can be malformed in ways the
/// SDK would otherwise have prevented.
/// </summary>
/// <remarks>
/// It was. An optional two-line unsubscribe block was interpolated directly before
/// <c>Content-Type</c> and carried no trailing newline, so the two ran together into one
/// nonsense header and the message was left with no Content-Type. A message with no
/// Content-Type is plain text, so recipients saw the boundaries, the part headers and the
/// raw HTML rather than the message — and only notification emails were affected, which is
/// why the transactional ones looked fine throughout.
/// </remarks>
public class NotificationMessageFormatTests
{
    private const string Unsubscribe = "https://app.famick.com/api/v1/notifications/unsubscribe?token=abc";

    private static string Build(
        string subject = "Famick: 148 item(s) need attention",
        string unsubscribeUrl = Unsubscribe,
        string textBody = "body",
        string htmlBody = "<p>body</p>",
        string toEmail = "someone@example.com") =>
        AwsSesEmailService.BuildRawNotificationMessage(
            "Famick Home Management <noreply@famick.com>",
            toEmail,
            subject,
            htmlBody,
            textBody,
            unsubscribeUrl);

    /// <summary>The header block is everything before the first empty line.</summary>
    private static string[] HeaderLines(string message)
    {
        var lines = message.Split("\r\n");
        var end = Array.IndexOf(lines, "");
        end.Should().BeGreaterThan(0, "the header block must be terminated by an empty line");
        return lines[..end];
    }

    /// <summary>The boundary the message actually declared, read back from its own header.</summary>
    private static string BoundaryOf(string message)
    {
        var match = Regex.Match(message, @"Content-Type: multipart/alternative; boundary=""([^""]+)""");
        match.Success.Should().BeTrue("the message must declare a multipart boundary");
        return match.Groups[1].Value;
    }

    [Fact]
    public void ContentTypeIsAHeaderOfItsOwn()
    {
        var message = Build();

        HeaderLines(message).Should().ContainSingle(h => h.StartsWith("Content-Type: multipart/alternative;"));
    }

    [Fact]
    public void TheUnsubscribeHeaderDoesNotSwallowTheOneAfterIt()
    {
        var headers = HeaderLines(Build());

        headers.Should().Contain("List-Unsubscribe-Post: List-Unsubscribe=One-Click");
        headers.Should().NotContain(h => h.StartsWith("List-Unsubscribe-Post:") && h.Contains("Content-Type"));
    }

    [Fact]
    public void EveryHeaderLineLooksLikeAHeader()
    {
        foreach (var line in HeaderLines(Build()))
            line.Should().MatchRegex("^[A-Za-z-]+: ", "'{0}' is in the header block", line);
    }

    [Fact]
    public void WithoutAnUnsubscribeUrlTheBlockIsSimplyAbsent()
    {
        var headers = HeaderLines(Build(unsubscribeUrl: ""));

        headers.Should().Contain(h => h.StartsWith("Content-Type: multipart/alternative;"));
        headers.Should().NotContain(h => h.StartsWith("List-Unsubscribe"));
    }

    [Fact]
    public void BothPartsDeclareTheirTransferEncoding()
    {
        var message = Build();

        message.Should().Contain("Content-Type: text/plain; charset=UTF-8\r\nContent-Transfer-Encoding: 8bit");
        message.Should().Contain("Content-Type: text/html; charset=UTF-8\r\nContent-Transfer-Encoding: 8bit");
    }

    [Fact]
    public void TheMessageIsClosedByItsOwnTerminatingBoundary()
    {
        var message = Build();

        message.Should().EndWith($"--{BoundaryOf(message)}--");
    }

    [Fact]
    public void AnAsciiSubjectIsLeftAlone()
    {
        HeaderLines(Build()).Should().Contain("Subject: Famick: 148 item(s) need attention");
    }

    [Fact]
    public void ASubjectWithAnAccentIsEncodedRatherThanSentRaw()
    {
        HeaderLines(Build(subject: "Reminder: Café"))
            .Should().Contain("Subject: =?UTF-8?B?UmVtaW5kZXI6IENhZsOp?=");
    }

    // --- The message must survive content that looks like message structure ---

    [Fact]
    public void EveryMessageGetsItsOwnBoundary()
    {
        BoundaryOf(Build()).Should().NotBe(BoundaryOf(Build()));
    }

    [Fact]
    public void ABodyCannotContainTheBoundaryThatDelimitsIt()
    {
        var message = Build(textBody: "--=_Famick_deadbeef\r\nContent-Type: text/html\r\n\r\ninjected");

        var boundary = BoundaryOf(message);
        message.Should().EndWith($"--{boundary}--");
        // Three delimiter lines and no more: two openers and the terminator.
        Regex.Matches(message, Regex.Escape($"--{boundary}")).Count.Should().Be(3);
    }

    [Theory]
    [InlineData("Reminder\r\nBcc: attacker@example.com")]
    [InlineData("Reminder\nBcc: attacker@example.com")]
    [InlineData("Reminder\rBcc: attacker@example.com")]
    public void ASubjectCannotAddAHeaderOfItsOwn(string subject)
    {
        var headers = HeaderLines(Build(subject: subject));

        headers.Should().NotContain(h => h.StartsWith("Bcc:"));
        headers.Should().ContainSingle(h => h.StartsWith("Subject: "));
    }

    [Fact]
    public void ARecipientCannotAddAHeaderOfItsOwn()
    {
        var headers = HeaderLines(Build(toEmail: "someone@example.com\r\nBcc: attacker@example.com"));

        headers.Should().NotContain(h => h.StartsWith("Bcc:"));
    }

    // --- RFC 5322 line endings ---

    [Fact]
    public void EveryLineEndsWithCrLf()
    {
        var message = Build(textBody: "one\ntwo", htmlBody: "<p>one</p>\n<p>two</p>");

        Regex.Matches(message, "(?<!\r)\n").Should().BeEmpty("a bare LF is not a line ending in RFC 5322");
    }
}
