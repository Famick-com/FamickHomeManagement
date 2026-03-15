using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for calendar ICS export generation logic.
/// Recreates the ICS generation to avoid MAUI project dependency.
/// </summary>
public class CalendarIcsExportTests
{
    private class TestEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Location { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public bool IsAllDay { get; set; }
    }

    private static string GenerateIcsContent(IEnumerable<TestEvent> events)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Famick//HomeManagement//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");

        foreach (var evt in events)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{evt.EventId}@famick.com");

            if (evt.IsAllDay)
            {
                sb.AppendLine($"DTSTART;VALUE=DATE:{evt.StartTimeUtc:yyyyMMdd}");
                sb.AppendLine($"DTEND;VALUE=DATE:{evt.EndTimeUtc:yyyyMMdd}");
            }
            else
            {
                sb.AppendLine($"DTSTART:{evt.StartTimeUtc:yyyyMMdd'T'HHmmss'Z'}");
                sb.AppendLine($"DTEND:{evt.EndTimeUtc:yyyyMMdd'T'HHmmss'Z'}");
            }

            sb.AppendLine($"SUMMARY:{EscapeIcsText(evt.Title)}");

            if (!string.IsNullOrWhiteSpace(evt.Description))
                sb.AppendLine($"DESCRIPTION:{EscapeIcsText(evt.Description)}");

            if (!string.IsNullOrWhiteSpace(evt.Location))
                sb.AppendLine($"LOCATION:{EscapeIcsText(evt.Location)}");

            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string EscapeIcsText(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }

    [Fact]
    public void GenerateIcs_ContainsVCalendarHeader()
    {
        var ics = GenerateIcsContent(Array.Empty<TestEvent>());

        ics.Should().Contain("BEGIN:VCALENDAR");
        ics.Should().Contain("VERSION:2.0");
        ics.Should().Contain("END:VCALENDAR");
    }

    [Fact]
    public void GenerateIcs_SingleEvent_ContainsVEvent()
    {
        var eventId = Guid.NewGuid();
        var events = new[]
        {
            new TestEvent
            {
                EventId = eventId,
                Title = "Team Meeting",
                StartTimeUtc = new DateTime(2026, 3, 15, 14, 0, 0),
                EndTimeUtc = new DateTime(2026, 3, 15, 15, 0, 0)
            }
        };

        var ics = GenerateIcsContent(events);

        ics.Should().Contain("BEGIN:VEVENT");
        ics.Should().Contain("END:VEVENT");
        ics.Should().Contain($"UID:{eventId}@famick.com");
        ics.Should().Contain("SUMMARY:Team Meeting");
        ics.Should().Contain("DTSTART:20260315T140000Z");
        ics.Should().Contain("DTEND:20260315T150000Z");
    }

    [Fact]
    public void GenerateIcs_AllDayEvent_UsesDateFormat()
    {
        var events = new[]
        {
            new TestEvent
            {
                Title = "Birthday",
                StartTimeUtc = new DateTime(2026, 3, 15),
                EndTimeUtc = new DateTime(2026, 3, 16),
                IsAllDay = true
            }
        };

        var ics = GenerateIcsContent(events);

        ics.Should().Contain("DTSTART;VALUE=DATE:20260315");
        ics.Should().Contain("DTEND;VALUE=DATE:20260316");
        ics.Should().NotContain("DTSTART:2026");
    }

    [Fact]
    public void GenerateIcs_WithDescription_IncludesDescription()
    {
        var events = new[]
        {
            new TestEvent
            {
                Title = "Meeting",
                Description = "Discuss project status",
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow.AddHours(1)
            }
        };

        var ics = GenerateIcsContent(events);

        ics.Should().Contain("DESCRIPTION:Discuss project status");
    }

    [Fact]
    public void GenerateIcs_WithLocation_IncludesLocation()
    {
        var events = new[]
        {
            new TestEvent
            {
                Title = "Dinner",
                Location = "123 Main St",
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow.AddHours(2)
            }
        };

        var ics = GenerateIcsContent(events);

        ics.Should().Contain("LOCATION:123 Main St");
    }

    [Fact]
    public void GenerateIcs_WithoutDescription_OmitsDescription()
    {
        var events = new[]
        {
            new TestEvent
            {
                Title = "Quick Call",
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow.AddMinutes(30)
            }
        };

        var ics = GenerateIcsContent(events);

        ics.Should().NotContain("DESCRIPTION:");
    }

    [Fact]
    public void EscapeIcsText_EscapesSpecialCharacters()
    {
        var escaped = EscapeIcsText("Hello; World, Test\nNew line");

        escaped.Should().Be("Hello\\; World\\, Test\\nNew line");
    }

    [Fact]
    public void EscapeIcsText_EscapesBackslash()
    {
        var escaped = EscapeIcsText("path\\to\\file");

        escaped.Should().Be("path\\\\to\\\\file");
    }

    [Fact]
    public void GenerateIcs_MultipleEvents_ContainsAll()
    {
        var events = new[]
        {
            new TestEvent { Title = "Event A", StartTimeUtc = DateTime.UtcNow, EndTimeUtc = DateTime.UtcNow.AddHours(1) },
            new TestEvent { Title = "Event B", StartTimeUtc = DateTime.UtcNow, EndTimeUtc = DateTime.UtcNow.AddHours(1) },
            new TestEvent { Title = "Event C", StartTimeUtc = DateTime.UtcNow, EndTimeUtc = DateTime.UtcNow.AddHours(1) }
        };

        var ics = GenerateIcsContent(events);

        ics.Should().Contain("SUMMARY:Event A");
        ics.Should().Contain("SUMMARY:Event B");
        ics.Should().Contain("SUMMARY:Event C");

        // Should have exactly 3 VEVENT blocks
        var veventCount = ics.Split("BEGIN:VEVENT").Length - 1;
        veventCount.Should().Be(3);
    }
}
