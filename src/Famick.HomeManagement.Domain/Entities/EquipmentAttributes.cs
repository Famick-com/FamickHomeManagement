namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Kind-specific fields for a piece of equipment, stored as a single <c>jsonb</c> column rather
/// than as columns of their own.
/// </summary>
/// <remarks>
/// <para>Only <see cref="Enums.EquipmentKind.Vehicle"/> uses these today. The point of the JSON column
/// is that a new kind can gain its own fields by adding properties here, with no migration and no
/// widening of the <c>equipment</c> table — which is what makes adding kinds cheap.</para>
/// <para><b>Two vehicle fields deliberately did not move in here</b> when vehicles were folded
/// into equipment: the primary driver stayed a real <c>uuid</c> column, because it is a foreign key
/// to a contact and must still be nulled when that contact is deleted; and the active flag stayed a
/// real column, because list queries filter on it and a JSON predicate there cannot be indexed.
/// Everything in this class is descriptive — nothing here is a foreign key, and nothing here is
/// filtered on in a list query. Keep it that way.</para>
/// <para>Uniqueness of <see cref="Vin"/> is enforced by a partial unique index on the expression
/// <c>(attributes -&gt;&gt; 'Vin')</c>, not by EF — see the equipment configuration.</para>
/// </remarks>
public class EquipmentAttributes
{
    /// <summary>Model year, for a vehicle.</summary>
    public int? Year { get; set; }

    /// <summary>Trim level, e.g. "SE", "Limited", "XLT".</summary>
    public string? Trim { get; set; }

    /// <summary>Vehicle Identification Number. Unique per tenant where present.</summary>
    public string? Vin { get; set; }

    /// <summary>Licence plate number.</summary>
    public string? LicensePlate { get; set; }

    /// <summary>Colour.</summary>
    public string? Color { get; set; }

    /// <summary>What was paid for it.</summary>
    public decimal? PurchasePrice { get; set; }

    /// <summary>True when every property is unset, so an empty object can be stored as null.</summary>
    public bool IsEmpty =>
        Year is null
        && string.IsNullOrWhiteSpace(Trim)
        && string.IsNullOrWhiteSpace(Vin)
        && string.IsNullOrWhiteSpace(LicensePlate)
        && string.IsNullOrWhiteSpace(Color)
        && PurchasePrice is null;
}
