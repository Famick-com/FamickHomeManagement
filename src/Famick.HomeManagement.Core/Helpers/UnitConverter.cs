using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Helpers;

/// <summary>What a standard unit measures. Amounts only convert between units of the same dimension.</summary>
public enum UnitDimension
{
    Volume,
    Weight
}

/// <summary>
/// Converts amounts between standard units. These are physical constants, the same for every household,
/// so they live here rather than in a table; a household's own units convert through the
/// <see cref="StandardUnit"/> they are tagged with.
/// </summary>
/// <remarks>
/// Only within a dimension: volume to weight depends on the ingredient, and is the product's own
/// purchase-to-stock factor. Anything that can't be converted gives null rather than a guess.
/// </remarks>
public static class UnitConverter
{
    // The size of each unit in its dimension's base: milliliters for volume, grams for weight. The US
    // customary volumes are exact multiples of one another (a cup is 48 teaspoons), so conversions
    // among them come out exact.
    private static readonly Dictionary<StandardUnit, (UnitDimension Dimension, decimal Size)> Units = new()
    {
        [StandardUnit.Teaspoon] = (UnitDimension.Volume, 4.92892159375m),
        [StandardUnit.Tablespoon] = (UnitDimension.Volume, 14.78676478125m),
        [StandardUnit.FluidOunce] = (UnitDimension.Volume, 29.5735295625m),
        [StandardUnit.Cup] = (UnitDimension.Volume, 236.5882365m),
        [StandardUnit.Pint] = (UnitDimension.Volume, 473.176473m),
        [StandardUnit.Quart] = (UnitDimension.Volume, 946.352946m),
        [StandardUnit.Gallon] = (UnitDimension.Volume, 3785.411784m),
        [StandardUnit.Milliliter] = (UnitDimension.Volume, 1m),
        [StandardUnit.Liter] = (UnitDimension.Volume, 1000m),

        [StandardUnit.Gram] = (UnitDimension.Weight, 1m),
        [StandardUnit.Kilogram] = (UnitDimension.Weight, 1000m),
        [StandardUnit.Ounce] = (UnitDimension.Weight, 28.349523125m),
        [StandardUnit.Pound] = (UnitDimension.Weight, 453.59237m),
    };

    public static UnitDimension DimensionOf(StandardUnit unit) => Units[unit].Dimension;

    /// <summary>Whether an amount in one unit can be expressed in the other.</summary>
    public static bool CanConvert(StandardUnit? from, StandardUnit? to) =>
        from is { } f && to is { } t && Units[f].Dimension == Units[t].Dimension;

    /// <summary>
    /// The amount, in <paramref name="from"/>, expressed in <paramref name="to"/>; null when either unit
    /// is missing (an untagged household unit) or they measure different things.
    /// </summary>
    public static decimal? Convert(decimal amount, StandardUnit? from, StandardUnit? to)
    {
        if (!CanConvert(from, to)) return null;
        if (from == to) return amount;
        return amount * Units[from!.Value].Size / Units[to!.Value].Size;
    }
}
