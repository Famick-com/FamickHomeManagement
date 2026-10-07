using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Equipment;

/// <summary>
/// The per-kind defaults that make <see cref="EquipmentKind"/> more than a label: a starting icon,
/// a starting usage unit, and the icon-picker group a kind belongs to.
/// </summary>
/// <remarks>
/// <para>This lives in Core rather than in the Blazor UI so the web client, the mobile app and the
/// server all answer these questions the same way. Mobile keeps its own DTOs, but it should not
/// keep its own idea of what an Appliance defaults to.</para>
/// <para><b>These are suggestions, not rules.</b> A caller applies them when creating something or
/// when the user changes kind, and whatever the user then sets wins — including clearing the usage
/// unit. Treating them as rules would mean a user could never have an appliance they do not want to
/// track hours on.</para>
/// </remarks>
public static class EquipmentKindDefaults
{
    /// <summary>
    /// The kinds in the order they should be offered in a picker. "Other" is last: it is the
    /// fallback, so it should not be the first thing a user reads.
    /// </summary>
    public static readonly IReadOnlyList<EquipmentKind> DisplayOrder =
    [
        EquipmentKind.Appliance,
        EquipmentKind.Electronics,
        EquipmentKind.Vehicle,
        EquipmentKind.Outdoor,
        EquipmentKind.Tool,
        EquipmentKind.Furniture,
        EquipmentKind.Other,
    ];

    /// <summary>
    /// The Material icon name to start a new item of this kind with.
    /// </summary>
    public static string DefaultIcon(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Appliance => "Kitchen",
        EquipmentKind.Electronics => "Tv",
        EquipmentKind.Vehicle => "DirectionsCar",
        EquipmentKind.Outdoor => "Grass",
        EquipmentKind.Tool => "Construction",
        EquipmentKind.Furniture => "Chair",
        _ => "Settings",
    };

    /// <summary>
    /// The usage unit to start a new item of this kind with, or null for kinds whose usage is not
    /// normally metered.
    /// </summary>
    /// <remarks>
    /// This matters more than it looks: the usage tab only appears when a usage unit is set, so
    /// before kinds existed a user got usage tracking only by guessing that typing "hours" into a
    /// free-text box would unlock it.
    /// </remarks>
    public static string? DefaultUsageUnit(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Vehicle => "miles",
        EquipmentKind.Appliance => "hours",
        EquipmentKind.Outdoor => "hours",
        EquipmentKind.Tool => "hours",
        _ => null,
    };

    /// <summary>
    /// The icon-picker group that matches this kind, so the picker can open on the right one.
    /// Must stay in step with the group names in <c>EquipmentIconPickerDialog</c>.
    /// </summary>
    public static string? IconGroup(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Appliance => "Appliances",
        EquipmentKind.Electronics => "Electronics",
        EquipmentKind.Vehicle => "Vehicles",
        EquipmentKind.Outdoor => "Outdoor",
        EquipmentKind.Tool => "Tools",
        EquipmentKind.Furniture => "Home & Furniture",
        // Other deliberately maps to no group, so the picker shows everything.
        _ => null,
    };

    /// <summary>
    /// The localization key for a kind's display name. Note two names differ deliberately from the
    /// enum member: <see cref="EquipmentKind.Tool"/> reads "Tools" and
    /// <see cref="EquipmentKind.Furniture"/> reads "Home &amp; Furniture".
    /// </summary>
    public static string LocalizationKey(EquipmentKind kind) => $"equipment.kind.{kind switch
    {
        EquipmentKind.Appliance => "appliance",
        EquipmentKind.Electronics => "electronics",
        EquipmentKind.Vehicle => "vehicle",
        EquipmentKind.Outdoor => "outdoor",
        EquipmentKind.Tool => "tool",
        EquipmentKind.Furniture => "furniture",
        _ => "other",
    }}";

    /// <summary>
    /// Whether this kind has any fields in <see cref="EquipmentAttributes"/> for the editor to
    /// render. Only vehicles do today.
    /// </summary>
    public static bool HasAttributes(EquipmentKind kind) => kind == EquipmentKind.Vehicle;
}
