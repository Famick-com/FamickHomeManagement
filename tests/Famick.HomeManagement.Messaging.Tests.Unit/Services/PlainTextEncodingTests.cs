using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Famick.HomeManagement.Messaging.Tests.Unit.Services;

/// <summary>
/// One template set renders into destinations with opposite escaping rules. Only the HTML
/// body is parsed as HTML; the subject is a mail header, and the text alternative, SMS,
/// push and in-app copy are read literally.
/// </summary>
/// <remarks>
/// Escaping the literal destinations is not merely untidy. "Grandma's" arrives as
/// "Grandma&amp;#39;s", and a verification link picks up "&amp;amp;" between its query
/// parameters — a link that no longer resolves. Two templates were fixed by hand with
/// triple braces; the rest were not, which is why the renderer now decides per channel
/// instead of leaving it to each template to remember.
/// </remarks>
public class PlainTextEncodingTests
{
    private readonly StubbleTemplateRenderer _renderer = new(NullLogger<StubbleTemplateRenderer>.Instance);

    private static CalendarReminderData Reminder() => new()
    {
        EventTitle = "Dinner at Grandma's & Grandpa's",
        StartTime = "6:00 PM",
        StartDate = "Friday, September 11",
        TimeZoneLabel = "EDT"
    };

    private static EmailVerificationData Verification() => new()
    {
        HouseholdName = "O'Brien & Sons",
        VerificationLink = "https://app.famick.com/verify?token=abc123&uid=42",
        Token = "abc123"
    };

    [Theory]
    [InlineData(TransportChannel.EmailText)]
    [InlineData(TransportChannel.Sms)]
    [InlineData(TransportChannel.Push)]
    [InlineData(TransportChannel.InApp)]
    public async Task LiteralChannelsKeepTheCharactersTheyWereGiven(TransportChannel channel)
    {
        var rendered = await _renderer.RenderAsync(MessageType.CalendarReminder, channel, Reminder());

        rendered.Should().NotContain("&#39;");
        rendered.Should().NotContain("&amp;");
    }

    /// <summary>
    /// The absence of an encoded form is satisfied just as well by dropping the characters,
    /// so the channels that carry the event title assert on the title itself. Push and in-app
    /// bodies are excluded because they carry only the time — the title is the notification's
    /// own title, not part of the body.
    /// </summary>
    [Theory]
    [InlineData(TransportChannel.EmailText)]
    [InlineData(TransportChannel.Sms)]
    public async Task ChannelsCarryingTheTitleRenderItAsTyped(TransportChannel channel)
    {
        var rendered = await _renderer.RenderAsync(MessageType.CalendarReminder, channel, Reminder());

        rendered.Should().Contain("Dinner at Grandma's & Grandpa's");
    }

    [Fact]
    public async Task SubjectKeepsTheCharactersItWasGiven()
    {
        var subject = await _renderer.RenderSubjectAsync(MessageType.CalendarReminder, Reminder());

        subject.Should().Be("Reminder: Dinner at Grandma's & Grandpa's");
    }

    [Fact]
    public async Task ALinkSurvivesTheTextAlternativeIntact()
    {
        var body = await _renderer.RenderAsync(
            MessageType.EmailVerification, TransportChannel.EmailText, Verification());

        body.Should().Contain("https://app.famick.com/verify?token=abc123&uid=42");
        body.Should().NotContain("&amp;uid");
    }

    [Fact]
    public async Task TheHtmlBodyStillEscapes()
    {
        var body = await _renderer.RenderAsync(
            MessageType.CalendarReminder, TransportChannel.EmailHtml, Reminder());

        body.Should().Contain("Grandma&#39;s &amp; Grandpa&#39;s");
    }

    [Fact]
    public async Task TheHtmlBodyDoesNotLetContentBecomeMarkup()
    {
        var data = Reminder();
        data.EventTitle = "<script>alert(1)</script>";

        var body = await _renderer.RenderAsync(
            MessageType.CalendarReminder, TransportChannel.EmailHtml, data);

        body.Should().NotContain("<script>");
        body.Should().Contain("&lt;script&gt;");
    }
}
