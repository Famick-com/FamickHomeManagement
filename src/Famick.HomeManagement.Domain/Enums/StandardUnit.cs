using System.Text.Json.Serialization;

namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// A measure with a fixed size, which a household's quantity unit can be tagged with so that amounts
/// in it convert to other standard measures of the same kind (2 tbsp is ⅛ cup, 500 g is 0.5 kg).
/// </summary>
/// <remarks>
/// Cup, fluid ounce, pint, quart and gallon are US customary measures. Stored and sent by name, so the
/// order here can change freely.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StandardUnit
{
    // Volume
    Teaspoon,
    Tablespoon,
    FluidOunce,
    Cup,
    Pint,
    Quart,
    Gallon,
    Milliliter,
    Liter,

    // Weight
    Gram,
    Kilogram,
    Ounce,
    Pound
}
