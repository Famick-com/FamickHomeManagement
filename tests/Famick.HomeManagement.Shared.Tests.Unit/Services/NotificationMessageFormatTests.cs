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

    private static string Build(string subject = "Famick: 148 item(s) need attention", string unsubscribeUrl = Unsubscribe) =>
        AwsSesEmailService.BuildRawNotificationMessage(
            "Famick Home Management <noreply@famick.com>",
            "someone@example.com",
            subject,
            "<p>body</p>",
            "body",
            unsubscribeUrl);

    /// <summary>The header block is everything before the first empty line.</summary>
    private static string[] HeaderLines(string message)
    {
        var lines = message.Replace("\r\n", "\n").Split('\n');
        var end = Array.IndexOf(lines, "");
        end.Should().BeGreaterThan(0, "the header block must be terminated by an empty line");
        return lines[..end];
    }

    [Fact]
    public void ContentTypeIsAHeaderOfItsOwn()
    {
        var headers = HeaderLines(Build());

        headers.Should().Contain(@"Content-Type: multipart/alternative; boundary=""boundary123""");
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

        headers.Should().Contain(@"Content-Type: multipart/alternative; boundary=""boundary123""");
        headers.Should().NotContain(h => h.StartsWith("List-Unsubscribe"));
    }

    [Fact]
    public void BothPartsDeclareTheirTransferEncoding()
    {
        var message = Build();

        message.Should().Contain("Content-Type: text/plain; charset=UTF-8\nContent-Transfer-Encoding: 8bit");
        message.Should().Contain("Content-Type: text/html; charset=UTF-8\nContent-Transfer-Encoding: 8bit");
    }

    [Fact]
    public void TheMessageIsClosedByATerminatingBoundary()
    {
        Build().Should().EndWith("--boundary123--");
    }

    [Fact]
    public void AnAsciiSubjectIsLeftAlone()
    {
        HeaderLines(Build()).Should().Contain("Subject: Famick: 148 item(s) need attention");
    }

    [Fact]
    public void ASubjectWithAnAccentIsEncodedRatherThanSentRaw()
    {
        var headers = HeaderLines(Build(subject: "Reminder: Café"));

        headers.Should().Contain("Subject: =?UTF-8?B?UmVtaW5kZXI6IENhZsOp?=");
    }
}
