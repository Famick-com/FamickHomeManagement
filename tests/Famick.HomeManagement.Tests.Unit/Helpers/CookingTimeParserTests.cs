using Famick.HomeManagement.Core.Helpers;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Helpers;

public class CookingTimeParserTests
{
    private static CookingTime Single(string text)
    {
        var times = CookingTimeParser.FindAll(text);
        times.Should().ContainSingle($"\"{text}\" has one cooking time");
        return times[0];
    }

    [Theory]
    [InlineData("Bake for 30 minutes", "30 minutes", 30 * 60)]
    [InlineData("Bake for 30 min", "30 min", 30 * 60)]
    [InlineData("Bake for 30 mins", "30 mins", 30 * 60)]
    [InlineData("Bake for 30m", "30m", 30 * 60)]
    [InlineData("Rest 1 minute", "1 minute", 60)]
    [InlineData("Roast 1 hour", "1 hour", 3600)]
    [InlineData("Roast 1 hr", "1 hr", 3600)]
    [InlineData("Braise 2 hrs", "2 hrs", 2 * 3600)]
    [InlineData("Proof 2h", "2h", 2 * 3600)]
    [InlineData("Whisk 90 seconds", "90 seconds", 90)]
    [InlineData("Whisk 30 sec", "30 sec", 30)]
    [InlineData("Whisk 45 secs", "45 secs", 45)]
    [InlineData("BAKE 30 MINUTES", "30 MINUTES", 30 * 60)]
    [InlineData("Give it a 30-minute rest", "30-minute", 30 * 60)]
    public void Finds_a_single_time(string text, string phrase, int seconds)
    {
        var time = Single(text);

        text.Substring(time.Index, time.Length).Should().Be(phrase);
        time.Duration.Should().Be(TimeSpan.FromSeconds(seconds));
        time.MinimumDuration.Should().BeNull();
    }

    [Theory]
    [InlineData("Roast 1 hour 15 minutes", "1 hour 15 minutes", 75 * 60)]
    [InlineData("Roast 1 hr 15 min", "1 hr 15 min", 75 * 60)]
    [InlineData("Roast 1 hour and 15 minutes", "1 hour and 15 minutes", 75 * 60)]
    [InlineData("Roast 1h15m", "1h15m", 75 * 60)]
    [InlineData("Stir 2 minutes 30 seconds", "2 minutes 30 seconds", 150)]
    public void Reads_a_compound_time_as_one(string text, string phrase, int seconds)
    {
        var time = Single(text);

        text.Substring(time.Index, time.Length).Should().Be(phrase);
        time.Duration.Should().Be(TimeSpan.FromSeconds(seconds));
    }

    [Theory]
    [InlineData("Simmer 1½ hours", "1½ hours", 90)]
    [InlineData("Simmer 1 1/2 hours", "1 1/2 hours", 90)]
    [InlineData("Simmer 1-1/2 hours", "1-1/2 hours", 90)]
    [InlineData("Simmer 1.5 hours", "1.5 hours", 90)]
    [InlineData("Simmer 1/2 hour", "1/2 hour", 30)]
    [InlineData("Simmer ½ hour", "½ hour", 30)]
    [InlineData("Simmer 2¼ hours", "2¼ hours", 135)]
    [InlineData("Simmer half an hour", "half an hour", 30)]
    [InlineData("Simmer an hour", "an hour", 60)]
    [InlineData("Simmer a minute", "a minute", 1)]
    public void Reads_fractions_and_words(string text, string phrase, int minutes)
    {
        var time = Single(text);

        text.Substring(time.Index, time.Length).Should().Be(phrase);
        time.Duration.Should().Be(TimeSpan.FromMinutes(minutes));
    }

    [Theory]
    [InlineData("Bake 20-25 minutes", "20-25 minutes", 20, 25)]
    [InlineData("Bake 20 - 25 minutes", "20 - 25 minutes", 20, 25)]
    [InlineData("Bake 20–25 minutes", "20–25 minutes", 20, 25)]
    [InlineData("Bake 20 to 25 minutes", "20 to 25 minutes", 20, 25)]
    [InlineData("Braise 1-2 hours", "1-2 hours", 60, 120)]
    [InlineData("Braise 45 minutes to 1 hour", "45 minutes to 1 hour", 45, 60)]
    public void Runs_a_range_to_its_upper_bound_and_reports_the_lower(string text, string phrase, int low, int high)
    {
        var time = Single(text);

        text.Substring(time.Index, time.Length).Should().Be(phrase);
        time.Duration.Should().Be(TimeSpan.FromMinutes(high));
        time.MinimumDuration.Should().Be(TimeSpan.FromMinutes(low));
    }

    [Fact]
    public void A_to_that_does_not_lead_to_a_time_is_not_a_range()
    {
        const string text = "Cook 5 minutes to soften";

        var time = Single(text);

        text.Substring(time.Index, time.Length).Should().Be("5 minutes");
        time.MinimumDuration.Should().BeNull();
    }

    [Fact]
    public void A_temperature_before_a_range_is_not_its_low_end()
    {
        const string text = "Preheat to 425. Bake 10 to 12 minutes.";

        var time = Single(text);

        text.Substring(time.Index, time.Length).Should().Be("10 to 12 minutes");
        time.MinimumDuration.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Finds_every_time_in_a_step_in_order()
    {
        const string text = "Simmer 10 minutes, then bake for 25 min at 350°F.";

        var times = CookingTimeParser.FindAll(text);

        times.Select(t => text.Substring(t.Index, t.Length)).Should().Equal("10 minutes", "25 min");
        times.Select(t => t.Duration).Should().Equal(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(25));
    }

    [Theory]
    [InlineData("Preheat the oven to 350 degrees")]
    [InlineData("Add 2 cups of flour")]
    [InlineData("Repeat step 3")]
    [InlineData("Bake for 30")]
    [InlineData("Chill overnight")]
    [InlineData("Bake until golden")]
    [InlineData("Add 15 mint leaves")]
    [InlineData("Add a second egg")]
    [InlineData("A 1990s classic")]
    [InlineData("Roll to 2 m long")]
    [InlineData("Cook for 0 minutes")]
    [InlineData("Marinate 48 hours")]
    [InlineData("")]
    [InlineData(null)]
    public void Finds_nothing_that_is_not_a_cooking_time(string? text)
    {
        CookingTimeParser.FindAll(text).Should().BeEmpty();
    }

    [Fact]
    public void Digits_other_than_0_to_9_are_not_read_as_a_time()
    {
        CookingTimeParser.FindAll("Bake ١٥ minutes").Should().BeEmpty();
    }

    [Fact]
    public void An_absurd_time_is_skipped_without_losing_the_times_after_it()
    {
        const string text = "Bake 1000000000000 hours, then rest 5 minutes";

        var time = Single(text);

        text.Substring(time.Index, time.Length).Should().Be("5 minutes");
    }

    [Fact]
    public void Allows_a_full_day()
    {
        Single("Cure for 24 hours").Duration.Should().Be(TimeSpan.FromHours(24));
    }
}
