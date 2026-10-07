namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// The kind of a piece of equipment. This is the single taxonomy for equipment — it replaced
/// the tenant-owned <c>EquipmentCategory</c> entity, which modelled the same idea but could be
/// renamed or deleted per household and therefore could not drive behaviour.
/// </summary>
/// <remarks>
/// <para>These values are deliberately the same groups the icon picker already uses
/// (<c>EquipmentIconPickerDialog</c>), with its combined "Tools &amp; Outdoor" group split in two.
/// The 1:1 correspondence is what lets a kind select the right icon group, so keep them aligned
/// if either side gains a member.</para>
/// <para>A kind drives three things: which icon group is offered, the default
/// <see cref="Entities.Equipment.UsageUnit"/>, and which <see cref="Entities.EquipmentAttributes"/> the
/// editor renders. Only <see cref="Vehicle"/> has an attribute set today; the others can gain one
/// without a migration, because attributes are stored as JSON.</para>
/// </remarks>
public enum EquipmentKind
{
    /// <summary>Anything that does not fit another kind. The fallback, and the default.</summary>
    Other = 0,

    /// <summary>Kitchen and laundry appliances, HVAC, water heaters.</summary>
    Appliance = 1,

    /// <summary>Televisions, computers, networking gear, cameras.</summary>
    Electronics = 2,

    /// <summary>Cars, motorcycles, boats, tractors. Carries the vehicle attribute set.</summary>
    Vehicle = 3,

    /// <summary>Lawn, garden, pool and grill equipment.</summary>
    Outdoor = 4,

    /// <summary>Hand and power tools. Displayed as "Tools".</summary>
    Tool = 5,

    /// <summary>Furniture and fixtures. Displayed as "Home &amp; Furniture".</summary>
    Furniture = 6,
}
