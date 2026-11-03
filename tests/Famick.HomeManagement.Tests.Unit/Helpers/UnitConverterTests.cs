using System.Globalization;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Helpers;

public class UnitConverterTests
{
    // Expected amounts are strings so they reach the test as exact decimals.
    [Theory]
    [InlineData(2, StandardUnit.Tablespoon, StandardUnit.Cup, "0.125")]
    [InlineData(1, StandardUnit.Gallon, StandardUnit.Cup, "16")]
    [InlineData(1, StandardUnit.Kilogram, StandardUnit.Gram, "1000")]
    [InlineData(500, StandardUnit.Gram, StandardUnit.Kilogram, "0.5")]
    [InlineData(1, StandardUnit.Tablespoon, StandardUnit.Teaspoon, "3")]
    [InlineData(1, StandardUnit.Cup, StandardUnit.FluidOunce, "8")]
    [InlineData(1, StandardUnit.Quart, StandardUnit.Pint, "2")]
    [InlineData(1, StandardUnit.Gallon, StandardUnit.Quart, "4")]
    [InlineData(1, StandardUnit.Liter, StandardUnit.Milliliter, "1000")]
    [InlineData(1, StandardUnit.Pound, StandardUnit.Ounce, "16")]
    [InlineData(3, StandardUnit.Cup, StandardUnit.Cup, "3")]
    public void Converts_exactly_within_a_dimension(int amount, StandardUnit from, StandardUnit to, string expected)
    {
        UnitConverter.Convert(amount, from, to).Should().Be(decimal.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(1, StandardUnit.Cup, StandardUnit.Milliliter, "236.5882365")]
    [InlineData(1, StandardUnit.Pound, StandardUnit.Gram, "453.59237")]
    [InlineData(1, StandardUnit.Ounce, StandardUnit.Gram, "28.349523125")]
    public void Converts_between_US_and_metric(int amount, StandardUnit from, StandardUnit to, string expected)
    {
        UnitConverter.Convert(amount, from, to).Should().Be(decimal.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Converting_there_and_back_returns_the_amount()
    {
        var ml = UnitConverter.Convert(1, StandardUnit.Cup, StandardUnit.Milliliter)!.Value;

        UnitConverter.Convert(ml, StandardUnit.Milliliter, StandardUnit.Cup).Should().Be(1);
    }

    [Theory]
    [InlineData(StandardUnit.Cup, StandardUnit.Gram)]
    [InlineData(StandardUnit.Ounce, StandardUnit.FluidOunce)]
    [InlineData(StandardUnit.Kilogram, StandardUnit.Liter)]
    public void Refuses_to_convert_between_volume_and_weight(StandardUnit from, StandardUnit to)
    {
        UnitConverter.CanConvert(from, to).Should().BeFalse();
        UnitConverter.Convert(1, from, to).Should().BeNull();
    }

    [Fact]
    public void Refuses_to_convert_an_untagged_unit()
    {
        UnitConverter.Convert(1, null, StandardUnit.Cup).Should().BeNull();
        UnitConverter.Convert(1, StandardUnit.Cup, null).Should().BeNull();
        UnitConverter.Convert(1, null, null).Should().BeNull();
    }

    [Fact]
    public void Every_standard_unit_has_a_dimension()
    {
        foreach (var unit in Enum.GetValues<StandardUnit>())
            FluentActions.Invoking(() => UnitConverter.DimensionOf(unit)).Should().NotThrow(unit.ToString());
    }
}
