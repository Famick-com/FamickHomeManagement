using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Guards the normalization applied to client-supplied calendar dates.
/// A Local DateTime reaching Npgsql throws on save; a naive ToUniversalTime()
/// fixes that but silently shifts the date for clients east of UTC.
/// </summary>
public class DateNormalizationTests
{
    [Fact]
    public void ToUtcCalendarDate_WithNull_ReturnsNull()
    {
        DateNormalization.ToUtcCalendarDate(null).Should().BeNull();
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    public void ToUtcCalendarDate_AlwaysReturnsUtc(DateTimeKind kind)
    {
        // Npgsql rejects anything that is not Utc for "timestamp with time zone".
        var input = DateTime.SpecifyKind(new DateTime(2026, 9, 1, 0, 0, 0), kind);

        var result = DateNormalization.ToUtcCalendarDate(input);

        result!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ToUtcCalendarDate_PreservesTheCalendarDayThatWasPicked()
    {
        // The regression this exists for: ToUniversalTime() on local midnight east of UTC
        // lands on the previous day, moving a best-before date a day earlier than chosen.
        var picked = DateTime.SpecifyKind(new DateTime(2026, 9, 1), DateTimeKind.Local);

        var result = DateNormalization.ToUtcCalendarDate(picked);

        result!.Value.Year.Should().Be(2026);
        result.Value.Month.Should().Be(9);
        result.Value.Day.Should().Be(1);
    }

    [Fact]
    public void ToUtcCalendarDate_DropsTheTimeComponent()
    {
        var withTime = DateTime.SpecifyKind(new DateTime(2026, 9, 1, 17, 43, 12), DateTimeKind.Local);

        var result = DateNormalization.ToUtcCalendarDate(withTime);

        result!.Value.TimeOfDay.Should().Be(TimeSpan.Zero);
    }
}
