using Famick.HomeManagement.Core.Helpers;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Helpers;

public class QuantityTextTests
{
    [Theory]
    [InlineData("30", 30)]
    [InlineData("1.5", 1.5)]
    [InlineData("1 1/2", 1.5)]
    [InlineData("1-1/2", 1.5)]
    [InlineData("1½", 1.5)]
    [InlineData("1 ½", 1.5)]
    [InlineData("1/2", 0.5)]
    [InlineData("1⁄2", 0.5)]
    [InlineData("2 3⁄4", 2.75)]
    [InlineData("½", 0.5)]
    [InlineData("⅛", 0.125)]
    [InlineData("⅜", 0.375)]
    [InlineData("1⅝", 1.625)]
    [InlineData("⅞", 0.875)]
    [InlineData("⅕", 0.2)]
    [InlineData("1 1/0", 1)]
    public void Parses_the_forms_recipes_write(string text, double expected)
    {
        QuantityText.Parse(text).Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData("⅙", 1 / 6d)]
    [InlineData("⅚", 5 / 6d)]
    [InlineData("⅓", 1 / 3d)]
    public void Parses_thirds_and_sixths(string text, double expected)
    {
        QuantityText.Parse(text).Should().BeApproximately(expected, 1e-9);
    }
}
